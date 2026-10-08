import * as THREE from 'three';
import { strokePath } from '../style/VectorStroke.js';

const textureCache=new Map();

function seeded(seed=1){
  let value=seed>>>0;
  return ()=>((value=Math.imul(value,1664525)+1013904223>>>0)/4294967296);
}

function canvasTexture(key,draw,w=384,h=384){
  if(textureCache.has(key)) return textureCache.get(key);
  const canvas=document.createElement('canvas');canvas.width=w;canvas.height=h;
  const ctx=canvas.getContext('2d');draw(ctx,w,h);
  const texture=new THREE.CanvasTexture(canvas);texture.colorSpace=THREE.SRGBColorSpace;
  textureCache.set(key,texture);return texture;
}

function washTexture(){
  return canvasTexture('facade-wash-v2',(ctx,w,h)=>{
    const rnd=seeded(1843);
    ctx.filter='blur(8px)';
    for(let i=0;i<30;i++){
      const x=-40+rnd()*(w+80),y=rnd()*h,bw=45+rnd()*130,bh=3+rnd()*14,a=.008+rnd()*.014;
      ctx.save();ctx.translate(x+bw/2,y+bh/2);ctx.rotate((rnd()-.5)*.22);
      const gradient=ctx.createLinearGradient(-bw/2,0,bw/2,0);
      gradient.addColorStop(0,'rgba(225,227,229,0)');
      gradient.addColorStop(.5,'rgba(225,227,229,'+a+')');
      gradient.addColorStop(1,'rgba(225,227,229,0)');
      ctx.fillStyle=gradient;ctx.fillRect(-bw/2,-bh/2,bw,bh);ctx.restore();
    }
    ctx.filter='none';
  });
}

function standard(color){
  return new THREE.MeshStandardMaterial({color,roughness:.96,metalness:0});
}
function unlit(color,opacity=1){
  return new THREE.MeshBasicMaterial({
    color,toneMapped:false,transparent:opacity<1,opacity,side:THREE.DoubleSide
  });
}
function box(parent,size,pos,material){
  const mesh=new THREE.Mesh(new THREE.BoxGeometry(...size),material);
  mesh.position.set(...pos);mesh.receiveShadow=true;parent.add(mesh);return mesh;
}
function panel(parent,size,pos,color,opacity=1){
  const mesh=new THREE.Mesh(new THREE.PlaneGeometry(...size),unlit(color,opacity));
  mesh.position.set(...pos);mesh.renderOrder=6;parent.add(mesh);return mesh;
}

export function addFacade(fill,strokes,{
  x,z,w,h,d=4,color=0x12151c,white=0xf2efe6,
  lit=[],litDensity=0,seed=1,edgeOpacity=.24,roof='flat',
  windowCols=null,windowRows=null,windowScale=[.30,.28]
}={}){
  box(fill,[w,h,d],[x,h/2,z],standard(color));
  if(roof==='setback') box(fill,[w*.70,h*.14,d*.70],[x,h+h*.07,z-.15],standard(color));

  const front=z+d/2+.03;
  const wash=new THREE.Mesh(new THREE.PlaneGeometry(w*.96,h*.94),new THREE.MeshBasicMaterial({
    map:washTexture(),transparent:true,opacity:.28,depthWrite:false,toneMapped:false
  }));
  wash.position.set(x,h*.5,front+.008);wash.renderOrder=3;fill.add(wash);

  strokePath(strokes,[
    [x-w/2,.1,front],[x-w/2,h,front],[x+w/2,h,front],[x+w/2,.45,front]
  ],{color:white,width:1.05,opacity:edgeOpacity});

  const cols=windowCols??Math.max(4,Math.round(w/1.45));
  const rows=windowRows??Math.max(4,Math.round(h/1.75));
  const dx=w/(cols+1),dy=h/(rows+1);
  const rnd=seeded(seed);
  const explicit=new Set(lit);
  const reflectionSources=[];

  for(let row=0;row<rows;row++){
    for(let col=0;col<cols;col++){
      const index=row*cols+col;
      const isLit=explicit.size?explicit.has(index):rnd()<litDensity;
      if(!isLit) continue;

      const px=x-w/2+dx*(col+1),py=dy*(row+1);
      const ww=dx*windowScale[0],hh=dy*windowScale[1];
      const opacity=.78+rnd()*.16;
      panel(fill,[ww,hh],[px,py,front+.02],white,opacity);

      if(row<2){
        reflectionSources.push({
          x:px,z:front,width:Math.max(.18,ww*.75),
          intensity:.28+opacity*.28,color:'white'
        });
      }
    }
  }
  return {front,reflectionSources};
}
