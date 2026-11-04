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
  note: string | null;
};
export interface ActionFeedback {
  title: string;
  result: string;
  changes: { label: string; delta: number }[];
}
export type WebSnapshot = {
  mode: "world" | "scenario";
  day: number; money: number; health: number; busy: boolean; error: string;
  title: string; subtitle: string; path: string[];
  entries: ViewNode[]; clocks: RuntimeClock[];
  dice: DieChoice[]; selectedAction: string | null; selectedDice: DieChoice[];
  requiredDice: number; story: StoryFrame | null;
  storyCount: number; message: string; restBlocked: boolean;
  actors: { id: string; name: string; composure: number; status: string }[];
  restReasons: { id: string; reason: string; location: string; node: string }[];
  dossierOpen: boolean;
  dossiers: DossierEntry[];
  lastAction: ActionFeedback | null;
};
type Allocation = { type: "die"; actorId: string; slotId: number } |
  { type: "item"; itemId: string; qty: number };
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
  private pending: { node: RuntimeNode; dice: DieChoice[] } | null = null;
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
      busy: this.busy, error: this.error,
      title: focus.title, subtitle: focus.subtitle, path: [...this.path],
      entries: entries.map(viewNode), clocks: focus.clocks.map(x => ({ ...x })),
      dice, selectedAction: this.pending?.node.name ?? null,
      selectedDice: [...(this.pending?.dice ?? [])],
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
  async open(id: string): Promise<void> {
    return this.command(async () => {
      if(this.dossierOpen)throw new Error("请先关闭卷宗");
      if (this.pending) throw new Error("请完成当前骰子分配，或按返回取消行动");
      const focus = this.focus();
      const node = [...focus.children,
        ...(this.host.mode === "scenario" && focus === this.host.root ?
          this.host.runner.getCarryNodes() : [])].find(x => x.name === id);
      if (!node || node.disabled) throw new Error("不可访问的节点：" + id);
      this.message = "";
      this.lastAction=null;
      if (node.isPlace) {
        if (this.host.mode !== "world" || focus !== this.host.root) throw new Error("地点只能从城市进入");
        await this.host.enterPlace(node.name);
        this.path = this.host.mode === "world" ? [node.name] : [];
        this.feed(this.host.state.takeStoryEvents());
      } else if (node.children.length) {
        this.path.push(node.name);
      } else if (node.resolve?.kind === "note" || node.resolve?.kind === "observe") {
        this.message = viewNode(node).note ?? node.title;
      } else if (node.resolve?.kind === "instant" || node.resolve?.kind === "roll") {
        this.pending = { node, dice: [] };
        if (!node.requires.some(x => x.type === "die")) await this.commitAction();
      }
    });
  }
  async pickDie(actorId: string, slotId: number): Promise<void> {
    return this.command(async () => {
      if (!this.pending) throw new Error("没有等待分配骰子的行动");
      const available = this.getSnapshot().dice.find(x => x.actorId === actorId && x.slotId === slotId);
      if (!available) throw new Error("骰子不可用");
      if (this.pending.dice.some(x => x.actorId === actorId && x.slotId === slotId)) throw new Error("骰子不能重复");
      this.pending.dice.push(available);
      const needed = this.pending.node.requires.filter(x => x.type === "die").length;
      if (this.pending.dice.length === needed) await this.commitAction();
    });
  }
  private async commitAction(): Promise<void> {
    const pending = this.pending;
    if (!pending) throw new Error("no pending action");
    let index = 0;
    const slots: Allocation[] = pending.node.requires.map(req => {
      if (req.type === "item") return { type: "item", itemId: req.itemId!, qty: req.qty! };
      const die = pending.dice[index++];
      if (!die) throw new Error("missing die for action");
      return { type: "die", actorId: die.actorId, slotId: die.slotId };
    });
    const previousMode = this.host.mode;
    const outcome = await this.host.runner.performSlots(pending.node.name, slots);
    this.pending = null;
    await this.host.refresh();
    const dayAdvance = this.host.takeDayAdvance();
    if (this.host.mode !== previousMode || dayAdvance) this.path = [];
    this.focus();
    this.lastAction=dayAdvance?null:summarizeAction(outcome,pending.node.title,
      slots.filter(x=>x.type==="die").length);
    this.message = dayAdvance ? ("第 " + dayAdvance.after + " 日") :
      pending.node.title + " · " + this.lastAction!.result;
    this.feed([...(dayAdvance?.events ?? []), ...this.host.state.takeStoryEvents()]);
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
      const frames = await this.host.director.endRound();
      await this.host.refresh();
      this.message = "交锋进入下一回合";
      this.feed([...frames.flatMap(x => x.events), ...this.host.state.takeStoryEvents()]);
      this.lastAction=null;
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
      this.lastAction=null;this.dossierOpen=false;this.dossiers=[];
      this.story = new StoryPlaybackQueue();
      this.pendingBanter=[];this.redeemedBubbles.clear();
      this.feed(this.host.state.takeStoryEvents());
    });
  }
}
