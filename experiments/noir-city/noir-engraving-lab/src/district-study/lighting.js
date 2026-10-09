import * as T from 'three';
import {profile as P} from './profile.js';
export function lightDistrict(scene,key){
 key.position.set(-65,85,-65);Object.assign(key.shadow.camera,{left:-105,right:105,top:105,bottom:-105,near:1,far:240});key.shadow.camera.updateProjectionMatrix();key.shadow.mapSize.set(4096,4096);key.shadow.normalBias=.12;
 scene.fog=new T.Fog(P.background,P.fogNear,P.fogFar);
 for(const [x,z] of [[-7,-44],[7,44],[-32,-10],[32,20]]){const light=new T.PointLight(0xd5e8ff,42,12,2);light.position.set(x,4,z);scene.add(light);}
}
