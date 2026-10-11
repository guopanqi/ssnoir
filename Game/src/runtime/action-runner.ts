import { GameScriptSession } from "./script-session.ts";
import { GameRuntimeState } from "./game-state.ts";
import { ActionTurnState } from "./turn-state.ts";
import { convertNode, type RuntimeNode } from "./node-converter.ts";
import { resolveFate, type RollOutcome } from "./fate-strip.ts";

/**
 * Companion code for original Scheme-authored nodes. Functions and effect
 * closures stay in their originating LIPS VM; JavaScript never evals closures
 * or tries to serialize live procedures.
 */
const DISPATCH = `
(define (__ssnoir-find-node node name)
  (if (equal? (cadr node) name)
    node
    (__ssnoir-find-children (get-kwarg (cddr node) ':children '()) name)))
(define (__ssnoir-find-children children name)
  (if (null? children)
    #f
    (let ((found (__ssnoir-find-node (car children) name)))
      (if found found (__ssnoir-find-children (cdr children) name)))))
(define (__ssnoir-effect! outcome)
  (if (and (pair? outcome) (equal? (car outcome) 'outcome))
    ((cadr outcome))
    (outcome)))
(define (__ssnoir-run-captured-node! name chosen)
  (let ((n (__ssnoir-find-node __ssnoir-active-render-root name)))
    (if (not n)
      (error "action node vanished from render tree")
      (let ((resolve (get-kwarg (cddr n) ':resolve #f)))
        (if (not resolve)
          (error "cannot execute container")
          (cond
            ((equal? (car resolve) 'instant) (__ssnoir-effect! (cadr resolve)))
            ((or (equal? (car resolve) 'roll) (equal? (car resolve) 'recovery-roll))
              (__ssnoir-effect!
                (cond ((equal? chosen "fail") (list-ref resolve 3))
                      ((equal? chosen "neutral") (list-ref resolve 4))
                      ((equal? chosen "success") (list-ref resolve 5))
                      (else (error "unknown roll outcome")))))
            (else (error "node is not an executable action"))))))))
`;

export interface ActionResult {
  name:string; mode:"instant"|"roll"; outcome?:RollOutcome;
  chosenDie?:number; fateDie?:number; prepared?:number;
  before:ReturnType<GameRuntimeState["snapshot"]>;
  after:ReturnType<GameRuntimeState["snapshot"]>;
}
function findNode(root:RuntimeNode, name:string):RuntimeNode|undefined {
  if(root.name===name)return root;
  for(const child of root.children) {
    const n=findNode(child,name);
    if(n)return n;
  }
}
export class GameActionRunner {
  private root:RuntimeNode|null=null;
  readonly scripts:GameScriptSession;
  readonly state:GameRuntimeState;
  readonly turn:ActionTurnState;
  private constructor(scripts:GameScriptSession,state:GameRuntimeState,turn:ActionTurnState) {
    this.scripts=scripts; this.state=state; this.turn=turn;
  }
  static async create(scripts:GameScriptSession,state:GameRuntimeState,turn:ActionTurnState):Promise<GameActionRunner>{
    await scripts.evaluate(DISPATCH);
    const result=new GameActionRunner(scripts,state,turn);
    return result;
  }

  async prepare(source = "(get-render-data)"):Promise<RuntimeNode>{
    // Store Scheme closures in the interpreter, not in serializable game state.
    const value=await this.scripts.evaluate("(define __ssnoir-active-render-root "+source+") __ssnoir-active-render-root");
    this.root=convertNode(value);
    return this.root;
  }

  async perform(name:string,dieSlotId?:number):Promise<ActionResult>{
    if(!this.root)throw new Error("prepare a render tree first");
    const node=findNode(this.root,name);
    if(!node||!node.resolve||node.disabled)throw new Error("unavailable action "+name);
    if(node.resolve.kind!=="instant"&&node.resolve.kind!=="roll")throw new Error("non-action node "+name);
    const requiredDice=node.requires.filter(x=>x.type==="die");
    if(requiredDice.length>1)throw new Error("multi-die requirements are not implemented");
    let selected:ReturnType<ActionTurnState["get"]>|null=null;
    if(requiredDice.length) {
      if(dieSlotId===undefined)throw new Error("missing required die");
      selected=this.turn.get(dieSlotId);
    } else if(dieSlotId!==undefined)throw new Error("action does not require a die");
    for(const cost of node.requires.filter(x=>x.type==="item")) {
      if(this.state.getItemCount(cost.itemId!)<cost.qty!)throw new Error("insufficient item "+cost.itemId);
    }
    if(node.resolve.kind==="roll"&&!selected)throw new Error("roll action requires die slot");

    const before=this.state.snapshot(), diceBefore=this.turn.available();
    try {
      let result:ReturnType<typeof resolveFate>|undefined;
      if(node.resolve.kind==="roll")result=resolveFate(
        selected!.value,this.turn.stat(node.resolve.skill!),0,this.turn.rollFate()
      );
      const outcome=result?.outcome??"success";
      await this.scripts.evaluate("(__ssnoir-run-captured-node! "+JSON.stringify(name)+" "+JSON.stringify(outcome)+")");
      for(const cost of node.requires.filter(x=>x.type==="item")) {
        this.state.setItemCount(cost.itemId!,this.state.getItemCount(cost.itemId!)-cost.qty!);
      }
      if(selected)this.turn.spend(selected.slotId);
      return { name, mode:node.resolve.kind, outcome:result?.outcome,
        chosenDie:selected?.value,fateDie:result?.fateDie,prepared:result?.prepared,
        before,after:this.state.snapshot() };
    }catch(error){
      this.state.restore(before);
      this.turn.restore(diceBefore);
      throw error;
    }
  }
}
