/**
 * Phase-2 action pool, modeled after TeamState's fixed slot identity.
 * Distinguish slot id (immutable) from current array offset (changes on use).
 */
export interface ActionDie { slotId: number; value: number }
export class ActionTurnState {
  private dice: ActionDie[] = [];
  private readonly stats = new Map([["violence",-1],["knowledge",1],["sharpness",0],["social",0]]);

  private readonly random: () => number;
  constructor(random: () => number = Math.random) { this.random = random; this.startDay(); }

  private d6(): number {
    const n = this.random();
    if (!Number.isFinite(n) || n < 0 || n >= 1) throw new Error("RNG must return [0,1)");
    return Math.floor(n*6)+1;
  }
  rollFate(): number { return this.d6(); }
  startDay(): void { this.dice = Array.from({length:4}, (_x,slotId) => ({slotId,value:this.d6()})); }
  available(): ActionDie[] { return this.dice.map(x=>({...x})); }
  get(slotId:number): ActionDie {
    const d = this.dice.find(x=>x.slotId===slotId);
    if(!d)throw new Error("unavailable die slot " + slotId);
    return {...d};
  }
  spend(slotId:number): ActionDie {
    const die=this.get(slotId);
    this.dice=this.dice.filter(x=>x.slotId!==slotId);
    return die;
  }
  stat(name:string): number {
    const v=this.stats.get(name.toLowerCase());
    if(v===undefined)throw new Error("unknown actor stat "+name);
    return v;
  }
  restore(data:ActionDie[]):void {
    const ids=new Set<number>();
    if (!Array.isArray(data) || data.some(x=>
      !Number.isInteger(x.slotId)||x.slotId<0||x.slotId>3||
      !Number.isInteger(x.value)||x.value<1||x.value>6||
      (ids.has(x.slotId)?true:!(ids.add(x.slotId)))))throw new Error("invalid action dice snapshot");
    this.dice=data.map(x=>({...x}));
  }
}
