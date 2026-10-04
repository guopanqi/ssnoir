import * as T from 'three';
import {buildBridge} from '../bridge-study/scene.js';
import {profile as P} from './profile.js';

export function buildDistrict(scene){
 const core=buildBridge(scene,{bankLength:170,groundSize:600,skyline:false});
 const mat={body:new T.MeshStandardMaterial({color:P.body,roughness:1}),roof:new T.MeshStandardMaterial({color:P.roof,roughness:.95}),pale:new T.MeshStandardMaterial({color:P.pale,roughness:1}),street:new T.MeshStandardMaterial({color:P.street,roughness:.85}),window:new T.MeshBasicMaterial({color:0xa2b5c4})};
 const roofHeights=new Map();
 const line=new T.LineBasicMaterial({color:P.line,transparent:true,opacity:P.lineOpacity});
 function mesh(g,m,x,y,z,edge=false){const o=new T.Mesh(g,mat[m]);o.position.set(x,y,z);o.castShadow=true;o.receiveShadow=true;scene.add(o);if(edge){const l=new T.LineSegments(new T.EdgesGeometry(g,35),line);l.scale.setScalar(1.003);l.userData.minPixels=12;o.add(l);}return o;}
 function box(w,h,d,x,y,z,m='body',edge=false){return mesh(new T.BoxGeometry(w,h,d),m,x,y+h/2,z,edge);}
 function stroke(points,minPixels=12){const l=new T.Line(new T.BufferGeometry().setFromPoints(points.map(p=>new T.Vector3(...p))),line);l.userData.minPixels=minPixels;scene.add(l);}
 function building(x,z,w,d,h,index){
  roofHeights.set(`${x},${z}`,h);box(w,h,d,x,0,z);
  if(index%4===0){box(w*.4,h*.6,d*.35,x+w*.6,0,z+d*.28);box(w*.45,.2,d*.38,x+w*.6,h*.6,z+d*.28,'roof',true);}
  // 普通建筑主要描屋顶和少量檐口，地标才用完整结构线。
  box(w+.3,.25,d+.3,x,h,z,'roof',true);
  if(index%3===0){const roof=mesh(new T.CylinderGeometry(w*.52,w*.52,d,4),'roof',x,h+1.2,z,true);roof.rotation.x=Math.PI/2;roof.scale.z=.35;}
  else if(index%3===1){box(w*.68,1.5,d*.6,x,h+.25,z,'body');box(w*.73,.18,d*.65,x,h+1.75,z,'roof',true);}
  if(index%2===0)stroke([[x-w/2,h*.5,z-d/2-.06],[x+w/2,h*.5,z-d/2-.06]],16);
  for(let row=0;row<Math.min(3,Math.floor(h/3));row++){
   for(let col=0;col<3;col++){
    const xx=x+(col-1)*w*.25,yy=2+row*3;
    if((index+row*3+col)%7===0)box(.6,1,.04,xx,yy,z-d/2-.03,'window');
    else stroke([[xx-.35,yy,z-d/2-.045],[xx-.35,yy+1,z-d/2-.045],[xx+.35,yy+1,z-d/2-.045]],10);
   }
  }
 }
 // 街道连续通向桥头，街区内部留院落。两岸使用不同的高度节奏。
 for(const side of [-1,1]){
  box(42,.08,170,side*46,-.1,0,'street');
  for(const z of [-57,-24,10,44,78])box(65,.1,3.2,side*39,-.02,z,'roof');
  let index=side===-1?0:13;
  for(const z of [-40,-7,27,61])for(const x of [side*33,side*56]){
   if(z===61)continue; // 最后一排留给两岸地标，避免模型互相穿插。
   const h=side===-1?7+(index*5%9):9+(index*7%13);
   building(x,z,14+(index%4),23,h,index++);
  }
  // 沿河少量低矮门面让边界连续，不围住桥头地标。
  for(const z of [-50,-35,43,61])building(side*15,z,8,9,4+(z%3+3)%3,index++);
 }
 // 上游第二座低桥只读作跨河亮面，服从主桥。
 for(const z of [-57,44]){box(16,.45,3,0,.8,z,'roof',true);for(const x of [-6.5,6.5])box(1.3,1,3,x,-.2,z,'body');}
 // 西岸的阶梯办公楼：长距离也能辨认的垂直地标。
 box(12,19,12,-43,0,57,'body',true);box(12.5,.45,12.5,-43,19,57,'pale',true);
 box(8,7,8,-43,19.45,57,'body',true);box(8.4,.35,8.4,-43,26.45,57,'pale',true);
 box(4,6,4,-43,26.8,57,'body',true);box(4.4,.3,4.4,-43,32.8,57,'pale',true);
 for(const x of [-46,-43,-40])stroke([[x,3,50.95],[x,17,50.95]],20);
 // 东岸长仓库：与尖高地标相对照的横向锯齿屋顶。
 box(22,6,15,46,0,53,'body');
 for(let i=0;i<5;i++){
  const x=35+i*4.4;
  const shape=new T.Shape();shape.moveTo(0,0);shape.lineTo(4.4,0);shape.lineTo(4.4,2);shape.closePath();
  mesh(new T.ExtrudeGeometry(shape,{depth:15,bevelEnabled:false}),'roof',x,6,45.5,true);
 }
 // 水塔与屋顶烟囱给住宅街区一个不同的轮廓。
 for(const [x,z] of [[56,-40],[-56,27]]){
  const base=roofHeights.get(`${x},${z}`);if(base===undefined)throw new Error('Water tower requires roof');
  for(const dx of [-1.5,1.5])for(const dz of [-1.5,1.5])box(.14,5,.14,x+dx,base+.25,z+dz,'roof');
  mesh(new T.CylinderGeometry(2.2,2.2,4,24),'body',x,base+7.25,z,true);
  mesh(new T.ConeGeometry(2.5,1.5,24),'pale',x,base+10,z);
 }
 for(const [x,z] of [[-7,-44],[7,44],[-32,-10],[32,20]]){box(.15,4,.15,x,0,z,'roof');box(.6,.16,.6,x,4,z,'window');}
 // 延长河面上的断续反光，密度不随城市面积膨胀。
 for(let i=0;i<35;i++){const z=-80+i*4.7;if(Math.abs(z)<26)continue;const x=Math.sin(i*4.1)*3.6;stroke([[x-.6,-.18,z],[x+.6,-.18,z]],7);}
 for(let i=0;i<15;i++){const x=-82+i*12;const h=10+(i*11%17);box(8+(i%3),h,11,x,0,101+(i%3)*6,'body');if(i%4===0)box(5,4,6,x,h,101+(i%3)*6,'roof');}
 return core;
}
