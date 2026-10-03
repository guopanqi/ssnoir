export const SHOTS=Object.freeze([
  Object.freeze({
    id:'01-city-plaza',
    name:'01 · CITY PLAZA',
    position:[9.5,4.2,41],
    target:[-5.0,3.8,-13.2],
    fov:40,
    purpose:'第一审核镜头：侦探前景、暗蓝城市、密集亮窗、人群、湿地与探照灯组成一个干净而精致的 Genesis Noir 风格城市舞台。',
  }),
  Object.freeze({
    id:'02-city-plaza-offset',
    name:'02 · CITY PLAZA OFFSET',
    position:[-13.5,5.2,33],
    target:[-2.5,3.7,-12],
    fov:39,
    purpose:'鲁棒性镜头：同一套资产换一个机位，确认风格来自场景与渲染系统，而不是只为一张截图作弊。',
  }),
]);

export function applyShot(camera,controls,index){
  const shot=SHOTS[index];
  camera.fov=shot.fov;
  camera.updateProjectionMatrix();
  camera.position.fromArray(shot.position);
  controls.target.fromArray(shot.target);
  controls.update();
  return shot;
}
