import {Container,Graphics,Text,Sprite, type Texture} from "pixi.js";
import {StagePlayback,type StageEntity} from "../runtime/stage-playback.ts";

export interface StageAssetResolver {(path:string):Texture|null}
interface Drawable {root:Container;body:Graphics;sprite:Sprite|null;asset:string}
/**
 * Platform-neutral Pixi stage view; coordinates use the C# -30..30 stage domain.
 * Only the resolver owns art assets. Missing assets are VISIBLE and logged: never
 * pretend a primitive stand-in is the original portrait.
 */
export class StageVectorView {
  readonly root=new Container();
  private readonly drawings=new Map<string,Drawable>();
  private readonly missing=new Set<string>();
  private readonly backdrop=new Graphics();
  private readonly layers={back:new Container(),middle:new Container(),front:new Container()};
  constructor(private readonly resolve:StageAssetResolver=()=>null){
    this.root.visible=false;
    this.root.addChild(this.backdrop,this.layers.back,this.layers.middle,this.layers.front);
  }
  mount():void{
    this.reset();
    this.root.visible=true;
    this.backdrop.rect(0,0,1024,576).fill({color:0x07080c,alpha:.97});
    this.backdrop.moveTo(0,490).lineTo(1024,490).stroke({color:0x68636a,alpha:.6,width:2});
  }
  private create(entity:StageEntity):Drawable {
    const root=new Container(),body=new Graphics();
    root.addChild(body);
    const texture=this.resolve(entity.asset);
    let sprite:Sprite|null=null;
    if(texture){
      sprite=new Sprite(texture);
      sprite.anchor.set(.5,1);
      sprite.width=entity.kind==="actor"?220:95;
      sprite.height=entity.kind==="actor"?370:80;
      root.addChild(sprite);
    }else{
      this.missing.add(entity.asset);
      if(entity.kind==="actor"){
        body.circle(0,-275,25).stroke({color:0xc5c1b7,width:4});
        body.moveTo(-34,-245).lineTo(-85,-32).lineTo(85,-32).lineTo(34,-245)
          .closePath().stroke({color:0xb9b3a6,width:3});
      }else{
        body.roundRect(-42,-68,84,65,6).fill({color:0x353842,alpha:.8})
          .stroke({color:0xab9e88,width:2});
      }
      const label=new Text({text:entity.id+"\n缺少："+entity.asset.split("/").at(-1),
        style:{fontSize:15,fill:0xf0c8a8,align:"center",wordWrap:true,wordWrapWidth:205}});
      label.anchor.set(.5,1);label.y=entity.kind==="actor"?-295:-78;
      root.addChild(label);
    }
    this.layers[entity.layer].addChild(root);
    return {root,body,sprite,asset:entity.asset};
  }
  sync(player:StagePlayback):void{
    const living=new Set(player.entities.keys());
    for(const [id,drawable] of this.drawings)if(!living.has(id)){
      drawable.root.destroy({children:true});this.drawings.delete(id);
    }
    for(const entity of player.entities.values()){
      let drawable=this.drawings.get(entity.id);
      if(!drawable){drawable=this.create(entity);this.drawings.set(entity.id,drawable);}
      else if(!drawable.sprite&&this.resolve(entity.asset)){
        // The exact original image may finish decoding after a stage begins.
        // Replace the missing-asset diagnostic when it actually becomes usable.
        drawable.root.destroy({children:true});
        this.drawings.delete(entity.id);
        this.missing.delete(entity.asset);
        drawable=this.create(entity);
        this.drawings.set(entity.id,drawable);
      }
      const x=512+entity.x*26,y=481-entity.y*26;
      drawable.root.position.set(x,y);
      drawable.root.alpha=entity.light==="faint"?.42:entity.light==="ember"?.66:1;
      if(entity.pose!=="基础"&&entity.kind==="actor"){
        const asset=entity.asset+"_"+entity.pose;
        if(drawable.asset!==asset){
          const texture=this.resolve(asset);
          if(drawable.sprite&&texture){drawable.sprite.texture=texture;drawable.asset=asset;}
          else this.missing.add(asset);
        }
      }
    }
  }
  missingAssets():string[]{return [...this.missing];}
  reset():void{
    for(const d of this.drawings.values())d.root.destroy({children:true});
    this.drawings.clear();this.missing.clear();this.backdrop.clear();
    this.root.visible=false;
  }
  destroy():void{this.reset();this.root.destroy({children:true});}
}
