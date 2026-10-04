import * as T from 'three';
import {profile as P} from './profile.js';

function kit(scene){
 const materials={body:new T.MeshStandardMaterial({color:P.body,roughness:1}),stone:new T.MeshStandardMaterial({color:P.stone,roughness:.9}),floor:new T.MeshStandardMaterial({color:P.floor,roughness:.8}),white:new T.MeshStandardMaterial({color:0xa2adb4,roughness:1}),outside:new T.MeshBasicMaterial({color:0x7e95a4}),glow:new T.MeshBasicMaterial({color:P.light}),black:new T.MeshStandardMaterial({color:0x131e28,roughness:.6})};
 const lines={main:new T.LineBasicMaterial({color:P.line,transparent:true,opacity:P.lineOpacity}),dim:new T.LineBasicMaterial({color:P.dimLine,transparent:true,opacity:P.dimOpacity})};
 function mesh(g,m,pos,edge='main'){const o=new T.Mesh(g,materials[m]);o.position.set(...pos);o.castShadow=true;o.receiveShadow=true;scene.add(o);if(edge){const e=new T.LineSegments(new T.EdgesGeometry(g,35),lines[edge]);e.scale.setScalar(1.003);o.add(e);}return o;}
 function box(w,h,d,x,y,z,m='body',edge='main'){return mesh(new T.BoxGeometry(w,h,d),m,[x,y+h/2,z],edge);}
 function cyl(r,h,x,y,z,m='body',edge=false){return mesh(new T.CylinderGeometry(r,r,h,24),m,[x,y+h/2,z],edge);}
 function stroke(points,dim=false){const o=new T.Line(new T.BufferGeometry().setFromPoints(points.map(p=>new T.Vector3(...p))),lines[dim?'dim':'main']);scene.add(o);return o;}
 function tube(a,b,r=.035,m='stone'){const start=new T.Vector3(...a),end=new T.Vector3(...b),d=end.clone().sub(start);const o=mesh(new T.CylinderGeometry(r,r,d.length(),8),m,start.clone().add(end).multiplyScalar(.5).toArray(),false);o.quaternion.setFromUnitVectors(new T.Vector3(0,1,0),d.normalize());return o;}
 function window(x,y,z,w=1.1,h=1.65,lit=false){box(w+.22,h+.22,.15,x,y-.1,z,'stone','dim');box(w,h,.06,x,y,z-.1,lit?'glow':'black',false);box(.07,h,.09,x,y,z-.15,'stone');box(w,.065,.1,x,y+h*.55,z-.15,'stone');box(w+.35,.1,.35,x,y-.15,z-.05,'stone');}
 return {box,cyl,mesh,stroke,tube,window};
}

export function buildExterior(scene){
 const {box,cyl,mesh,stroke,tube,window}=kit(scene);
 box(60,.2,60,0,-.4,0,'floor',false);
 box(27,.25,7,0,-.1,-6.5,'body',false);
 box(23,.2,3.4,0,0,-3.4,'stone','dim');
 // 砖石公寓以门厅、凸窗和退台屋顶建立节奏，不逐块描砖。
 box(9,11,7,0,0,1.5,'body');
 box(9.5,.4,7.5,0,10.8,1.5,'stone');
 box(9,1.0,.25,0,11.1,-2.05,'body');
 for(const x of [-4.2,-.2,4.2])box(.18,10.5,.16,x,.1,-2.06,'stone','dim');
 for(const y of [3.6,6.5,9.4]){
  box(9,.14,.23,0,y,-2.1,'stone','dim');
  for(const x of [-3,-1,1,3]){if(y===3.6&&x===-1)continue;window(x,y-2.35,-2.12,1.1,1.65,(x===1&&y===6.5)||(x===-3&&y===9.4));}
 }
 // 凸窗：三面玻璃与窄檐形成可识别的住所表情。
 for(const y of [3.9,6.8,9.7]){box(1.8,2.1,.75,2.9,y-2.3,-2.42,'body');window(2.9,y-2.15,-2.82,1.25,1.5,y===6.8);box(2,.16,1,2.9,y-.25,-2.42,'stone');}
 for(let i=0;i<4;i++)box(2.4,.18,1.9-i*.35,-1,.18*i,-3.1+i*.17,'stone');
 box(1.7,2.7,.12,-1,.72,-2.1,'black');
 box(.1,2.7,.1,-1,.72,-2.19,'stone');box(1.7,.6,.09,-1,2.65,-2.2,'glow');
 const canopy=mesh(new T.CylinderGeometry(1.2,1.2,1.2,32,1,false,-Math.PI/2,Math.PI),'stone',[-1,3.4,-2.85]);canopy.rotation.x=-Math.PI/2;
 for(const x of [-2.1,.1]){cyl(.075,3.3,x,.3,-3.25,'stone');tube([x,.5,-4.2],[x,1.6,-2.4]);}
 // 侧面的消防梯靠轮廓读出，避免整面建筑都变成铁丝网。
 for(const y of [3.3,6.3,9.3]){
  box(1.3,.12,2.3,-5.1,y,1,'black','dim');
  stroke([[-5.8,y+1,0],[-5.8,y+1,2.1]]);
  for(const z of [0,.7,1.4,2.1])tube([-5.8,y,z],[-5.8,y+1,z],.018);
  tube([-5.8,y,2],[-5.8,y-2.8,.3],.025);tube([-4.8,y,2],[-4.8,y-2.8,.3],.025);
  for(let i=0;i<9;i++){const q=i/9;tube([-5.8,y-q*2.8,2-q*1.7],[-4.8,y-q*2.8,2-q*1.7],.018);}
 }
 // 邻屋轮廓退后，入口留出空间。
 for(const [x,h] of [[-9,7.8],[9,9]]){box(6,h,7,x,0,2,'body',false);box(6.3,.3,7.2,x,h,2,'stone','dim');for(let row=0;row<2;row++)for(const dx of [-1.7,0,1.7])window(x+dx,1.4+row*2.7,-1.55,1,1.5,false);}
 for(const z of [2,9,17])box(8,8+(z%5),6,-16,0,z,'body',false);
 // 长椅、街灯和电话线是少量生活线索。
 box(2.1,.12,.65,3.4,.65,-3.4,'stone');box(2.1,.55,.1,3.4,.8,-3.12,'body');for(const x of [2.6,4.2])box(.1,.65,.55,x,0,-3.4,'black');
 cyl(.06,4.4,-6.7,0,-3.9,'stone');tube([-6.7,4.35,-3.9],[-6,4.35,-3.9]);cyl(.28,.12,-6,4.2,-3.9,'glow');
 stroke([[-11,10.6,0],[-4,9.9,0],[5,10.1,0],[12,11,0]],true);
 return {kind:'exterior',lamp:[-6,4,-3.9],window:[1,5,-2.5]};
}

export function buildRoom(scene){
 const {box,cyl,mesh,stroke,tube}=kit(scene);
 box(6.4,.2,7.4,0,-.2,0,'floor');
 // 窗口真正留洞；近侧两面墙不建，方便检查空间。
 box(6.4,1.05,.2,0,0,3.6,'body','dim');box(6.4,.65,.2,0,2.6,3.6,'body','dim');
 for(const x of [-2.25,2.25])box(1.9,1.55,.2,x,1.05,3.6,'body','dim');
 box(.2,3.25,7.4,3.2,0,0,'body','dim');
 const pane=mesh(new T.PlaneGeometry(2.5,1.55),'outside',[0,1.825,3.72],false);pane.rotation.y=Math.PI;pane.castShadow=false;
 box(2.55,.1,.5,0,1.0,3.45,'stone');
 box(.09,1.55,.18,0,1.05,3.55,'stone');box(2.5,.07,.18,0,1.88,3.55,'stone');
 // 百叶窗只留数根横档，地板方向线保持弱。
 for(const y of [2.3,2.45,2.6])box(2.5,.035,.12,0,y,3.52,'stone','dim');
 for(let x=-2.8;x<3;x+=.6)stroke([[x,.013,-3.5],[x,.013,3.5]],true);
 // 铁床在后左；床单与枕头给出亮块，不模拟布料噪声。
 box(1.7,.35,2.7,-1.9,.35,1.5,'black');box(1.65,.22,2.65,-1.9,.7,1.5,'white');box(1.5,.13,1.35,-1.9,.91,.8,'stone','dim');box(1.05,.15,.48,-1.9,.94,2.43,'white');
 for(const z of [.1,2.9]){for(const x of [-2.8,-1])cyl(.045,1.35,x,0,z,'stone');tube([-2.8,1.3,z],[-1,1.3,z]);for(const x of [-2.5,-2.2,-1.9,-1.6,-1.3])tube([x,.55,z],[x,1.3,z],.022);}
 // 书桌、打字机、椅子：侦探生活空间的视觉中心。
 box(1.45,.15,2.1,2.24,1.05,.0,'stone');for(const x of [1.66,2.82])for(const z of [-.85,.85])box(.1,1.05,.1,x,0,z,'body');
 box(.45,.7,.75,2.65,.3,-.55,'body');box(.65,.14,.48,2.18,1.2,.35,'black');box(.63,.22,.12,2.18,1.33,.53,'body');box(.48,.35,.025,2.18,1.45,.58,'white',false);
 for(let row=0;row<2;row++)for(let i=0;i<6;i++)box(.055,.025,.045,1.93+i*.09,1.35,.2+row*.09,'stone',false);
 box(.42,.025,.65,2.2,1.205,-.5,'white',false);
 box(.65,.12,.65,1.15,.55,-.05,'body');for(const x of [.88,1.42])for(const z of [-.32,.22])box(.06,.55,.06,x,0,z,'stone','dim');box(.65,.65,.08,1.15,.65,-.37,'body');
 // 灯有灯罩和真实照明，室内允许小面积偏暖的阅读光。
 cyl(.3,.07,.8,0,-1.65,'black');cyl(.035,1.75,.8,.07,-1.65,'stone');mesh(new T.CylinderGeometry(.25,.43,.45,24,1,true),'white',[.8,1.95,-1.65],false);
 // 门边小几和烛台电话。
 box(.6,.1,.6,2.65,.8,-2.7,'stone');cyl(.07,.8,2.65,0,-2.7,'body');cyl(.17,.045,2.65,.9,-2.7,'black');cyl(.035,.33,2.65,.94,-2.7,'stone');mesh(new T.SphereGeometry(.095,16,8),'black',[2.65,1.31,-2.7],false);tube([2.65,1.15,-2.7],[2.43,1.15,-2.7],.025,'black');
 // 可添置物仅作视觉候选展示，没有冒充游戏存档状态。
 box(1,.75,.7,2.05,0,2.8,'body');cyl(.23,.045,2.05,.8,2.8,'black');tube([2.3,.8,2.9],[2.32,1.2,2.9],.035);const horn=mesh(new T.ConeGeometry(.33,.65,24,1,true),'stone',[2.1,1.38,2.85],false);horn.rotation.z=-Math.PI/3;
 cyl(.25,.38,-2.4,0,-.9,'stone');for(let i=0;i<5;i++){const x=-2.4+Math.sin(i*2)*.42,z=-.9+Math.cos(i*2)*.35,y=.85+i*.1;tube([-2.4,.35,-.9],[x,y,z],.018);const leaf=mesh(new T.SphereGeometry(.23,16,8),'stone',[x,y,z],false);leaf.scale.set(.6,1.6,.12);leaf.rotation.z=i*.7;}
 return {kind:'room',lamp:[.8,1.75,-1.65],window:[0,2.4,4.5]};
}
