export function installCaptureApi({ profile, world, post, camera, controls, applyShot, render }) {
  const bloomStrength=profile.atmosphere.bloomStrength;

  const setLinework=(visible)=>{
    world.groups.edges.visible=visible;
    world.groups.strokes.visible=visible;
  };

  const modes={
    structure(){
      setLinework(true);
      world.groups.emissive.visible=false;
      world.groups.atmosphere.visible=false;
      world.groups.characters.visible=true;
      post.outline.enabled=true;
      post.bloom.strength=0;
      post.finish.uniforms.uEnabled.value=0;
    },
    clean(){
      setLinework(true);
      world.groups.emissive.visible=true;
      world.groups.atmosphere.visible=true;
      world.groups.characters.visible=true;
      post.outline.enabled=true;
      post.bloom.strength=bloomStrength;
      post.finish.uniforms.uEnabled.value=0;
    },
    final(){
      setLinework(true);
      world.groups.emissive.visible=true;
      world.groups.atmosphere.visible=true;
      world.groups.characters.visible=true;
      post.outline.enabled=true;
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
    info(){
      return {
        profile,
        camera:{position:camera.position.toArray(),target:controls.target.toArray()},
        outlineEnabled:post.outline.enabled,
      };
    },
  };
}
