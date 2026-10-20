import { GameScriptSession } from "./script-session.ts";
import { GameRuntimeState } from "./game-state.ts";
import { ActionTurnState } from "./turn-state.ts";
import { convertNode, schemeList, type RuntimeNode } from "./node-converter.ts";
import { SITES } from "./injury.ts";
import { resolveFate, type RollOutcome } from "./fate-strip.ts";

/**
 * Companion code for original Scheme-authored nodes. Functions and effect
 * closures stay in their originating LIPS VM; JavaScript never evals closures
 * or tries to serialize live procedures.
 */
const DISPATCH = `
(define __ssnoir-carry-render-root #f)
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
(define (__ssnoir-get-modifiers name)
  (let ((n (__ssnoir-find-node __ssnoir-active-render-root name)))
    (if (not n) (error "modifier node missing")
      (let ((resolve (get-kwarg (cddr n) ':resolve #f)))
        (if (and resolve (or (equal? (car resolve) 'roll)
                             (equal? (car resolve) 'recovery-roll)))
            ((list-ref resolve 2))
            '())))))
(define (__ssnoir-run-place-arrivals! place-name)
  (let ((place (__ssnoir-find-node __ssnoir-active-render-root place-name)))
    (if (or (not place) (not (get-kwarg (cddr place) ':place #f)))
        (error "invalid place arrival destination")
        (for-each (lambda (beat)
          (if (and (pair? beat) (equal? (car beat) 'arrival) (= (length beat) 3))
              ((caddr beat))
              (error "malformed place arrival")))
          (get-kwarg (cddr place) ':arrivals '())))))
(define (__ssnoir-run-captured-node! name chosen)
  (let ((n (or (__ssnoir-find-node __ssnoir-active-render-root name)
               (if __ssnoir-carry-render-root
                   (__ssnoir-find-node __ssnoir-carry-render-root name)
                   #f))))
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
  chosenDie?:number; percentageRoll?:number; prepared?:number;
  before:ReturnType<GameRuntimeState["snapshot"]>;
  after:ReturnType<GameRuntimeState["snapshot"]>;
  clockEffects:ReturnType<GameRuntimeState["takeActionReport"]>;
}
function findNode(root:RuntimeNode, name:string):RuntimeNode|undefined {
  if(root.name===name)return root;
  for(const child of root.children) {
    const n=findNode(child,name);
    if(n)return n;
  }
}
export interface EncounterCarryHooks {
  isSupportUsed():boolean;
  setSupportUsed(used:boolean):void;
  getCarriedSupport():string|false;
}
export class GameActionRunner {
  private root:RuntimeNode|null=null;
  private carry:RuntimeNode[]=[];
  readonly scripts:GameScriptSession;
  readonly state:GameRuntimeState;
  readonly turn:ActionTurnState;
  private readonly carryHooks?:EncounterCarryHooks;
  private constructor(scripts:GameScriptSession,state:GameRuntimeState,turn:ActionTurnState,carryHooks?:EncounterCarryHooks) {
    this.scripts=scripts; this.state=state; this.turn=turn;this.carryHooks=carryHooks;
  }
  static async create(scripts:GameScriptSession,state:GameRuntimeState,turn:ActionTurnState,carryHooks?:EncounterCarryHooks):Promise<GameActionRunner>{
    await scripts.evaluate(DISPATCH);
    const result=new GameActionRunner(scripts,state,turn,carryHooks);
    return result;
  }

  async prepare(source = "(get-render-data)"):Promise<RuntimeNode>{
    // Store Scheme closures in the interpreter, not in serializable game state.
    const value=await this.scripts.evaluate("(define __ssnoir-active-render-root "+source+") __ssnoir-active-render-root");
    this.root=convertNode(value);
    if(this.carryHooks){
      // These authored cards live beside the encounter tree, as in C# CurrentCarryNodes.
      // Keep the Scheme closures in their original encounter VM.
      const raw=await this.scripts.evaluate(
        '(define __ssnoir-carry-render-root (node "__ssnoir-carry" :children (encounter-action-nodes))) __ssnoir-carry-render-root'
      );
      const carry=convertNode(raw).children;
      const seen=new Set<string>();
      const visit=(n:RuntimeNode)=>{seen.add(n.name);for(const child of n.children)visit(child);};
      visit(this.root);
      for(const card of carry){
        if(seen.has(card.name))throw new Error("Encounter carry card name conflicts with scene: "+card.name);
        if(!card.resolve||card.children.length||card.isPlace)throw new Error("invalid encounter carry card: "+card.name);
        if(card.supportId&&card.requires.length)throw new Error("support cards must not have requirement slots");
      }
      this.carry=carry;
    }else this.carry=[];
    return this.root;
  }

  /** The most recently materialized tree, valid until a Scheme mutation.
   * SceneDirector/WorldHost use this to avoid evaluating the same authored
   * world tree again after enterPlace has already refreshed it.
   */
  getPreparedRoot():RuntimeNode|null {return this.root;}
  getCarryNodes():RuntimeNode[]{return [...this.carry];}

  /** World navigation is not an action; it never consumes a die or advances a day. */
  async enterPlace(name:string):Promise<RuntimeNode>{
    if(!this.root)await this.prepare();
    if(!this.root)throw new Error("world tree missing");
    const world=this.root;
    const requested=world.children.find(x=>x.name===name);
    if(!requested||!requested.isPlace||requested.disabled)throw new Error("unavailable world place: "+name);
    const before=this.state.snapshot();
    const previous=this.root;
    this.state.beginActionReport();
    try {
      await this.scripts.evaluate("(on-enter-place "+JSON.stringify(name)+")");
      // on-enter-place can change which arrival beats are present.
      const refreshed=await this.prepare();
      const place=refreshed.children.find(x=>x.name===name);
      if(!place||!place.isPlace||place.disabled)throw new Error("place disappeared during entry");
      // If there are no arrivals, the first post-entry materialization is
      // already authoritative. Re-evaluating get-render-data is redundant
      // and can cost seconds for the full chapter-one city content.
      if(place.arrivals.length) {
        await this.scripts.evaluate("(__ssnoir-run-place-arrivals! "+JSON.stringify(name)+")");
      }
      // C# arrivals are a story beat, not an action report with costs.
      const report=this.state.takeActionReport();
      if(report.length)throw new Error("arrival produced action clock costs");
      if(!place.arrivals.length)return place;
      const updated=await this.prepare();
      const result=updated.children.find(x=>x.name===name);
      if(!result||!result.isPlace)throw new Error("entered place removed itself");
      return result;
    }catch(error){
      this.state.cancelActionReport();
      this.state.restore(before);
      this.root=previous;
      throw error;
    }
  }

  /** C#-parity action slot resolution on TeamState (all actors, multi-die). */
  async performSlots(name:string,slots:(
    {type:"die";actorId:string;slotId:number}|
    {type:"item";itemId:string;qty:number}
  )[]):Promise<ActionResult>{
    if(!this.root)throw new Error("render tree must be prepared");
    const node=findNode(this.root,name)??this.carry.find(n=>n.name===name);
    if(!node||node.disabled||!node.resolve||
      (node.resolve.kind!=="roll"&&node.resolve.kind!=="instant"))
      throw new Error("unavailable action "+name);
    if(slots.length!==node.requires.length)throw new Error("slot count mismatch");
    if(node.supportId){
      if(!this.carryHooks||this.state.currentMode()!=="encounter")
        throw new Error("support card only usable during an encounter");
      if(this.carryHooks.getCarriedSupport()!==node.supportId)
        throw new Error("support card does not match the carried relationship");
      if(this.carryHooks.isSupportUsed())throw new Error("support already used this encounter");
    }
    const dice:Array<{actorId:string;slotId:number;value:number}>=[];
    const used=new Set<string>();
    for(let i=0;i<slots.length;i++){
      const item=slots[i],req=node.requires[i];
      if(item.type!==req.type)throw new Error("slot type mismatch");
      if(item.type==="item"){
        if(req.itemId!.toLowerCase()!==item.itemId.toLowerCase()||req.qty!==item.qty)
          throw new Error("item slot mismatch");
        if(this.state.getItemCount(item.itemId)<item.qty)throw new Error("insufficient item "+item.itemId);
      }else{
        if(this.state.team.status(item.actorId)!=="active")throw new Error("inactive actor");
        const key=item.actorId+"/"+item.slotId;
        if(used.has(key))throw new Error("die used twice");used.add(key);
        const die=this.state.team.dice(item.actorId).find(x=>x.slotId===item.slotId);
        if(!die)throw new Error("die slot unavailable "+key);
        dice.push({...item,value:die.value});
      }
    }
    if(node.resolve.kind==="roll"&&!dice.length)throw new Error("roll needs a die");
    const actorId=dice[0]?.actorId??"player",actor=this.state.team.require(actorId);
    if(actor.status!=="active")throw new Error("inactive acting actor");
    const before=this.state.snapshot();
    const priorSupportUsed=this.carryHooks?.isSupportUsed()??false;
    this.state.beginActionContext(actorId,this.state.currentMode() as "world"|"encounter");
    this.state.beginActionReport();
    try{
      let roll:ReturnType<typeof resolveFate>|undefined;
      if(node.resolve.kind==="roll"){
        const raw=await this.scripts.evaluate(
          "(__ssnoir-get-modifiers "+JSON.stringify(name)+")");
        let modifier=0;
        for(const entry of schemeList(raw,"action modifiers")){
          const parts=schemeList(entry,"modifier");
          if(parts.length!==3||String(parts[0])!=="modifier")throw new Error("invalid modifier");
          const value=Number(String(parts[1]));
          if(!Number.isSafeInteger(value))throw new Error("invalid modifier value");
          modifier+=value;
        }
        const skill=node.resolve.skill!;
        if(actor.role==="protagonist"){
          if(this.state.injury.skill===skill)modifier+=this.state.injury.modifier;
          modifier-=this.state.injury.scars.filter(s=>
            SITES.find(p=>p.part===s.part)?.skill===skill).length;
        }
        roll=resolveFate(dice[0].value,this.state.team.stat(actorId,skill),
          modifier,this.turn.rollPercentage());
      }
      // In C# the support is charged before its effect; failures restore
      // the TS-only flag consistently with the native-state rollback.
      if(node.supportId)this.carryHooks!.setSupportUsed(true);
      await this.scripts.evaluate("(__ssnoir-run-captured-node! "+
        JSON.stringify(name)+" "+JSON.stringify(roll?.outcome??"success")+")");
      for(const item of slots){
        if(item.type==="die")this.state.team.spendDie(item.actorId,item.slotId);
        // C# SceneManager.ExecuteAction clips an item-slot spend to zero.
        else this.state.setItemCount(item.itemId,Math.max(0,this.state.getItemCount(item.itemId)-item.qty));
      }
      if(!this.state.hasPendingHospitalization())await this.scripts.evaluate("(on-action)");
      return {name,mode:node.resolve.kind,outcome:roll?.outcome,
        chosenDie:dice[0]?.value,prepared:roll?.prepared,percentageRoll:roll?.percentageRoll,
        before,after:this.state.snapshot(),clockEffects:this.state.takeActionReport()};
    }catch(error){
      this.state.cancelActionReport();this.state.restore(before);
      if(node.supportId)this.carryHooks?.setSupportUsed(priorSupportUsed);
      throw error;
    }finally{this.state.clearActionContext();}
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
    this.state.beginActionContext("player","world");
    this.state.beginActionReport();
    try {
      let result:ReturnType<typeof resolveFate>|undefined;
      if(node.resolve.kind==="roll")result=resolveFate(
        selected!.value,this.turn.stat(node.resolve.skill!),0,this.turn.rollPercentage()
      );
      const outcome=result?.outcome??"success";
      await this.scripts.evaluate("(__ssnoir-run-captured-node! "+JSON.stringify(name)+" "+JSON.stringify(outcome)+")");
      for(const cost of node.requires.filter(x=>x.type==="item")) {
        this.state.setItemCount(cost.itemId!,this.state.getItemCount(cost.itemId!)-cost.qty!);
      }
      if(selected)this.turn.spend(selected.slotId);
      // C# SceneManager executes on-action AFTER the outcome and resource
      // consumption, in the initiating interpreter's original VM.
      if(!this.state.hasPendingHospitalization()) await this.scripts.evaluate("(on-action)");
      return { name, mode:node.resolve.kind, outcome:result?.outcome,
        chosenDie:selected?.value,percentageRoll:result?.percentageRoll,prepared:result?.prepared,
        before,after:this.state.snapshot(),clockEffects:this.state.takeActionReport() };
    }catch(error){
      this.state.cancelActionReport();
      this.state.restore(before);
      this.turn.restore(diceBefore);
      throw error;
    }finally{
      this.state.clearActionContext();
    }
  }
}
