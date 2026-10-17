/** Port of Engine/Runtime/Core/Injury.cs: a single recoverable injury track. */
export const SITES = [
  {part:"手",skill:"violence",name:"力量"},
  {part:"头",skill:"knowledge",name:"见识"},
  {part:"眼",skill:"sharpness",name:"敏锐"},
  {part:"脸",skill:"social",name:"交际"}
] as const;
export interface InjurySnapshot { severity:number; part:string; scars:{part:string;day:number}[] }
export class InjuryState {
  severity=0;
  part="";
  scars:{part:string;day:number}[]=[];
  get band():0|1|2 { return this.severity>=4?2:this.severity>0?1:0; }
  get skill():string { return SITES.find(s=>s.part===this.part)?.skill??""; }
  get modifier():number { return this.band===1?-1:0; }
  /** Each two injury points block one action slot. */
  get actionSlotLoss():number { return Math.floor(this.severity/2); }
  injure(amount:number,random:()=>number=Math.random):boolean {
    if (!Number.isSafeInteger(amount)||amount<=0)throw new Error("injury amount must be positive");
    if(this.severity===0) {
      const r=random();
      if(!Number.isFinite(r)||r<0||r>=1)throw new Error("invalid injury random");
      this.part=SITES[Math.floor(r*SITES.length)].part;
    }
    this.severity=Math.min(7,this.severity+amount);
    return this.severity===7;
  }
  heal(amount:number):void {
    if(!Number.isSafeInteger(amount)||amount<=0)throw new Error("heal amount must be positive");
    this.severity=Math.max(0,this.severity-amount);
    if(this.severity===0)this.part="";
  }
  scarCount(part?:string):number {
    if(part===undefined)return this.scars.length;
    if(!SITES.some(s=>s.part===part))throw new Error("unknown body part "+part);
    return this.scars.filter(x=>x.part===part).length;
  }
  addScar(day:number):void {
    if(!this.part||!Number.isSafeInteger(day)||day<=0)throw new Error("invalid scar");
    this.scars.push({part:this.part,day});
  }
  snapshot():InjurySnapshot {
    return {severity:this.severity,part:this.part,scars:this.scars.map(x=>({...x}))};
  }
  restore(snapshot:InjurySnapshot):void {
    if(!Number.isSafeInteger(snapshot.severity)||snapshot.severity<0||snapshot.severity>7 ||
      (snapshot.severity>0&&!SITES.some(s=>s.part===snapshot.part))||
      (snapshot.severity===0&&snapshot.part!=="")||
      !Array.isArray(snapshot.scars)||snapshot.scars.some(x=>
        !SITES.some(s=>s.part===x.part)||!Number.isSafeInteger(x.day)||x.day<=0))
      throw new Error("invalid injury save");
    this.severity=snapshot.severity;
    this.part=snapshot.part;
    this.scars=snapshot.scars.map(x=>({...x}));
  }
}
