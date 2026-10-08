import * as THREE from 'three';

function seeded(seed=1){
  let value=seed>>>0;
  return ()=>((value=Math.imul(value,1664525)+1013904223>>>0)/4294967296);
}

function makeTexture(sources,bounds,{size=1024}={}){
  const canvas=document.createElement('canvas');canvas.width=canvas.height=size;
  const ctx=canvas.getContext('2d');
  const rnd=seeded(7331);
  const {minX,maxX,minZ,maxZ}=bounds;
  const toX=x=>(x-minX)/(maxX-minX)*size;
  const toY=z=>(maxZ-z)/(maxZ-minZ)*size;

  for(const source of sources){
    const x=toX(source.x),y=Math.max(0,Math.min(size,toY(source.z)));
    const intensity=source.intensity??.5;
    const width=Math.max(3,(source.width??.4)/(maxX-minX)*size*.85);
    const length=60+intensity*190;
    const gold=source.color==='gold';
    const rgb=gold?'226,182,61':'248,245,234';

    const gradient=ctx.createLinearGradient(0,y,0,Math.min(size,y+length));
    gradient.addColorStop(0,'rgba('+rgb+','+(.045*intensity)+')');
    gradient.addColorStop(.35,'rgba('+rgb+','+(.028*intensity)+')');
    gradient.addColorStop(1,'rgba('+rgb+',0)');

    ctx.save();
    ctx.fillStyle=gradient;
    ctx.shadowColor='rgba('+rgb+','+(.018*intensity)+')';
    ctx.shadowBlur=11;
    ctx.fillRect(x-width/2,y,width,length);
    ctx.restore();

    // Each source leaves a few sharp horizontal water glints close to its own x position.
    for(let i=0;i<4;i++){
      const gy=y+15+rnd()*Math.max(30,length*.75);
      const glen=width*(1.2+rnd()*3.2);
      ctx.strokeStyle='rgba('+rgb+','+(.035+intensity*.035)+')';
      ctx.lineWidth=.7+rnd()*1.3;ctx.lineCap='round';
      ctx.beginPath();ctx.moveTo(x-glen*.5,gy);ctx.lineTo(x+glen*.5,gy+(rnd()-.5)*2);ctx.stroke();
    }
  }

  // Aggressively interrupt broad bars so the eye reads water, not rectangular projected planes.
  ctx.save();ctx.globalCompositeOperation='destination-out';
  for(let i=0;i<220;i++){
    const y=rnd()*size,x=rnd()*size,len=20+rnd()*190;
    ctx.fillStyle='rgba(0,0,0,'+(.70+rnd()*.27)+')';
    ctx.fillRect(x,y,len,2+rnd()*8);
  }
  ctx.restore();

  // Very sparse neutral glints provide continuity between source reflections.
  ctx.lineCap='round';
  for(let i=0;i<45;i++){
    const x=rnd()*size,y=rnd()*size,len=8+rnd()*65;
    ctx.strokeStyle='rgba(245,242,232,'+(.015+rnd()*.035)+')';
    ctx.lineWidth=.5+rnd()*1.1;
    ctx.beginPath();ctx.moveTo(x,y);ctx.lineTo(x+len,y+(rnd()-.5)*3);ctx.stroke();
  }

  const texture=new THREE.CanvasTexture(canvas);
  texture.colorSpace=THREE.SRGBColorSpace;
  return texture;
}

export function addReflectionField(parent,{
  sources=[],
  bounds={minX:-20,maxX:20,minZ:-25,maxZ:15},
  opacity=.34
}={}){
  const width=bounds.maxX-bounds.minX,depth=bounds.maxZ-bounds.minZ;
  const material=new THREE.MeshBasicMaterial({
    map:makeTexture(sources,bounds),transparent:true,opacity,depthWrite:false,toneMapped:false
  });
  const mesh=new THREE.Mesh(new THREE.PlaneGeometry(width,depth),material);
  mesh.rotation.x=-Math.PI/2;
  mesh.position.set((bounds.minX+bounds.maxX)/2,.015,(bounds.minZ+bounds.maxZ)/2);
  mesh.renderOrder=4;parent.add(mesh);return mesh;
}
