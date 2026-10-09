import sharp from '../portrait-stage-lab/node_modules/sharp/lib/index.js';
import fs from 'node:fs/promises';
for(const [actor,name] of [['neil','尼尔'],['night','夜莺']]){
 const folder='experiments/portrait-stage/line-stage-study/assets/original-neon';await fs.mkdir(folder,{recursive:true});
 const source=actor==='night'?'experiments/portrait-stage/line-stage-study/art/night-neon-no-microphone/35b508da-500c-407c-a53f-8120aa5ad885/image.png':`UnityClient/Assets/Resources/Portraits/Neon/${name}.png`;
 const {data,info}=await sharp(source).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 let left=info.width,right=0,top=info.height,bottom=0;
 for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){
  const i=(y*info.width+x)*4,m=Math.max(data[i],data[i+1],data[i+2]);
  if(m>20){left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y)}
  // Black-backed neon is additive light: unpremultiply into transparent pixels.
  data[i+3]=m;for(let c=0;c<3;c++)data[i+c]=m?Math.round(data[i+c]*255/m):0;
 }
 const silhouette=actor==='night'
 ? 'M413 68C459 52 501 80 526 98C555 70 622 74 646 118C675 163 661 196 637 230C637 260 617 278 599 295C625 318 636 346 633 386L607 435L609 543L609 640L605 752L597 865L594 1024H443L414 982L411 856L394 735L391 666L380 555L380 470L379 384C379 342 383 320 399 299C367 296 337 283 334 249C329 211 349 181 375 152C384 130 389 94 413 68Z M578 354L601 367L599 395L582 413L574 391Z'
 : 'M490 61C537 49 591 61 618 89L646 109L638 147L613 158L612 201L578 249L591 290L626 314L651 356L689 401L746 421L763 455L743 496L693 499L648 486L635 600L621 726L619 1024H482L479 782L466 665L436 605L416 539L365 489L356 421L367 349L390 293L453 250L475 192L477 148L472 105Z';
 await fs.writeFile(`${folder}/${actor}-silhouette.svg`,`<svg xmlns="http://www.w3.org/2000/svg" viewBox="${left} ${top} ${right-left+1} ${bottom-top+1}"><path fill="white" fill-rule="evenodd" d="${silhouette}"/></svg>`);
 await sharp(data,{raw:info}).extract({left,top,width:right-left+1,height:bottom-top+1}).png().toFile(`${folder}/${actor}-neutral.png`);
}
