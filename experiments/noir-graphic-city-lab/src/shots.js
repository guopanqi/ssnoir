export const SHOTS=Object.freeze([
  Object.freeze({
    id:'01-city-plaza',
    name:'01 · CITY PLAZA',
    position:[9.5,4.4,41],
    target:[-3.7,4.1,-13.2],
    fov:42,
    purpose:'第一审核镜头：侦探前景、暗蓝城市、密集亮窗、人群、湿地与探照灯组成一个干净而精致的 Genesis Noir 风格城市舞台。',
  }),
  Object.freeze({
    id:'02-city-plaza-offset',
    name:'02 · CITY PLAZA OFFSET',
    position:[16.5,5.8,26],
    target:[-4.0,4.2,-10],
    fov:40,
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
