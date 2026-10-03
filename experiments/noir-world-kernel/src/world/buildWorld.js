import * as THREE from 'three';
import { polyline, smoothPolyline, segments, resizeLineMaterials } from '../style/lineArt.js';
import { addSvgDetective } from './svgFigure.js';

const P={
  void:0x020309, ink:0x010205, deep:0x0a0c12, wall:0x12151c, wall2:0x181b22,
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
}function washTexture(){
  return canvasTexture((ctx,w,h)=>{
    ctx.clearRect(0,0,w,h);
    let seed=1843;
    const rnd=()=>((seed=Math.imul(seed,1664525)+1013904223>>>0)/4294967296);

    // Broad directional brush bands: intentionally too weak to read as individual shapes.
    ctx.save();
    ctx.filter='blur(7px)';
    for(let i=0;i<34;i++){
      const x=-40+rnd()*(w+80),y=rnd()*h;
      const bw=30+rnd()*120,bh=4+rnd()*20;
      const a=.010+rnd()*.018;
      ctx.translate(x+bw/2,y+bh/2);
      ctx.rotate((rnd()-.5)*.28);
      const g=ctx.createLinearGradient(-bw/2,0,bw/2,0);
      g.addColorStop(0,'rgba(225,227,229,0)');
      g.addColorStop(.45,'rgba(225,227,229,'+a+')');
      g.addColorStop(.55,'rgba(225,227,229,'+(a*.8)+')');
      g.addColorStop(1,'rgba(225,227,229,0)');
      ctx.fillStyle=g;ctx.fillRect(-bw/2,-bh/2,bw,bh);
      ctx.setTransform(1,0,0,1,0,0);
    }
    ctx.restore();

    for(let i=0;i<24;i++){
      const x=rnd()*w,y=rnd()*h,len=20+rnd()*95;
      ctx.strokeStyle='rgba(245,242,232,'+(.010+rnd()*.018)+')';
      ctx.lineWidth=.4+rnd()*.8;
      ctx.beginPath();ctx.moveTo(x,y);ctx.lineTo(x+len,y+(rnd()-.5)*4);ctx.stroke();
    }
  },384,384);
}
function reflectionTexture(){
  return canvasTexture((ctx,w,h)=>{
    ctx.clearRect(0,0,w,h);
    let seed=7331;
    const rnd=()=>((seed=Math.imul(seed,1664525)+1013904223>>>0)/4294967296);

    // First paint broad vertical light memories, as if windows and lamps are reflected.
    const columns=[
      [150,210,26,'255,252,238',.075],
      [330,165,34,'255,252,238',.060],
      [530,190,42,'255,252,238',.078],
      [710,235,30,'255,252,238',.052],
      [850,185,34,'226,182,61',.050]
    ];
    for(const [x,y,width,color,alpha] of columns){
      ctx.save();
      ctx.fillStyle='rgba('+color+','+alpha+')';
      ctx.shadowColor='rgba('+color+','+(alpha*.75)+')';
      ctx.shadowBlur=26;
      ctx.fillRect(x,y,width,700-y);
      ctx.restore();
    }

    // Water breaks the vertical reflections into irregular horizontal fragments.
    ctx.save();
    ctx.globalCompositeOperation='destination-out';
    for(let i=0;i<115;i++){
      const y=100+rnd()*850,x=rnd()*900;
      const len=45+rnd()*330;
      ctx.fillStyle='rgba(0,0,0,'+(.52+rnd()*.40)+')';
      ctx.fillRect(x,y,len,2+rnd()*10);
    }
    ctx.restore();

    // A few sharp surface glints sit above the soft reflected masses.
    ctx.lineCap='round';
    for(let i=0;i<70;i++){
      const x=rnd()*950,y=120+rnd()*820,len=12+rnd()*120;
      const gold=i%11===0;
      ctx.strokeStyle=gold?'rgba(226,182,61,.22)':'rgba(245,242,232,.14)';
      ctx.lineWidth=.7+rnd()*2.2;
      ctx.beginPath();ctx.moveTo(x,y);ctx.lineTo(x+len,y+(rnd()-.5)*5);ctx.stroke();
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
  const wash=new THREE.Mesh(new THREE.PlaneGeometry(w*.96,h*.94),new THREE.MeshBasicMaterial({
    map:washTexture(),transparent:true,opacity:.34,depthWrite:false,toneMapped:false
  }));
  wash.position.set(x,h*.50,front+.008);wash.renderOrder=3;fill.add(wash);
  // Only three major silhouette strokes; no full CAD rectangle.
  polyline(lines,[
    [x-w/2,0,front],[x-w/2,h,front],[x+w/2,h,front],[x+w/2,.4,front]
  ],{color:P.white,width:1.30,opacity:.32});

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

function outlinedShape(fillGroup,lineGroup,shape,z=0,{width=2.4,opacity=.95}={}){
  shapeMesh(fillGroup,shape,z,P.ink);
  const pts=shape.getPoints(56).map(p=>[p.x,p.y,z+.025]);
  polyline(lineGroup,pts,{color:P.white,width,opacity,closed:true});
}

function addFigure(fill,lines,{x,y=0,z,s=1,profile=false,flip=false,gold=false}){
  const g=new THREE.Group();g.position.set(x,y,z);g.scale.setScalar(s);fill.add(g);
  const lg=new THREE.Group();lg.position.copy(g.position);lg.scale.copy(g.scale);lines.add(lg);
  const sx=v=>flip?-v:v;

  // Long coat torso: narrower waist, clear shoulder line, gentle flare at the hem.
  const torso=new THREE.Shape();
  torso.moveTo(sx(-.32),2.32);
  torso.quadraticCurveTo(sx(-.43),2.05,sx(-.39),1.70);
  torso.lineTo(sx(-.36),.94);
  torso.quadraticCurveTo(sx(0),.80,sx(.36),.94);
  torso.lineTo(sx(.39),1.70);
  torso.quadraticCurveTo(sx(.43),2.05,sx(.32),2.32);
  torso.quadraticCurveTo(sx(0),2.48,sx(-.32),2.32);
  torso.closePath();
  outlinedShape(g,lg,torso,0,{width:2.7,opacity:.98});

  // Arms are separate planar puppet pieces, which is closer to the source game's 2D/3D mix.
  const armL=new THREE.Shape();
  armL.moveTo(sx(-.30),2.24);
  armL.quadraticCurveTo(sx(-.58),2.02,sx(-.58),1.66);
  armL.lineTo(sx(-.52),.62);
  armL.quadraticCurveTo(sx(-.48),.49,sx(-.39),.58);
  armL.lineTo(sx(-.25),1.72);
  armL.quadraticCurveTo(sx(-.20),2.05,sx(-.30),2.24);
  armL.closePath();
  outlinedShape(g,lg,armL,.003,{width:2.2,opacity:.82});

  const armR=new THREE.Shape();
  armR.moveTo(sx(.30),2.24);
  armR.quadraticCurveTo(sx(.55),1.98,sx(.53),1.61);
  armR.lineTo(sx(.47),.57);
  armR.quadraticCurveTo(sx(.43),.47,sx(.35),.56);
  armR.lineTo(sx(.24),1.72);
  armR.quadraticCurveTo(sx(.20),2.05,sx(.30),2.24);
  armR.closePath();
  outlinedShape(g,lg,armR,.006,{width:2.1,opacity:.78});

  // Legs remain visible below the coat, preventing the silhouette from reading as a robe.
  const legL=new THREE.Shape();
  legL.moveTo(sx(-.25),.92);legL.lineTo(sx(-.07),.92);
  legL.lineTo(sx(-.05),.08);legL.lineTo(sx(-.27),.08);legL.closePath();
  outlinedShape(g,lg,legL,.002,{width:1.8,opacity:.76});

  const legR=new THREE.Shape();
  legR.moveTo(sx(.07),.92);legR.lineTo(sx(.25),.92);
  legR.lineTo(sx(.28),.08);legR.lineTo(sx(.05),.08);legR.closePath();
  outlinedShape(g,lg,legR,.002,{width:1.8,opacity:.76});

    // Head remains a black silhouette; a small nose/chin break makes the profile human.
  const head=new THREE.Shape();
  if(profile){
    head.moveTo(sx(-.16),2.47);
    head.bezierCurveTo(sx(-.24),2.63,sx(-.23),2.83,sx(-.10),2.95);
    head.bezierCurveTo(sx(.02),3.05,sx(.19),3.04,sx(.27),2.98);
    head.lineTo(sx(.38),2.97);
    head.lineTo(sx(.29),2.91);
    head.quadraticCurveTo(sx(.31),2.72,sx(.18),2.58);
    head.quadraticCurveTo(sx(.04),2.45,sx(-.16),2.47);
  }else{
    head.moveTo(sx(-.20),2.48);
    head.bezierCurveTo(sx(-.28),2.65,sx(-.25),2.86,sx(-.10),2.98);
    head.bezierCurveTo(sx(.02),3.06,sx(.19),3.03,sx(.25),2.92);
    head.bezierCurveTo(sx(.31),2.78,sx(.27),2.58,sx(.14),2.49);
    head.quadraticCurveTo(sx(-.04),2.41,sx(-.20),2.48);
  }
  head.closePath();
  outlinedShape(g,lg,head,.010,{width:2.5,opacity:.96});

  // Fedora has a long horizontal brim and a slightly asymmetric crown.
  const hat=new THREE.Shape();
  hat.moveTo(sx(-.30),2.98);
  hat.quadraticCurveTo(sx(-.28),3.25,sx(-.16),3.28);
  hat.lineTo(sx(.21),3.28);
  hat.quadraticCurveTo(sx(.30),3.17,sx(.30),2.98);
  hat.closePath();
  outlinedShape(g,lg,hat,.014,{width:2.35,opacity:.95});
  segments(lg,[[sx(-.48),2.97,.04,sx(.48),2.97,.04]],{color:P.white,width:3.0,opacity:.97});

  // One internal coat seam is enough to imply construction without becoming a wireframe.
  segments(lg,[[sx(0),.24,.04,sx(0),1.05,.04]],{color:P.white,width:1.15,opacity:.32});
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

  // Far office wall: a field of luminous windows builds the city more effectively than contour lines.
  box(fill,[17,18,4],[1.5,9,-27],standard(0x070a12));
  const farZ=-24.96;
  for(let row=0;row<7;row++){
    for(let col=0;col<8;col++){
      if((row*3+col*5)%7===0 || (row+col)%5===0) continue;
      const px=-5.1+col*1.9,py=3.0+row*1.85;
      const ww=.62+((row+col)%3)*.07,hh=.72+((row*2+col)%2)*.08;
      panel(fill,[ww,hh],[px,py,farZ],P.white,.88);
    }
  }

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

  addSvgDetective(fill,{x:-.6,y:.02,z:4.2,scale:.0104});
  addFigure(fill,lines,{x:-8.1,y:.02,z:-5.5,s:.66,profile:false});
  addFigure(fill,lines,{x:7.8,y:.02,z:-2.0,s:.62,profile:true,flip:true});
  addFigure(fill,lines,{x:1.9,y:.02,z:-10.8,s:.36,profile:false});
  addFigure(fill,lines,{x:3.0,y:.02,z:-10.5,s:.32,profile:true,flip:true});
  addFigure(fill,lines,{x:-2.6,y:.02,z:-10.9,s:.34,profile:false});

  // Large painterly reflection layer.
  const wet=new THREE.Mesh(new THREE.PlaneGeometry(39,39),new THREE.MeshBasicMaterial({
    map:reflectionTexture(),transparent:true,opacity:.62,depthWrite:false,toneMapped:false
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
