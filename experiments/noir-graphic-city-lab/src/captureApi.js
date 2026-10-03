export function installCaptureApi({ profile, world, post, camera, controls, applyShot, render }) {
  const bloomStrength=profile.atmosphere.bloomStrength;

  const modes={
    structure(){
      world.groups.lines.visible=true;
      world.groups.emissive.visible=false;
      world.groups.atmosphere.visible=false;
      world.groups.characters.visible=true;
      post.bloom.strength=0;
      post.finish.uniforms.uEnabled.value=0;
    },
    clean(){
      world.groups.lines.visible=true;
      world.groups.emissive.visible=true;
      world.groups.atmosphere.visible=true;
      world.groups.characters.visible=true;
      post.bloom.strength=bloomStrength;
      post.finish.uniforms.uEnabled.value=0;
    },
    final(){
      world.groups.lines.visible=true;
      world.groups.emissive.visible=true;
      world.groups.atmosphere.visible=true;
      world.groups.characters.visible=true;
      post.bloom.strength=bloomStrength;
      post.finish.uniforms.uEnabled.value=1;
    },
  };

  window.__graphicCityLab={
    ready:true,
    setShot(index){const shot=applyShot(camera,controls,index);render();return shot;},
    setMode(name){
      if(!modes[name]) throw new Error(`Unknown mode: ${name}`);
      modes[name](); render(); return name;
    },
    render,
    info(){return {profile,camera:{position:camera.position.toArray(),target:controls.target.toArray()}};},
  };
}
