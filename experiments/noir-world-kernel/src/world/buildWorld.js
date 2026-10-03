import * as THREE from 'three';
import { polyline, smoothPolyline, segments, resizeLineMaterials } from '../style/lineArt.js';

const P={
  void:0x020309, ink:0x010205, deep:0x070a11, wall:0x0b0f17, wall2:0x101520,
  white:0xf2efe6, dim:0x8a9098, gold:0xe2b63d
};

const basic=(color,opts={})=>new THREE.MeshBasicMaterial({color,toneMapped:false,...opts});
const standard=(color,opts={})=>new THREE.MeshStandardMaterial({color,roughness:.95,metalness:0,...opts});

function box(parent,size,pos,mat,rotY=0){
  const m=new THREE.Mesh(new THREE.BoxGeometry(...size),mat);
  m.position.set(...pos);m.rotation.y=rotY;m.receiveShadow=true;parent.add(m);return m;
}
function panel(parent,size,pos,color=P.white,opacity=1){
  const m=new THREE.Mesh(new THREE.PlaneGeometry(...size),basic(color,{
    transparent:opacity<1,opacity,side:THREE.DoubleSide,depthWrite:opacity===1
  }));
  m.position.set(...pos);m.renderOrder=6;parent.add(m);return m;
}
function canvasTexture(draw,w=512,h=256){
  const c=document.createElement('canvas');c.width=w;c.height=h;
  const ctx=c.getContext('2d');draw(ctx,w,h);
  const t=new THREE.CanvasTexture(c);t.colorSpace=THREE.SRGBColorSpace;return t;
}
function haloTexture(){
  return canvasTexture((ctx,w,h)=>{
    const g=ctx.createRadialGradient(w/2,h/2,0,w/2,h/2,w/2);
    g.addColorStop(0,'rgba(255,252,236,.95)');
    g.addColorStop(.07,'rgba(255,252,236,.52)');
    g.addColorStop(.30,'rgba(255,252,236,.16)');
    g.addColorStop(1,'rgba(255,252,236,0)');
    ctx.fillStyle=g;ctx.fillRect(0,0,w,h);
  },256,256);
}
function reflectionTexture(){
  return canvasTexture((ctx,w,h)=>{
    ctx.clearRect(0,0,w,h);
    const streak=(x,y,len,width,alpha,color='255,252,240')=>{
      ctx.save();
      ctx.strokeStyle='rgba('+color+','+alpha+')';
      ctx.lineWidth=width;
      ctx.lineCap='round';
      ctx.shadowColor='rgba('+color+','+(alpha*.55)+')';
      ctx.shadowBlur=width*2.5;
      ctx.beginPath();ctx.moveTo(x,y);ctx.lineTo(x+len,y+len*.03);ctx.stroke();ctx.restore();
    };
    for(let i=0;i<70;i++){
      const x=(i*137)%w,y=90+((i*83)%760),len=18+((i*61)%95);
      streak(x,y,len,1+((i*17)%4),.05+((i%5)*.012));
    }
    for(let i=0;i<18;i++){
      const x=80+((i*211)%850),y=180+((i*149)%650);
      streak(x,y,38+((i*47)%150),5+((i*13)%12),.10);
    }
    for(let i=0;i<9;i++){
      const x=100+((i*271)%800),y=250+((i*193)%550);
      streak(x,y,32+((i*41)%90),3+((i*7)%8),.12,'226,182,61');
    }
  },1024,1024);
}
function textTexture(text){
  return canvasTexture((ctx,w,h)=>{
    ctx.clearRect(0,0,w,h);
    ctx.fillStyle='#f2efe6';ctx.font='600 42px Georgia,serif';
    ctx.textAlign='center';ctx.textBaseline='middle';ctx.fillText(text,w/2,h/2);
  },512,96);
}
function shapeMesh(parent,shape,z,color=P.ink){
  const m=new THREE.Mesh(new THREE.ShapeGeometry(shape,32),basic(color,{side:THREE.DoubleSide}));
  m.position.z=z;parent.add(m);return m;
}

function addBuilding(fill,lines,{x,z,w,h,d=4,color=P.wall,lit=[]}){
  box(fill,[w,h,d],[x,h/2,z],standard(color));
  const front=z+d/2+.03;
  // Only three major silhouette strokes; no full CAD rectangle.
  polyline(lines,[
    [x-w/2,0,front],[x-w/2,h,front],[x+w/2,h,front],[x+w/2,.4,front]
  ],{color:P.white,width:2.25,opacity:.72});

  const cols=Math.max(3,Math.round(w/2.1));
  const rows=Math.max(3,Math.round(h/2.4));
  const dx=w/(cols+1),dy=h/(rows+1);
  for(let r=0;r<rows;r++){
    for(let c=0;c<cols;c++){
      const idx=r*cols+c;
      if(!lit.includes(idx)) continue;
      const px=x-w/2+dx*(c+1),py=dy*(r+1);
      panel(fill,[dx*.44,dy*.38],[px,py,front+.02],P.white,.98);
    }
  }
}

function roundedDiner(fill,lines,glow){
  const shape=new THREE.Shape();
  shape.moveTo(-6,-2.2);shape.lineTo(4.7,-2.2);
  shape.quadraticCurveTo(6,-2.2,6,-.9);
  shape.lineTo(6,1.0);shape.quadraticCurveTo(6,2.2,4.7,2.2);
  shape.lineTo(-6,2.2);shape.closePath();
  const front=shapeMesh(fill,shape,-12.95,P.deep);
  front.position.y=2.25;

  const pts=shape.getPoints(48).map(p=>[p.x,p.y+2.25,-12.92]);
  smoothPolyline(lines,pts,{color:P.white,width:2.9,opacity:.96,closed:true,segments:90});

  // Horizontal art-deco bands.
  segments(lines,[
    [-5.8,3.78,-12.89,5.65,3.78,-12.89],
    [-5.8,.70,-12.89,5.45,.70,-12.89]
  ],{color:P.white,width:4.2,opacity:.96});

  panel(fill,[3.0,1.55],[-3.05,2.12,-12.88],P.white,.84);
  panel(fill,[3.3,1.55],[2.25,2.12,-12.88],P.white,.92);

  const sign=new THREE.Mesh(new THREE.PlaneGeometry(4.2,.7),new THREE.MeshBasicMaterial({
    map:textTexture('NIGHT CAFE'),transparent:true,toneMapped:false,depthWrite:false
  }));
  sign.position.set(-.4,4.0,-12.86);fill.add(sign);

  const halo=new THREE.Sprite(new THREE.SpriteMaterial({
    map:haloTexture(),transparent:true,opacity:.18,depthWrite:false,blending:THREE.AdditiveBlending
  }));
  halo.position.set(0,2.6,-12.6);halo.scale.set(13,6.5,1);glow.add(halo);
}

function makePersonShape(profile=false,flip=false){
  const s=new THREE.Shape();
  const sx=v=>flip?-v:v;
  if(profile){
    s.moveTo(sx(-.48),.08);
    s.bezierCurveTo(sx(-.50),.9,sx(-.45),1.7,sx(-.37),2.12);
    s.bezierCurveTo(sx(-.54),2.28,sx(-.42),2.48,sx(-.25),2.60);
    s.bezierCurveTo(sx(-.18),2.68,sx(-.12),2.76,sx(-.08),2.88);
    s.bezierCurveTo(sx(.02),3.08,sx(.26),3.13,sx(.38),3.02);
    s.bezierCurveTo(sx(.48),2.93,sx(.42),2.82,sx(.30),2.76);
    s.bezierCurveTo(sx(.51),2.56,sx(.58),2.31,sx(.43),2.08);
    s.bezierCurveTo(sx(.48),1.54,sx(.52),.84,sx(.50),.08);
  }else{
    s.moveTo(sx(-.54),.08);
    s.bezierCurveTo(sx(-.57),.9,sx(-.49),1.75,sx(-.40),2.08);
    s.bezierCurveTo(sx(-.57),2.25,sx(-.44),2.47,sx(-.28),2.60);
    s.bezierCurveTo(sx(-.16),2.72,sx(-.10),2.79,sx(0),2.82);
    s.bezierCurveTo(sx(.10),2.79,sx(.16),2.72,sx(.28),2.60);
    s.bezierCurveTo(sx(.44),2.47,sx(.57),2.25,sx(.40),2.08);
    s.bezierCurveTo(sx(.49),1.75,sx(.57),.9,sx(.54),.08);
  }
  s.closePath();return s;
}

function addFigure(fill,lines,{x,y=0,z,s=1,profile=false,flip=false,gold=false}){
  const g=new THREE.Group();g.position.set(x,y,z);g.scale.setScalar(s);fill.add(g);
  const lg=new THREE.Group();lg.position.copy(g.position);lg.scale.copy(g.scale);lines.add(lg);
  const shape=makePersonShape(profile,flip);
  shapeMesh(g,shape,0,P.ink);
  const pts=shape.getPoints(72).map(p=>[p.x,p.y,.018]);
  polyline(lg,pts,{color:gold?P.gold:P.white,width:3.0,opacity:.98,closed:true});

  // Hat is similarly planar and curved.
  const hs=new THREE.Shape();
  hs.moveTo(-.31,2.80);hs.quadraticCurveTo(-.28,3.09,-.16,3.12);
  hs.lineTo(.20,3.12);hs.quadraticCurveTo(.29,3.03,.31,2.80);hs.closePath();
  shapeMesh(g,hs,.004,P.ink);
  const hp=hs.getPoints(24).map(p=>[p.x,p.y,.024]);
  polyline(lg,hp,{color:P.white,width:2.5,opacity:.95,closed:true});
  segments(lg,[[-.48,2.79,.025,.48,2.79,.025]],{color:P.white,width:3.0,opacity:.96});

  if(profile){
    const k=flip?-1:1;
    smoothPolyline(lg,[[.07*k,2.95,.03],[.22*k,2.95,.03],[.35*k,2.88,.03]],{
      color:P.white,width:1.35,opacity:.60,segments:18
    });
  }
}

function addLamp(fill,lines,glow,x,z,h=6.2){
  segments(lines,[[x,0,z,x,h,z],[x,h,z,x+.72,h,z]],{color:P.white,width:2.1,opacity:.68});
  panel(fill,[.24,.11],[x+.72,h-.03,z+.02],P.white,1);
  const halo=new THREE.Sprite(new THREE.SpriteMaterial({
    map:haloTexture(),transparent:true,opacity:.58,depthWrite:false,blending:THREE.AdditiveBlending
  }));
  halo.position.set(x+.72,h-.03,z+.2);halo.scale.set(4.5,4.5,1);glow.add(halo);
}

function addRain(lines){
  let seed=4481;const rnd=()=>((seed=Math.imul(seed,1664525)+1013904223>>>0)/4294967296);
  const rain=[];
  for(let i=0;i<120;i++){
    const x=-20+rnd()*40,y=1+rnd()*14,z=-24+rnd()*40,l=.2+rnd()*.55;
    rain.push([x,y,z,x-.11,y-l,z+.02]);
  }
  segments(lines,rain,{color:P.white,width:.65,opacity:.09});
}

export function buildWorld(scene){
  const fill=new THREE.Group(),lines=new THREE.Group(),glow=new THREE.Group();
  scene.add(fill,lines,glow);
  box(fill,[44,.12,48],[0,-.09,-7],standard(P.ink));

  // Background tower: bright window rhythm is more important than its box edges.
  addBuilding(fill,lines,{x:12.2,z:-18,w:10.5,h:15,d:5,color:P.wall2,
    lit:[0,1,2,3,5,6,7,8,10,11,12,13,15,16,17,18]});
  addBuilding(fill,lines,{x:-12.4,z:-16,w:9.0,h:11.5,d:5,color:P.wall,
    lit:[1,5,8]});
  addBuilding(fill,lines,{x:-13.8,z:-2.5,w:8.0,h:8.5,d:5,color:P.deep,
    lit:[3]});
  addBuilding(fill,lines,{x:13.8,z:-4,w:8.3,h:9.5,d:5,color:P.deep,
    lit:[0,5]});

  roundedDiner(fill,lines,glow);

  // Sparse perspective lines: enough to establish a walkable space, not a wireframe floor.
  segments(lines,[
    [-7.7,.025,12,-6.2,.025,-10.5],
    [7.7,.025,12,6.0,.025,-10.5]
  ],{color:P.white,width:1.4,opacity:.30});

  const cross=[];
  for(let i=-3;i<=3;i++) cross.push([i*1.2-.42,.035,3.1,i*1.2+.40,.035,3.1]);
  segments(lines,cross,{color:P.gold,width:3.2,opacity:.72});

  addLamp(fill,lines,glow,-6.5,4.3,6.0);
  addLamp(fill,lines,glow,6.9,-4.7,6.4);

  addFigure(fill,lines,{x:0,y:.02,z:4.0,s:1.12,profile:true});
  addFigure(fill,lines,{x:-8.0,y:.02,z:-5.5,s:.78,profile:false});
  addFigure(fill,lines,{x:7.8,y:.02,z:-2.0,s:.70,profile:true,flip:true});

  // Large painterly reflection layer.
  const wet=new THREE.Mesh(new THREE.PlaneGeometry(39,39),new THREE.MeshBasicMaterial({
    map:reflectionTexture(),transparent:true,opacity:.78,depthWrite:false,toneMapped:false
  }));
  wet.rotation.x=-Math.PI/2;wet.position.set(0,.015,1);glow.add(wet);

  const ring=new THREE.Mesh(new THREE.RingGeometry(2.7,2.84,96),basic(P.gold,{
    transparent:true,opacity:.92,side:THREE.DoubleSide
  }));
  ring.position.set(-8.5,12.2,-29);fill.add(ring);
  const dot=new THREE.Mesh(new THREE.CircleGeometry(.25,24),basic(P.gold));
  dot.position.set(-5.85,10.1,-28.9);fill.add(dot);

  addRain(lines);

  return {
    fill,lines,glow,
    setMode(mode){
      fill.visible=true;lines.visible=true;glow.visible=true;
      if(mode==='shape'){lines.visible=false;glow.visible=false;}
      if(mode==='line'){fill.visible=false;glow.visible=false;}
    },
    resize(w,h){resizeLineMaterials(w,h);}
  };
}
