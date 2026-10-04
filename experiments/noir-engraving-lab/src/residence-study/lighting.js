import * as T from 'three';
export function lightResidence(scene,world){
 scene.add(new T.HemisphereLight(0xc4d9eb,0x11151e,world.kind==='room'?.85:1.1));
 const key=new T.DirectionalLight(0xdcecff,world.kind==='room'?3.2:3.5);key.position.set(...(world.kind==='room'?[-1,6,7]:[-12,19,-10]));key.castShadow=true;key.shadow.mapSize.set(2048,2048);const span=world.kind==='room'?9:24;Object.assign(key.shadow.camera,{left:-span,right:span,top:span,bottom:-span,near:.1,far:90});key.shadow.bias=-.0002;key.shadow.normalBias=.025;scene.add(key);
 const practical=new T.PointLight(world.kind==='room'?0xffe5be:0xd5e8ff,world.kind==='room'?14:45,world.kind==='room'?5:10,2);practical.position.set(...world.lamp);scene.add(practical);
 const spill=new T.SpotLight(0xcbe4ff,world.kind==='room'?60:25,world.kind==='room'?12:7,Math.PI/5,.35,2);spill.position.set(...world.window);spill.target.position.set(...(world.kind==='room'?[-1,0,-1]:[1,0,-4]));spill.castShadow=true;scene.add(spill,spill.target);
 const fill=new T.DirectionalLight(0x91a9bf,.5);fill.position.set(-7,6,-7);scene.add(fill);
 if(world.kind==='exterior')scene.fog=new T.Fog(0x1a2433,30,85);
}
