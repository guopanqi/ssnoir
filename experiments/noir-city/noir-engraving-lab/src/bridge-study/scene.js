import * as T from 'three';

// 独立研究场景：桥、两岸、两处建筑主题。尺寸以构图为准，不替换游戏地点。
export function buildBridge(scene,{bankLength=53,groundSize=160,skyline=true}={}) {
  const ink = new T.LineBasicMaterial({color:0xc7d3dd,transparent:true,opacity:.78});
  const secondary = new T.LineBasicMaterial({color:0x66798e,transparent:true,opacity:.5});
  const pale = new T.MeshStandardMaterial({color:0x8999a8,roughness:.93});
  const dark = new T.MeshStandardMaterial({color:0x202b37,roughness:1});
  const road = new T.MeshStandardMaterial({color:0x526475,roughness:.8});
  const glass = new T.MeshBasicMaterial({color:0xc8d7dd});
  const lines = new T.Group(); lines.name='轮廓层'; scene.add(lines);
  function mesh(geometry, material, x,y,z, outlined=false) {
    const o = new T.Mesh(geometry,material);o.position.set(x,y,z);
    o.castShadow=true;o.receiveShadow=true;scene.add(o);
    if (outlined) {const e=new T.LineSegments(new T.EdgesGeometry(geometry,40),outlined==='dim'?secondary:ink);e.scale.setScalar(1.003);o.add(e);}
    return o;
  }
  function box(w,h,d,x,y,z,material=dark,outlined=false) {
    return mesh(new T.BoxGeometry(w,h,d),material,x,y+h/2,z,outlined);
  }
  function stroke(points,material=ink) {
    const o=new T.Line(new T.BufferGeometry().setFromPoints(points.map(p=>new T.Vector3(...p))),material);
    lines.add(o);return o;
  }
  function arch(x,y,z,width,height,material=ink,along='z') {
    const points=[];
    for(let i=0;i<=24;i++){const a=Math.PI*i/24;const v=Math.cos(a)*width/2;points.push(along==='z'?[x,y+Math.sin(a)*height,z+v]:[x+v,y+Math.sin(a)*height,z]);}
    stroke(points,material);
  }
  // 大块河面与连续步道，避免住宅方块铺满整个画面。
  mesh(new T.PlaneGeometry(groundSize,groundSize),new T.MeshStandardMaterial({color:0x182431,roughness:1}),0,-1,0).rotation.x=-Math.PI/2;
  box(19,.7,bankLength,-15,-.7,0,road);
  box(19,.7,bankLength,15,-.7,0,road);
  mesh(new T.PlaneGeometry(11,bankLength+12),new T.MeshBasicMaterial({color:0x1c2f40}),0,-.25,0).rotation.x=-Math.PI/2;
  for(const x of [-5.5,5.5]){
    box(.45,1,bankLength,x,-.65,0,pale);
    stroke([[x,.36,-bankLength/2],[x,.36,bankLength/2]],secondary);
  }
  // 桥用拱与石墩建立独特剪影；桥面与栏杆分别读成明面和细线。
  box(17,.4,4.4,0,1.8,0,pale,true);
  for(const z of [-2.2,2.2]) {
    stroke([[-8.5,3,z],[8.5,3,z]]);
    stroke([[-8.5,2.3,z],[8.5,2.3,z]],secondary);
    for(let x=-8;x<=8;x+=1)stroke([[x,2.3,z],[x,3,z]],secondary);
    for(const x of [-6.8,6.8])box(1.4,2.2,.9,x,0,z,pale,true);
    const span=new T.Shape();span.moveTo(-6,0);span.lineTo(-6,1.8);span.lineTo(6,1.8);span.lineTo(6,0);span.lineTo(5.2,0);
    for(let i=0;i<=32;i++){const a=Math.PI*i/32;span.lineTo(Math.cos(a)*5.2,Math.sin(a)*1.45);}
    span.lineTo(-6,0);
    mesh(new T.ExtrudeGeometry(span,{depth:.35,bevelEnabled:false}),dark,0,0,z-.18,true);
  }
  const towerStart=scene.children.length, towerLines=lines.children.length;
  // 左岸：退台钟楼与拱廊。用少数大结构取代方盒替身。
  box(7,8.5,8,-13,0,-8,dark,true);
  box(7.4,.4,8.4,-13,8.5,-8,pale,true);
  box(5.5,3,6.3,-13,8.9,-8,dark,true);
  box(5.8,.4,6.6,-13,11.9,-8,pale,true);
  box(3.2,3.5,3.6,-13,12.3,-8,dark,true);
  box(3.6,.5,4,-13,15.8,-8,pale,true);
  const crown=mesh(new T.ConeGeometry(2.45,2.7,4),pale,-13,17.6,-8,true);crown.rotation.y=Math.PI/4;
  // 钟面朝向相机的南侧；有轮廓与指针，不加小字。
  mesh(new T.CircleGeometry(.9,48),glass,-13,14.2,-9.82).rotation.y=Math.PI;
  const clockInk=new T.MeshBasicMaterial({color:0x111a23});
  box(.12,.65,.05,-13,14.15,-9.9,clockInk);
  box(.5,.12,.05,-12.78,14.1,-9.9,clockInk);
  for(let z=-11;z<=-5;z+=2){
    box(.25,3,.35,-9.4,0,z,pale,true);
    arch(-9.38,2.2,z,1.6,.75,ink);
  }
  box(3,.35,9,-9.4,3.0,-8,pale,true);
  for(let row=0;row<2;row++)for(let i=0;i<4;i++){
    box(.68,1.15,.05,-15.6+i*1.75,4.7+row*2,-12.04,glass);
  }
  for(const o of scene.children.slice(towerStart))o.position.z+=18;
  for(const o of lines.children.slice(towerLines))o.position.z+=18;
  const hallStart=scene.children.length,hallLines=lines.children.length;
  // 右岸：圆角大厅、立柱、扇形屋顶。长短/高低与钟楼相互制衡。
  box(8,5.5,10,12,0,8,dark,true);
  box(9,.4,11,12,5.5,8,pale,true);
  const dome=mesh(new T.SphereGeometry(4.2,32,16,0,Math.PI*2,0,Math.PI/2),dark,12,5.9,8);
  const rim=[];for(let i=0;i<=64;i++){const a=i/64*Math.PI*2;rim.push([12+4.25*Math.cos(a),5.9,8+4.25*Math.sin(a)]);}stroke(rim);
  arch(12,5.9,8,8.4,4.2,ink,'x');
  for(let z=4;z<=12;z+=2){box(.4,4,.4,7.7,0,z,pale,true);arch(7.65,3,z,1.5,.8);}
  box(3,.4,11,7.9,4,8,pale,true);
  for(const o of scene.children.slice(hallStart))o.position.z-=17;
  for(const o of lines.children.slice(hallLines))o.position.z-=17;
  // 背景街墙只保留少量连续屋顶，不把每栋房子的十二条棱全部描亮。
  for(const [x,z,w,h,d] of [[-20,4,6,5,8],[-19,14,7,7,8],[-19,-20,6,6,7],[18,-15,8,9,9],[18,-25,7,12,7],[22,24,6,10,7]]){
    box(w,h,d,x,0,z,dark);
    // 大尺度立面节奏：立柱与窗带，避免只剩立方体外框。
    for(const dx of [-w/2+.5,0,w/2-.5])box(.16,h-.6,.18,x+dx,.2,z-d/2-.07,dark,true);
    box(w,.18,.25,x,h*.55,z-d/2-.08,dark,'dim');
    box(w+.2,.3,d+.2,x,h,z,dark,'dim');
    for(let j=0;j<3;j++)box(.6,1,.03,x+(j-1)*1.8,Math.min(h-2,5),z-d/2-.02,new T.MeshBasicMaterial({color:0x657686}));
  }
  // 更远的城市读作叠置剪影。
  if(skyline)for(let i=0;i<9;i++)box(3+(i%3),7+(i*7%11),3,-22+i*5,0,34,dark);
  // 河面反光是断续长短笔画，避免均匀平行条纹墙。
  for(let i=0;i<50;i++){
    const z=-24+i; const x=Math.sin(i*7.3)*3.5; const len=.45+(i%5)*.34;
    stroke([[x-len/2,-.18,z],[x+len/2,-.18,z]],new T.LineBasicMaterial({color:0x8096a7,transparent:true,opacity:i%3===0?.5:.17}));
  }
  // 街灯：明确的灯头和真实受光，没有实体光锥。
  for(const [x,z] of [[-7,-15],[-7,7],[7,-7],[7,18]]) {
    box(.12,3.7,.12,x,0,z,pale,'dim');
    box(.6,.2,.6,x,3.7,z,glass);
    const lamp=new T.PointLight(0xd5e8ff,35,9,2);lamp.position.set(x,3.5,z);scene.add(lamp);
  }
  const hemi=new T.HemisphereLight(0xc4d9eb,0x11151e,1.1);scene.add(hemi);
  const key=new T.DirectionalLight(0xe2eeff,4.2);key.position.set(-18,25,-18);key.castShadow=true;
  key.shadow.mapSize.set(2048,2048);Object.assign(key.shadow.camera,{left:-35,right:35,top:35,bottom:-35,near:1,far:100});key.shadow.bias=-.0004;key.shadow.normalBias=.04;scene.add(key);
  const fill=new T.DirectionalLight(0x849ab3,.6);fill.position.set(20,15,20);scene.add(fill);
  scene.fog=new T.Fog(0x1a2433,55,115);
  return {lines,key};
}
