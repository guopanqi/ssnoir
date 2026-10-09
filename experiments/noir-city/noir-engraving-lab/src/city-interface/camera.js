import * as T from 'three';
import {shots} from '../district-study/shots.js';
export function cityCamera(camera){
 const world=shots.world,target=new T.Vector3(...world.target);camera.position.set(...world.position);camera.lookAt(target);
 let animation=null;
 function go(location){const destination=location||world;animation={start:performance.now(),from:camera.position.clone(),fromTarget:target.clone(),to:new T.Vector3(...destination.position),toTarget:new T.Vector3(...destination.target)};}
 function update(now){if(!animation)return;const t=Math.min(1,(now-animation.start)/1150),e=t*t*(3-2*t);camera.position.lerpVectors(animation.from,animation.to,e);target.lerpVectors(animation.fromTarget,animation.toTarget,e);camera.lookAt(target);if(t===1)animation=null;}
 return {go,update,get moving(){return animation!==null;}};
}
