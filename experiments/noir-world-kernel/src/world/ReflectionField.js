import * as THREE from 'three';

function makeTexture(sources,bounds,{size=1024}={}){
  const canvas=document.createElement('canvas');canvas.width=canvas.height=size;
  const ctx=canvas.getContext('2d');
  let seed=7331;
  const rnd=()=>((seed=Math.imul(seed,1664525)+1013904223>>>0)/4294967296);
  const {minX,maxX,minZ,maxZ}=bounds;
  const toX=x=>(x-minX)/(maxX-minX)*size;
  const toY=z=>(maxZ-z)/(maxZ-minZ)*size;

  for(const source of sources){
    const x=toX(source.x),y=Math.max(0,Math.min(size,toY(source.z)));
    const width=Math.max(6,(source.width??.5)/(maxX-minX)*size*1.6);
    const length=130+(source.intensity??.6)*360;
    const gold=source.color==='gold';
    const rgb=gold?'226,182,61':'248,245,234';
    const gradient=ctx.createLinearGradient(0,y,0,Math.min(size,y+length));
    gradient.addColorStop(0,'rgba('+rgb+','+(.10*(source.intensity??1))+')');
    gradient.addColorStop(.3,'rgba('+rgb+','+(.065*(source.intensity??1))+')');
    gradient.addColorStop(1,'rgba('+rgb+',0)');
    ctx.save();ctx.fillStyle=gradient;ctx.shadowColor='rgba('+rgb+',.05)';ctx.shadowBlur=18;
    ctx.fillRect(x-width/2,y,width,length);ctx.restore();
  }

  // Water interrupts the source-driven vertical light memory.
  ctx.save();ctx.globalCompositeOperation='destination-out';
  for(let i=0;i<150;i++){
    const y=rnd()*size,x=rnd()*size,len=25+rnd()*260;
    ctx.fillStyle='rgba(0,0,0,'+(.55+rnd()*.38)+')';
    ctx.fillRect(x,y,len,2+rnd()*9);
  }
  ctx.restore();

  ctx.lineCap='round';
  for(let i=0;i<80;i++){
    const x=rnd()*size,y=rnd()*size,len=10+rnd()*100;
    ctx.strokeStyle='rgba(245,242,232,'+(.035+rnd()*.08)+')';
    ctx.lineWidth=.6+rnd()*2.0;ctx.beginPath();ctx.moveTo(x,y);ctx.lineTo(x+len,y+(rnd()-.5)*4);ctx.stroke();
  }

  const texture=new THREE.CanvasTexture(canvas);
  texture.colorSpace=THREE.SRGBColorSpace;
  return texture;
}

export function addReflectionField(parent,{
  sources=[],
  bounds={minX:-20,maxX:20,minZ:-25,maxZ:15},
  opacity=.62
}={}){
  const width=bounds.maxX-bounds.minX;
  const depth=bounds.maxZ-bounds.minZ;
  const material=new THREE.MeshBasicMaterial({
    map:makeTexture(sources,bounds),transparent:true,opacity,depthWrite:false,toneMapped:false
  });
  const mesh=new THREE.Mesh(new THREE.PlaneGeometry(width,depth),material);
  mesh.rotation.x=-Math.PI/2;
  mesh.position.set((bounds.minX+bounds.maxX)/2,.015,(bounds.minZ+bounds.maxZ)/2);
  mesh.renderOrder=4;parent.add(mesh);return mesh;
}
