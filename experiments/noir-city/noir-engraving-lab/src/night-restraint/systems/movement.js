import * as T from 'three';
import {mulberry32} from '../scene/referenceCity.js';

function path(points,height){
 const vertices=points.map(([x,z])=>new T.Vector3(x,height,z));
 const lengths=[0];
 for(let i=1;i<vertices.length;i++)lengths.push(lengths[i-1]+vertices[i].distanceTo(vertices[i-1]));
 if(lengths.at(-1)<=0)throw new Error('移动路径长度必须大于零');
 return {vertices,lengths,total:lengths.at(-1)};
}
function sample(route,distance,target){
 const d=T.MathUtils.clamp(distance,0,route.total);let i=1;
 while(i<route.lengths.length-1&&route.lengths[i]<d)i++;
 return target.copy(route.vertices[i-1]).lerp(route.vertices[i],(d-route.lengths[i-1])/(route.lengths[i]-route.lengths[i-1]));
}
// A world-space ribbon follows the route, including corners. Length is measured in world units.
function ribbon(segments,width,color){
 const geometry=new T.BufferGeometry();
 geometry.setAttribute('position',new T.Float32BufferAttribute(new Float32Array((segments+1)*2*3),3));
 const colors=[];const indices=[];
 for(let i=0;i<=segments;i++){
  const fade=(1-i/segments)**1.8;
  for(let side=0;side<2;side++)colors.push(color.r*fade,color.g*fade,color.b*fade);
  if(i<segments){const k=i*2;indices.push(k,k+1,k+2,k+1,k+3,k+2);}
 }
 geometry.setAttribute('color',new T.Float32BufferAttribute(colors,3));geometry.setIndex(indices);
 const mesh=new T.Mesh(geometry,new T.MeshBasicMaterial({vertexColors:true,transparent:true,opacity:.6,side:T.DoubleSide,depthWrite:false,blending:T.AdditiveBlending}));mesh.frustumCulled=false;
 const pos=new T.Vector3(),ahead=new T.Vector3(),dir=new T.Vector3();
 return {mesh,update(route,d,sign,length,gain){
  const a=geometry.attributes.position;
  for(let i=0;i<=segments;i++){
   const distance=d-sign*(2+i/segments*length);sample(route,distance,pos);sample(route,distance+sign*.6,ahead);
   dir.copy(ahead).sub(pos).normalize();
   a.setXYZ(i*2,pos.x-dir.z*width,pos.y+.04,pos.z+dir.x*width);
   a.setXYZ(i*2+1,pos.x+dir.z*width,pos.y+.04,pos.z-dir.x*width);
  }
  a.needsUpdate=true;mesh.material.opacity=gain;
 }};
}
export function buildMovement(scene,roads,riverPoints,P){
 const rnd=mulberry32(7311),routes=roads.filter(r=>r.points.length>3).map(r=>path(r.points.map(([x,z])=>[x,-z]),1.2));
 if(!routes.length)throw new Error('没有车流道路');
 const cars=[],boats=[];
 const bodyMat=new T.MeshStandardMaterial({color:'#65717e',roughness:.55});
 const bodyGeo=new T.BoxGeometry(3.8,1.4,1.7),roofGeo=new T.BoxGeometry(2.1,.6,1.45);
 for(let i=0;i<80;i++){
  const group=new T.Group();group.add(new T.Mesh(bodyGeo,bodyMat));
  const roof=new T.Mesh(roofGeo,bodyMat);roof.position.y=1;group.add(roof);
  const tailMat=new T.MeshBasicMaterial({color:'#c7946a'});
  for(const z of [-.55,.55]){
   const h=new T.Mesh(new T.BoxGeometry(.25,.3,.3),new T.MeshBasicMaterial({color:new T.Color('#fff0cf').multiplyScalar(2)}));h.position.set(1.9,.1,z);group.add(h);
   const tail=new T.Mesh(new T.BoxGeometry(.23,.25,.25),tailMat);tail.position.set(-1.9,.1,z);group.add(tail);
  }
  const route=routes[Math.floor(rnd()*routes.length)],trail=ribbon(12,.4,new T.Color('#c7b58b'));
  scene.add(group,trail.mesh);cars.push({group,route,trail,tailMat,d:rnd()*route.total,sign:rnd()<.5?1:-1,speed:9+rnd()*10});
 }
 const waterPath=path(riverPoints,-.6);
 const hullShape=new T.Shape();hullShape.moveTo(-3,-11);hullShape.lineTo(3,-11);hullShape.lineTo(4,5);hullShape.lineTo(0,13);hullShape.lineTo(-4,5);hullShape.closePath();
 const hullGeo=new T.ExtrudeGeometry(hullShape,{depth:2,bevelEnabled:false});hullGeo.rotateX(Math.PI/2);
 for(let i=0;i<6;i++){
  const group=new T.Group();group.add(new T.Mesh(hullGeo,bodyMat));
  const cabin=new T.Mesh(new T.BoxGeometry(4,3,6),new T.MeshStandardMaterial({color:'#9a9b90'}));cabin.position.set(0,2,-2);group.add(cabin);
  const window=new T.Mesh(new T.BoxGeometry(4.1,.7,2),new T.MeshBasicMaterial({color:new T.Color('#e4c58b').multiplyScalar(1.3)}));window.position.set(0,2.4,-2);group.add(window);
  const bowLight=new T.Mesh(new T.SphereGeometry(.4,6,4),new T.MeshBasicMaterial({color:'#cfdee0'}));bowLight.position.set(0,1,8);group.add(bowLight);
  const wake=ribbon(18,2.5,new T.Color('#567787'));wake.mesh.position.y=-1.9;scene.add(group,wake.mesh);
  boats.push({group,route:waterPath,trail:wake,d:waterPath.total*(i+.3)/6,sign:i%2?1:-1,speed:5+i*.7});
 }
 const forward=new T.Vector3();
 function move(item,dt,speed,length,gain,isBoat){
  item.d+=dt*item.speed*speed*item.sign;
  if(item.d>item.route.total){item.d=item.route.total;item.sign=-1;}
  if(item.d<0){item.d=0;item.sign=1;}
  sample(item.route,item.d,item.group.position);sample(item.route,item.d+item.sign*.8,forward);
  forward.sub(item.group.position);
  item.group.rotation.y=isBoat?Math.atan2(forward.x,forward.z):-Math.atan2(forward.z,forward.x);
  item.trail.update(item.route,item.d,item.sign,length,gain);
 }
 return {tick(dt){
  cars.forEach((c,i)=>{c.group.visible=c.trail.mesh.visible=i<P.cars;if(i>=P.cars)return;c.tailMat.color.set('#c6ad81').lerp(new T.Color('#e25542'),P.tailRed);move(c,dt,P.carSpeed,P.trailLength,P.trailGain,false);});
  boats.forEach((b,i)=>{b.group.visible=b.trail.mesh.visible=i<P.boats;if(i<P.boats)move(b,dt,P.boatSpeed,40,P.wakeGain,true);});
 }};
}
