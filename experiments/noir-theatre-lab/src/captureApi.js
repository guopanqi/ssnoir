import { SHOTS } from './shots.js';

export function installCaptureApi({world, post, applyShot, camera, controls, render}) {
  let shotIndex=0;
  function setMode(name){
    post.setMode(name);
    world.setLinesVisible(name==='line' || name==='final');
    render();
    return name;
  }
  window.__noirTheatreLab = {
    ready:true,
    shots:SHOTS.map(s=>s.name),
    setMode,
    setShot(index){ shotIndex=index; const s=applyShot(index); render(); return s; },
    render,
    info(){ return {shotIndex, camera:{position:camera.position.toArray(),target:controls.target.toArray(),fov:camera.fov}}; },
  };
}
