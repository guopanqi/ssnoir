import * as T from 'three';
import {profile} from './profile.js';
// 以线条几何的屏幕尺寸决定是否保留，不按设备或固定镜头做隐藏分支。
export function lineHierarchy(scene){
 scene.updateMatrixWorld(true);
 const entries=[];
 scene.traverse(o=>{if(!o.isLine)return;o.geometry.computeBoundingSphere();const sphere=o.geometry.boundingSphere.clone().applyMatrix4(o.matrixWorld);entries.push({o,sphere,threshold:o.userData.minPixels??profile.detailPixels});});
 const center=new T.Vector3();
 return (camera,height,enabled,reduced)=>{
  const focal=height/(2*Math.tan(T.MathUtils.degToRad(camera.fov/2)));
  for(const {o,sphere,threshold} of entries){center.copy(sphere.center);const pixels=2*sphere.radius*focal/Math.max(.1,camera.position.distanceTo(center));o.visible=enabled&&(!reduced||pixels>=threshold);}
 };
}
