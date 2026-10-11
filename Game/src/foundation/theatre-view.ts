import {Container,Graphics,Text, type Texture,Sprite} from "pixi.js";
import type {TheatreObject,TheatreProgram} from "../runtime/theatre-contract.ts";
import {TheatrePlayback} from "../runtime/theatre-playback.ts";

type Drawable={container:Container;graphic?:Graphics;sprite?:Sprite;placeholder?:Text;node:TheatreObject};
function rgba(raw:string):{color:number;alpha:number}{
  const color=parseInt(raw.slice(1,7),16);
  const alpha=raw.length===9?parseInt(raw.slice(7),16)/255:1;
  return {color,alpha};
}
function point(raw:unknown):[number,number]{
  const p=raw as number[];
  return [Number(p[0]),Number(p[1])];
}
function vector(g:Graphics,node:TheatreObject,reveal:number):void{
  const color=rgba(node.color);
  const geo=node.geometry as unknown[];
  if(node.shape==="line"){
    const pts=(geo[1] as unknown[]).map(point);
    const lens=pts.slice(1).map((p,i)=>Math.hypot(p[0]-pts[i][0],p[1]-pts[i][1]));
    let budget=lens.reduce((sum,x)=>sum+x,0)*Math.max(0,Math.min(1,reveal));
    g.clear().moveTo(pts[0][0],pts[0][1]);
    for(let i=1;i<pts.length&&budget>0;i++){
      const seg=lens[i-1],step=Math.min(budget,seg);
      const t=step/seg;
      g.lineTo(pts[i-1][0]+(pts[i][0]-pts[i-1][0])*t,
        pts[i-1][1]+(pts[i][1]-pts[i-1][1])*t);
      budget-=step;
    }
    g.stroke({width:Number(geo[0]),color:color.color,alpha:color.alpha,
      join:"round",cap:"round"});
  }else if(node.shape==="polygon"){
    const pts=(geo as unknown[]).map(point).flat();
    g.clear().poly(pts).fill({color:color.color,alpha:color.alpha});
  }else if(node.shape==="glow"){
    const [w,h]=geo as number[];
    g.clear().ellipse(0,0,w/2,h/2).fill({color:color.color,alpha:color.alpha*.35});
  }else if(node.shape==="light"){
    const [radius]=geo as number[];
    g.clear().circle(0,0,radius).fill({color:color.color,alpha:color.alpha*.1});
  }else if(node.shape==="focus"){
    // The authored focus acts as a post-process composite in Unity.
    // Do not replace it with a black rectangle that conceals all actors.
    g.clear();
  }
}
export interface TheatreAssetResolver {
  (path:string):Texture|null;
}
/**
 * Pixi 8 vector rendering surface for author-authored theatre scenes.
 * Audio/portrait resolution is an explicit host responsibility; no hidden URL
 * fetch or inaccurate image fallback is treated as a resolved asset.
 */
export class TheatreVectorView {
  readonly root=new Container();
  private readonly shapes=new Map<string,Drawable>();
  private missing=new Set<string>();
  private bg=new Graphics();
  constructor(private readonly texture:TheatreAssetResolver=()=>null){
    this.root.visible=false;
    this.root.addChild(this.bg);
  }
  mount(program:TheatreProgram):void{
    this.reset();
    this.root.visible=true;
    this.root.scale.set(1024/program.width,576/program.height);
    const back=rgba(program.background);
    this.bg.rect(0,0,program.width,program.height)
      .fill({color:back.color,alpha:back.alpha});
    for(const node of program.objects){
      const holder=new Container();
      const shape:Drawable={container:holder,node};
      const parent=node.parent?this.shapes.get(node.parent)?.container:this.root;
      if(!parent)throw Error("Theatre renderer: missing parent "+node.parent);
      parent.addChild(holder);
      if(["line","polygon","glow","light","focus"].includes(node.shape)){
        const graphic=new Graphics();
        holder.addChild(graphic);shape.graphic=graphic;
        vector(graphic,node,1);
      }else if(node.shape==="image"){
        const known=this.texture(node.asset);
        if(known){
          const sprite=new Sprite(known);
          sprite.anchor.set(.5,1);
          sprite.width=Number((node.geometry as number[])[0]);
          sprite.height=Number((node.geometry as number[])[1]);
          holder.addChild(sprite);shape.sprite=sprite;
        }else{
          this.missing.add(node.asset);
          const [w,h]=node.geometry as number[];
          const ghost=new Graphics().roundRect(-w/2,-h,w,h,12)
            .fill({color:0x353945,alpha:.6});
          holder.addChild(ghost);
          const label=new Text({text:"未加载立绘\n"+node.asset.split("/").slice(-1)[0],
            style:{fontSize:20,fill:0xffbd9b,align:"center",wordWrap:true,
              wordWrapWidth:Math.max(80,w-16)}});
          label.anchor.set(.5,1);label.y=-h/2;
          holder.addChild(label);shape.placeholder=label;
        }
      }
      this.shapes.set(node.id,shape);
    }
  }
  sync(player:TheatrePlayback):void{
    for(const [id,state] of player.objects){
      const shape=this.shapes.get(id);
      if(!shape)continue;
      const values=state.values;
      shape.container.position.set(values.x,values.y);
      shape.container.scale.set(values["scale-x"],values["scale-y"]);
      shape.container.rotation=values.rotation*Math.PI/180;
      shape.container.alpha=values.opacity;
      shape.container.tint=Math.min(0xffffff,Math.floor(values.brightness*255))*0x010101;
      if(shape.graphic&&shape.node.shape==="line")
        vector(shape.graphic,shape.node,values.reveal);
      if(shape.sprite&&shape.sprite.label!==state.asset){
        const texture=this.texture(state.asset);
        if(texture){shape.sprite.texture=texture;shape.sprite.label=state.asset;}
        else this.missing.add(state.asset);
      }
    }
  }
  missingAssets():string[]{return [...this.missing];}
  reset():void{
    for(const shape of this.shapes.values())shape.container.destroy({children:true});
    this.shapes.clear();this.missing.clear();this.bg.clear();
    this.root.visible=false;
  }
  destroy():void{this.reset();this.root.destroy({children:true});}
}
