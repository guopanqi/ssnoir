import {Texture} from "pixi.js";

// Explicit, bounded set of existing SSNoir original portraits. ?inline keeps
// the same self-contained JS artifact on Web, WeChat and TapTap. Never import
// an entire Unity Resources directory into the 4 MiB Mini Game main package.
import neil from "../../../UnityClient/Assets/Resources/Portraits/Neon/尼尔_silhouette.png?inline";
import nightingale from "../../../UnityClient/Assets/Resources/Portraits/Neon/夜莺_silhouette.png?inline";
import frank from "../../../UnityClient/Assets/Resources/Portraits/Neon/弗兰克_silhouette.png?inline";
import baines from "../../../UnityClient/Assets/Resources/Portraits/Neon/贝恩斯_silhouette.png?inline";

const atlas:Record<string,string>={
  "尼尔":neil,"夜莺":nightingale,"弗兰克":frank,"贝恩斯":baines
};
/**
 * Optional image layer on top of the already-functional authored StagePlayback.
 * Uses the same Image adapter as the Mini Game host. Failed loading never
 * stops actions or dialogue; the stage renderer reports missing art clearly.
 */
export class StageImageRegistry {
  private readonly textures=new Map<string,Texture>();
  private readonly errors=new Map<string,string>();
  private readonly pending=new Set<string>();
  constructor(){
    for(const [name,url] of Object.entries(atlas)){
      this.pending.add(name);
      try{
        const image=new Image();
        image.onload=()=>{
          try {this.textures.set(name,Texture.from(image));this.errors.delete(name);}
          catch(error){this.errors.set(name,String(error));}
          this.pending.delete(name);
        };
        image.onerror=()=>{this.errors.set(name,"image decode failed");this.pending.delete(name);};
        image.src=url;
      }catch(error){this.errors.set(name,String(error));this.pending.delete(name);}
    }
  }
  get(asset:string):Texture|null{return this.textures.get(asset)??null;}
  diagnostics():{loaded:number;pending:number;errors:string[]}{
    return {loaded:this.textures.size,pending:this.pending.size,
      errors:[...this.errors].map(([name,error])=>name+": "+error)};
  }
  dispose():void {
    for(const texture of this.textures.values())texture.destroy();
    this.textures.clear();
  }
}
