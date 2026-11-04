import type { WorldHost } from "../runtime/world-host";
import type { RuntimeNode, RuntimeClock, ActionCost } from "../runtime/node-converter";
import { StoryPlaybackQueue, type StoryFrame } from "../runtime/story-player.ts";
import type { StoryEvent } from "../runtime/story-events";
import type { ActionResult } from "../runtime/action-runner.ts";
import type { DossierEntry } from "../runtime/dossier-converter.ts";

export type DieChoice = { actorId: string; actorName: string; slotId: number; value: number };
export type ViewNode = {
  id: string; title: string; subtitle: string; isPlace: boolean; disabled: boolean;
  tags: string[]; clocks: RuntimeClock[]; requires: ActionCost[];
  action: string | null; risk: string | null; childCount: number;
  note: string | null; groupPath?: string[];
};
export interface ActionFeedback {
  title: string;
  result: string;
  changes: { label: string; delta: number }[];
}
export type WebSnapshot = {
  mode: "world" | "scenario";
  day: number; money: number; health: number; inventory: {id: string; quantity: number}[]; busy: boolean; error: string;
  title: string; subtitle: string; path: string[];
  entries: ViewNode[]; displayCards: ViewNode[]; mapPlaces: ViewNode[]; clocks: RuntimeClock[];
  dice: DieChoice[]; selectedAction: string | null; selectedDice: DieChoice[];
  selectedSlots: (Allocation | null)[];
  actionPreview: {prepared:number;fail:number;neutral:number;success:number}|null;
  canExecute:boolean; resultOpen:boolean;
  requiredDice: number; story: StoryFrame | null;
  storyCount: number; message: string; restBlocked: boolean;
  actors: { id: string; name: string; composure: number; status: string }[];
  restReasons: { id: string; reason: string; location: string; node: string }[];
  dossierOpen: boolean;
  dossiers: DossierEntry[];
  lastAction: ActionFeedback | null;
  failure: {title:string;description:string}|null;
};
export type Allocation = { type: "die"; actorId: string; slotId: number } |
  { type: "item"; itemId: string; qty: number };
export type DropResource = {type:"die";actorId:string;slotId:number} |
  {type:"item";itemId:string};
function descend(nodes:RuntimeNode[],id:string):RuntimeNode|undefined{
  for(const node of nodes){
    if(node.name===id)return node;
    const child=descend(node.children,id);
    if(child)return child;
  }
}
function flattenCards(nodes:RuntimeNode[],groupPath:string[]=[]):ViewNode[]{
  const result:ViewNode[]=[];
  for(const node of nodes){
    if(node.isPlace)continue;
    result.push({...viewNode(node),groupPath:[...groupPath]});
    if(node.children.length)result.push(...flattenCards(node.children,
      [...groupPath,node.title]));
  }
  return result;
}
function viewNode(n: RuntimeNode): ViewNode {
  const kind = n.resolve?.kind ?? null;
  return {
    id: n.name, title: n.title, subtitle: n.subtitle,
    isPlace: n.isPlace, disabled: n.disabled, tags: [...n.tags],
    clocks: [...n.clocks, ...(n.resolve?.noteClock ? [n.resolve.noteClock] : [])]
      .map(c => ({ ...c })),
    requires: n.requires.map(c => ({ ...c })),
    action: kind, risk: n.risk?.level ?? null,
    childCount: n.children.length,
    note: kind === "observe" ? n.resolve?.text ?? "" :
      kind === "note" ? [n.resolve?.noteTitle, n.resolve?.noteText].filter(Boolean).join(" — ") : null
  };
}
function summarizeAction(result: ActionResult, title: string, diceSpent: number): ActionFeedback {
  const resultText: Record<string,string> = { success:"成功", neutral:"一般", fail:"失败" };
  const label = result.outcome ? (resultText[result.outcome] ?? result.outcome) : "完成";
  const changes: ActionFeedback["changes"] = [];
  const add = (name:string, delta:number):void => {
    if(delta)changes.push({label:name,delta});
  };
  if(result.before&&result.after){
    const original=result.before,updated=result.after;
    const first=original.team.actors.find(x=>x.id==="player");
    const second=updated.team.actors.find(x=>x.id==="player");
    if(first&&second)add("冷静",second.composure-first.composure);
    add("伤势",updated.injury.severity-original.injury.severity);
    for(const key of new Set([...Object.keys(original.inventory),...Object.keys(updated.inventory)]))
      add(key,(updated.inventory[key]??0)-(original.inventory[key]??0));
    add("成长",updated.growthLevel-original.growthLevel);
  }
  for(const effect of result.clockEffects??[])
    add(effect.displayLabel,effect.delta);
  if(diceSpent)add("行动骰",-diceSpent);
  return {title,result:label,changes};
}
/** Stable seedable generator. All player-visible game randomness comes from one stream. */
export function seededRandom(seed: number): () => number {
  if (!Number.isSafeInteger(seed) || seed < 0 || seed > 0xffffffff) {
    throw new Error("seed must be an unsigned 32-bit integer");
  }
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/**
 * Browser presentation facade over the existing Scheme world.
 * Does not own game rules, dates, inventory, clocks or authored story state.
 * The session is a serialized command boundary; presentation never runs Scheme code.
 */
export class WebGameSession {
  private readonly host: WorldHost;
  private readonly listeners = new Set<() => void>();
  private story = new StoryPlaybackQueue();
  private pendingBanter:StoryFrame[]=[];
  private redeemedBubbles=new Set<number>();
  private path: string[] = [];
  private pending: {
    node:RuntimeNode;slots:(Allocation|null)[];autoCommit:boolean;
    preview:{prepared:number;fail:number;neutral:number;success:number}|null;
  }|null=null;
  private resultOpen=false;
  private busy = false;
  private error = "";
  private message = "";
  private dossierOpen=false;
  private dossiers:DossierEntry[]=[];
  private lastAction:ActionFeedback|null=null;
  constructor(host: WorldHost) {
    this.host = host;
    this.feed(host.state.takeStoryEvents());
  }
  subscribe(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }
  private emit(): void { for (const listener of this.listeners) listener(); }
  private feed(events: StoryEvent[]): void {
    this.story.push(events);
    this.pendingBanter.push(...this.story.takeBanter());
    this.presentBanter();
  }
  /** Recovery is paid only when this Web client actually shows the bubble.
   * Blocking story frames postpone it; crossing a day discards the reward.
   */
  private presentBanter():void{
    if(this.story.blocked||!this.pendingBanter.length)return;
    const shown=this.pendingBanter.splice(0);
    const notices:string[]=[];
    const day=this.host.state.getGlobal("世界日");
    for(const frame of shown){
      const recovery=frame.recovery;
      if(!recovery||this.redeemedBubbles.has(recovery.id))continue;
      this.redeemedBubbles.add(recovery.id);
      if(day!==recovery.worldDay||!this.host.state.team.find(recovery.actorId))continue;
      this.host.state.team.restoreComposure(recovery.actorId,recovery.amount);
      this.host.state.notify(recovery.notice);
      notices.push(recovery.notice);
    }
    this.message=[shown.map(x=>x.text).filter(Boolean).join(" / "),...notices]
      .filter(Boolean).join(" · ").slice(0,500);
  }
  private focus(): RuntimeNode {
    let node = this.host.root;
    const valid: string[] = [];
    for (const name of this.path) {
      const next = node.children.find(x => x.name === name);
      if (!next) break;
      valid.push(name);
      node = next;
    }
    this.path = valid;
    return node;
  }
  getSnapshot(): WebSnapshot {
    const focus = this.focus();
    const roster = this.host.state.team.roster();
    const dice: DieChoice[] = roster.filter(x => x.status === "active")
      .flatMap(actor => this.host.state.team.dice(actor.id).map(die => ({
        actorId: actor.id, actorName: actor.name, slotId: die.slotId, value: die.value
      })));
    const entries = [...focus.children,
      ...(this.host.mode === "scenario" && focus === this.host.root ?
        this.host.runner.getCarryNodes() : [])];
    const day = this.host.state.getGlobal("世界日");
    return {
      mode: this.host.mode, day: typeof day === "number" ? day : 1,
      money: this.host.state.getItemCount("金钱"),
      health: this.host.state.injury.severity,
      inventory: this.host.state.listInventory(),
      failure: this.host.state.getFailure(),
      busy: this.busy, error: this.error,
      title: focus.title, subtitle: focus.subtitle, path: [...this.path],
      entries: entries.map(viewNode),
      displayCards:this.host.mode==="world"&&!this.path.length ? [] :
        flattenCards(entries),
      mapPlaces: this.host.mode==="world" ?
        this.host.root.children.filter(x=>x.isPlace).map(viewNode) : [],
      clocks: focus.clocks.map(x => ({ ...x })),
      dice, selectedAction: this.pending?.node.name ?? null,
      selectedDice: (this.pending?.slots??[]).filter((x):x is Extract<Allocation,{type:"die"}> =>
        x?.type==="die").map(x=>dice.find(d=>d.actorId===x.actorId&&d.slotId===x.slotId))
        .filter((x):x is DieChoice=>Boolean(x)),
      selectedSlots:this.pending?.slots.map(x=>x?{...x}:null)??[],
      actionPreview:this.pending?.preview?{...this.pending.preview}:null,
      canExecute:Boolean(this.pending?.slots.every(x=>x!==null)),
      resultOpen:this.resultOpen,
      requiredDice: this.pending?.node.requires.filter(x => x.type === "die").length ?? 0,
      story: this.story.current, storyCount: this.story.pendingCount(),
      message: this.message, restBlocked: this.host.state.hasRestBlockers(),
      actors: roster.map(x => ({ id: x.id, name: x.name, composure: x.composure, status: x.status })),
      restReasons: this.host.state.snapshot().restBlockers,
      dossierOpen:this.dossierOpen,
      dossiers:this.dossiers.map(x=>({...x,clocks:x.clocks.map(c=>({...c})),
        steps:x.steps.map(s=>({...s})),log:x.log.map(row=>({...row}))})),
      lastAction:this.lastAction?{...this.lastAction,changes:this.lastAction.changes.map(x=>({...x}))}:null
    };
  }
  private async command(body: () => Promise<void>): Promise<void> {
    if (this.busy) return;
    if (this.story.blocked) return;
    this.busy = true; this.error = ""; this.emit();
    try { await body(); }
    catch (error) {
      this.error = String(error instanceof Error ? error.message : error);
      console.error("[SSNoir web]", error);
    }
    finally { this.busy = false; this.emit(); }
  }
  private availableNode(id:string):RuntimeNode {
    const focus=this.focus();
    const nodes=[...focus.children,
      ...(this.host.mode==="scenario"&&focus===this.host.root ?
        this.host.runner.getCarryNodes():[])];
    const node=descend(nodes,id);
    if(!node||node.disabled)throw new Error("不可访问的节点："+id);
    return node;
  }
  private stage(node:RuntimeNode,autoCommit:boolean):void {
    if(node.children.length||node.isPlace||
      (node.resolve?.kind!=="instant"&&node.resolve?.kind!=="roll"))
      throw new Error("该节点不是可以执行的行动");
    this.pending={node,autoCommit,preview:null,
      slots:node.requires.map(req=>req.type==="item"&&autoCommit?
        {type:"item" as const,itemId:req.itemId!,qty:req.qty!}:null)};
    this.resultOpen=false;
  }
  /** Normal UI: a card's slots are always visible; no preliminary card click. */
  async assignToSlot(id:string,index:number,resource:DropResource):Promise<void>{
    return this.command(async()=>{
      if(this.dossierOpen)throw new Error("请先关闭卷宗");
      const node=this.availableNode(id);
      const req=node.requires[index];
      if(!req)throw new Error("行动没有对应的资源槽");
      if(req.type!==resource.type)throw new Error("资源类型与行动槽不匹配");
      if(!this.pending||this.pending.node.name!==id)this.stage(node,false);
      const pending=this.pending!;
      const original=pending.slots.map(x=>x?{...x}:null);
      const previousPreview=pending.preview;
      try {
        let allocation:Allocation;
        if(resource.type==="die"){
          const die=this.getSnapshot().dice.find(d=>
            d.actorId===resource.actorId&&d.slotId===resource.slotId);
          if(!die)throw new Error("骰子已经消耗或不可用");
          if(pending.slots.some((x,i)=>i!==index&&x?.type==="die"&&
             x.actorId===resource.actorId&&x.slotId===resource.slotId))
            throw new Error("同一枚骰子不能投入两个槽");
          allocation={type:"die",actorId:resource.actorId,slotId:resource.slotId};
        } else {
          if(req.itemId?.toLowerCase()!==resource.itemId.toLowerCase())
            throw new Error("这个槽需要："+req.itemId);
          const needed=req.qty??1;
          const reserved=pending.slots.reduce((n,x,i)=>n+(i!==index&&x?.type==="item"&&
            x.itemId.toLowerCase()===resource.itemId.toLowerCase()?x.qty:0),0);
          if(this.host.state.getItemCount(resource.itemId)<reserved+needed)
            throw new Error("物品数量不足");
          allocation={type:"item",itemId:req.itemId!,qty:needed};
        }
        pending.slots[index]=allocation;
        const first=pending.slots.find((x):x is Extract<Allocation,{type:"die"}> =>
          x?.type==="die");
        pending.preview=pending.node.resolve?.kind==="roll"&&first?
          await this.host.runner.previewRoll(pending.node.name,first.actorId,first.slotId):null;
      }catch(error){
        pending.slots=original;pending.preview=previousPreview;
        throw error;
      }
    });
  }
  async clearSlot(id:string,index:number):Promise<void>{
    return this.command(async()=>{
      if(!this.pending||this.pending.node.name!==id) return;
      if(!this.pending.slots[index])return;
      this.pending.slots[index]=null;
      const first=this.pending.slots.find((x):x is Extract<Allocation,{type:"die"}> =>
        x?.type==="die");
      this.pending.preview=this.pending.node.resolve?.kind==="roll"&&first?
        await this.host.runner.previewRoll(id,first.actorId,first.slotId):null;
    });
  }
  async executeAction(id:string):Promise<void>{
    return this.command(async()=>{
      if(this.dossierOpen)throw new Error("请先关闭卷宗");
      if(!this.pending||this.pending.node.name!==id)this.stage(this.availableNode(id),false);
      if(this.pending!.slots.some(x=>x===null))
        throw new Error("请先把骰子或物品放入所有必需的槽");
      await this.commitAction();
    });
  }
  async cancelAction():Promise<void>{
    return this.command(async()=>{this.pending=null;this.message="";});
  }
  dismissResult():void{this.resultOpen=false;this.emit();}

  /** Keep legacy scripted card clicks compatible; the drag UI uses assignToSlot instead. */
  async open(id: string): Promise<void> {
    return this.command(async () => {
      if(this.dossierOpen)throw new Error("请先关闭卷宗");
      if(this.pending)throw new Error("请完成当前骰子分配，或按返回取消行动");
      const node=this.availableNode(id);
      const focus=this.focus();
      this.message="";this.lastAction=null;this.resultOpen=false;
      if(node.isPlace){
        if(this.host.mode!=="world"||focus!==this.host.root)
          throw new Error("地点只能从城市进入");
        await this.host.enterPlace(node.name);
        this.path=this.host.mode==="world"?[node.name]:[];
        this.feed(this.host.state.takeStoryEvents());
      } else if(node.children.length){
        // Existing containers still support semantic focus for keyboard and old saves.
        // Direct card rendering below exposes their actions without opening them.
        const findPath=(nodes:RuntimeNode[],id:string,prefix:string[]=[]):string[]|null=>{
          for(const item of nodes){
            const next=[...prefix,item.name];
            if(item.name===id)return next;
            const sub=findPath(item.children,id,next);
            if(sub)return sub;
          }
          return null;
        };
        const path=findPath(focus.children,id);
        if(!path)throw new Error("容器不属于当前节点："+id);
        this.path.push(...path);
      } else if(node.resolve?.kind==="note"||node.resolve?.kind==="observe"){
        this.message=viewNode(node).note??node.title;
      } else if(node.resolve?.kind==="instant"||node.resolve?.kind==="roll"){
        this.stage(node,true);
        if(!node.requires.some(x=>x.type==="die"))await this.commitAction();
      }
    });
  }
  async pickDie(actorId:string,slotId:number):Promise<void>{
    return this.command(async()=>{
      const pending=this.pending;
      if(!pending)throw new Error("没有等待分配骰子的行动");
      const index=pending.node.requires.findIndex((req,i)=>req.type==="die"&&
        pending.slots[i]===null);
      if(index<0)throw new Error("当前行动没有空的骰子槽");
      const available=this.getSnapshot().dice.find(x=>
        x.actorId===actorId&&x.slotId===slotId);
      if(!available)throw new Error("骰子不可用");
      if(pending.slots.some(x=>x?.type==="die"&&
        x.actorId===actorId&&x.slotId===slotId))throw new Error("骰子不能重复");
      pending.slots[index]={type:"die",actorId,slotId};
      if(!pending.autoCommit&&pending.node.resolve?.kind==="roll"){
        pending.preview=await this.host.runner.previewRoll(pending.node.name,actorId,slotId);
      }
      if(pending.autoCommit&&pending.slots.every(x=>x!==null))await this.commitAction();
    });
  }
  private async commitAction():Promise<void>{
    const pending=this.pending;
    if(!pending)throw new Error("no pending action");
    if(pending.slots.some(x=>x===null))throw new Error("行动资源槽尚未填满");
    const slots=pending.slots as Allocation[];
    const previousMode=this.host.mode;
    const outcome=await this.host.runner.performSlots(pending.node.name,slots);
    this.pending=null;
    await this.host.refresh();
    const dayAdvance=this.host.takeDayAdvance();
    if(this.host.mode!==previousMode||dayAdvance)this.path=[];
    this.focus();
    this.lastAction=dayAdvance?null:summarizeAction(outcome,pending.node.title,
      slots.filter(x=>x.type==="die").length);
    this.resultOpen=!pending.autoCommit&&Boolean(this.lastAction);
    this.message=dayAdvance?("第 "+dayAdvance.after+" 日"):
      pending.node.title+" · "+this.lastAction!.result;
    this.feed([...(dayAdvance?.events??[]),...this.host.state.takeStoryEvents()]);
  }
  /** A map pin is authoritative navigation, even while a different location is open. */
  async switchPlace(id:string):Promise<void>{
    return this.command(async()=>{
      if(this.host.mode!=="world")throw new Error("交锋中不能切换城市地点");
      const place=this.host.getPlaces().find(x=>x.name===id&&!x.disabled);
      if(!place)throw new Error("地点尚未开放："+id);
      if(this.path[0]===id&&this.path.length===1){
        this.pending=null;this.resultOpen=false;return;
      }
      await this.host.enterPlace(id);
      this.pending=null;this.path=this.host.mode==="world"?[id]:[];
      this.resultOpen=false;this.lastAction=null;this.message="";
      this.feed(this.host.state.takeStoryEvents());
    });
  }
  async closeLocation():Promise<void>{
    return this.command(async()=>{
      if(this.host.mode!=="world")return;
      if(this.path.length){
        await this.host.leavePlace();
        this.path=[];
        this.feed(this.host.state.takeStoryEvents());
      }
      this.pending=null;this.resultOpen=false;this.lastAction=null;this.message="";
    });
  }
  async back(): Promise<void> {
    return this.command(async () => {
      this.message="";
      if (this.pending) this.pending = null;
      else if (this.path.length === 1 && this.host.mode === "world") {
        await this.host.leavePlace();
        this.path = [];
        this.feed(this.host.state.takeStoryEvents());
      }
      else if (this.path.length) this.path.pop();
    });
  }
  /** Read the authoritative, current Scheme dossier on demand. Never reveal future steps. */
  async toggleDossier():Promise<void>{
    return this.command(async()=>{
      if(this.host.mode!=="world")throw new Error("交锋中暂时无法打开城市卷宗");
      if(this.pending)throw new Error("请先取消正在分配的骰子");
      if(this.dossierOpen){this.dossierOpen=false;return;}
      await new Promise<void>(resolve=>setTimeout(resolve,20));
      this.dossiers=await this.host.getDossier();
      this.dossierOpen=true;
    });
  }
  /** The toolbar only navigates to the authored lodging; the sleep node owns day advance. */
  async goHome(): Promise<void> {
    return this.command(async () => {
      if (this.host.mode !== "world") throw new Error("交锋中不能返回旅馆");
      if (this.pending) throw new Error("请先取消正在分配的骰子");
      const home = this.host.getPlaces().find(x => x.name === "家" && !x.disabled);
      if (!home) throw new Error("当前没有可进入的住所");
      if (this.path[0] !== home.name) {
        await this.host.enterPlace(home.name);
        this.path = [home.name];
        this.feed(this.host.state.takeStoryEvents());
      } else this.path = [home.name];
    });
  }
  continueStory(): void {
    if (!this.story.blocked) return;
    this.story.advance();
    this.presentBanter();
    this.emit();
  }
  async endTurn(): Promise<void> {
    return this.command(async () => {
      if(this.host.mode === "world")
        throw new Error("城市换日必须在住所执行 Scheme 的睡觉行动");
      this.pending = null;
      const before=this.host.state.snapshot();
      const frames = await this.host.director.endRound();
      await this.host.refresh();
      const advance=this.host.takeDayAdvance();
      const after=this.host.state.snapshot();
      // Enemy-round consequences are real state deltas; do not infer them
      // from story captions or add a separate UI-side gameplay rule.
      this.resultOpen=false;
      this.lastAction=advance?null:summarizeAction({
        name:"交锋回合",mode:"instant",before,after,clockEffects:[]
      },"回合结算",0);
      this.message = advance?("第 "+advance.after+" 日 · 在诊所醒来"):
        "交锋进入下一回合";
      this.feed([...frames.flatMap(x => x.events),...(advance?.events??[]),
        ...this.host.state.takeStoryEvents()]);
      this.path = [];
    });
  }
  async exportSave(): Promise<string> {
    if (this.busy || this.story.blocked || this.pending) throw new Error("当前不能存档");
    return this.host.save();
  }
  async importSave(text: string): Promise<void> {
    return this.command(async () => {
      // Yield one macrotask so the browser can acknowledge the click and
      // present its busy state before the full Scheme VM reconstruction.
      await new Promise<void>(resolve => setTimeout(resolve, 25));
      await this.host.load(text);
      this.path = []; this.pending = null; this.message = "存档已载入";
      this.lastAction=null;this.resultOpen=false;this.dossierOpen=false;this.dossiers=[];
      this.story = new StoryPlaybackQueue();
      this.pendingBanter=[];this.redeemedBubbles.clear();
      this.feed(this.host.state.takeStoryEvents());
    });
  }
}
