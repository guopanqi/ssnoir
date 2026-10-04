import * as THREE from 'three';
import { strokeSmooth, strokeSegments, resizeVectorStrokes } from '../style/VectorStroke.js';
import { addFacade } from './FacadeGrammar.js';
import { addReflectionField } from './ReflectionField.js';
import { addPuppetCharacter } from './PuppetCharacter.js';
import { addSvgProp } from './SVGProp.js';
import taxiSvg from '../assets/taxi-study.svg?raw';

const P={
  void:0x020309,ink:0x010205,deep:0x0a0c12,wall:0x12151c,wall2:0x181b22,
  white:0xf2efe6,dim:0x8a9098,gold:0xe2b63d
};

const basic=(color,options={})=>new THREE.MeshBasicMaterial({color,toneMapped:false,...options});
const standard=(color,options={})=>new THREE.MeshStandardMaterial({color,roughness:.95,metalness:0,...options});

function box(parent,size,pos,material,rotationY=0){
  const mesh=new THREE.Mesh(new THREE.BoxGeometry(...size),material);
  mesh.position.set(...pos);mesh.rotation.y=rotationY;mesh.receiveShadow=true;parent.add(mesh);return mesh;
}

function panel(parent,size,pos,color=P.white,opacity=1){
  const mesh=new THREE.Mesh(new THREE.PlaneGeometry(...size),basic(color,{
    transparent:opacity<1,opacity,side:THREE.DoubleSide,depthWrite:opacity===1
  }));
  mesh.position.set(...pos);mesh.renderOrder=6;parent.add(mesh);return mesh;
}

function canvasTexture(draw,w=512,h=256){
  const canvas=document.createElement('canvas');canvas.width=w;canvas.height=h;
  const context=canvas.getContext('2d');draw(context,w,h);
  const texture=new THREE.CanvasTexture(canvas);texture.colorSpace=THREE.SRGBColorSpace;return texture;
}

function haloTexture(){
  return canvasTexture((ctx,w,h)=>{
    const gradient=ctx.createRadialGradient(w/2,h/2,0,w/2,h/2,w/2);
    gradient.addColorStop(0,'rgba(255,252,236,.95)');
    gradient.addColorStop(.07,'rgba(255,252,236,.50)');
    gradient.addColorStop(.30,'rgba(255,252,236,.14)');
    gradient.addColorStop(1,'rgba(255,252,236,0)');
    ctx.fillStyle=gradient;ctx.fillRect(0,0,w,h);
  },256,256);
}

function textTexture(text){
  return canvasTexture((ctx,w,h)=>{
    ctx.clearRect(0,0,w,h);ctx.fillStyle='#f2efe6';
    ctx.font='600 42px Georgia,serif';ctx.textAlign='center';ctx.textBaseline='middle';
    ctx.fillText(text,w/2,h/2);
  },512,96);
}

function shapeMesh(parent,shape,z,color=P.ink){
  const mesh=new THREE.Mesh(new THREE.ShapeGeometry(shape,32),basic(color,{side:THREE.DoubleSide}));
  mesh.position.z=z;parent.add(mesh);return mesh;
}

function addDiner(fill,strokes,glow){
  box(fill,[12,4.7,4.4],[0,2.35,-15.2],standard(P.deep));

  const shape=new THREE.Shape();
  shape.moveTo(-6,-2.2);shape.lineTo(4.7,-2.2);
  shape.quadraticCurveTo(6,-2.2,6,-.9);
  shape.lineTo(6,1.0);shape.quadraticCurveTo(6,2.2,4.7,2.2);
  shape.lineTo(-6,2.2);shape.closePath();
  const front=shapeMesh(fill,shape,-12.95,P.deep);front.position.y=2.25;

  const outline=shape.getPoints(48).map(point=>[point.x,point.y+2.25,-12.92]);
  strokeSmooth(strokes,outline,{color:P.white,width:2.8,opacity:.93,closed:true,samples:90});
  strokeSegments(strokes,[
    [-5.8,3.78,-12.89,5.65,3.78,-12.89],
    [-5.8,.70,-12.89,5.45,.70,-12.89]
  ],{color:P.white,width:4.0,opacity:.93});

  panel(fill,[3.0,1.55],[-3.05,2.12,-12.88],P.white,.82);
  panel(fill,[3.3,1.55],[2.25,2.12,-12.88],P.white,.90);

  const sign=new THREE.Mesh(new THREE.PlaneGeometry(4.2,.7),new THREE.MeshBasicMaterial({
    map:textTexture('NIGHT CAFE'),transparent:true,toneMapped:false,depthWrite:false
  }));
  sign.position.set(-.4,4.0,-12.86);sign.renderOrder=8;fill.add(sign);

  const halo=new THREE.Sprite(new THREE.SpriteMaterial({
    map:haloTexture(),transparent:true,opacity:.16,depthWrite:false,blending:THREE.AdditiveBlending
  }));
  halo.position.set(0,2.6,-12.6);halo.scale.set(13,6.5,1);glow.add(halo);

  return [
    {x:-3.05,z:-12.88,width:3.0,intensity:.95,color:'white'},
    {x:2.25,z:-12.88,width:3.3,intensity:1.0,color:'white'},
    {x:-.4,z:-12.86,width:2.0,intensity:.60,color:'white'}
  ];
}

function addLamp(fill,strokes,glow,x,z,h=6.2){
  strokeSegments(strokes,[[x,0,z,x,h,z],[x,h,z,x+.72,h,z]],{
    color:P.white,width:2.0,opacity:.64
  });
  panel(fill,[.24,.11],[x+.72,h-.03,z+.02],P.white,1);
  const halo=new THREE.Sprite(new THREE.SpriteMaterial({
    map:haloTexture(),transparent:true,opacity:.54,depthWrite:false,blending:THREE.AdditiveBlending
  }));
  halo.position.set(x+.72,h-.03,z+.2);halo.scale.set(4.3,4.3,1);glow.add(halo);
  return {x:x+.72,z,width:.35,intensity:1.0,color:'white'};
}

function addRain(strokes){
  let seed=4481;
  const rnd=()=>((seed=Math.imul(seed,1664525)+1013904223>>>0)/4294967296);
  const rain=[];
  for(let i=0;i<105;i++){
    const x=-20+rnd()*40,y=1+rnd()*14,z=-24+rnd()*40,length=.2+rnd()*.50;
    rain.push([x,y,z,x-.11,y-length,z+.02]);
  }
  strokeSegments(strokes,rain,{color:P.white,width:.60,opacity:.075});
}

function addForegroundFrame(fill,strokes){
  box(fill,[2.4,13,4.0],[-18.6,6.5,9.5],standard(P.ink),-.10);
  strokeSegments(strokes,[
    [-17.45,.2,7.6,-17.45,12.2,7.6],
    [-17.45,12.2,7.6,-15.0,12.2,7.6]
  ],{color:P.white,width:1.1,opacity:.24});
}

export function buildWorld(scene){
  const fill=new THREE.Group();
  const strokes=new THREE.Group();
  const glow=new THREE.Group();
  scene.add(fill,strokes,glow);

  box(fill,[44,.12,48],[0,-.09,-7],standard(P.ink));

  const reflectionSources=[];

  reflectionSources.push(...addFacade(fill,strokes,{
    x:12.2,z:-18,w:10.5,h:15,d:5,color:P.wall2,white:P.white,roof:'setback',
    lit:[0,1,2,3,5,6,7,8,10,11,12,13,15,16,17,18]
  }).reflectionSources);

  reflectionSources.push(...addFacade(fill,strokes,{
    x:-12.4,z:-16,w:9.0,h:11.5,d:5,color:P.wall,white:P.white,
    lit:[1,5,8]
  }).reflectionSources);

  reflectionSources.push(...addFacade(fill,strokes,{
    x:-13.8,z:-2.5,w:8.0,h:8.5,d:5,color:P.deep,white:P.white,edgeOpacity:.22,
    lit:[3]
  }).reflectionSources);

  reflectionSources.push(...addFacade(fill,strokes,{
    x:13.8,z:-4,w:8.3,h:9.5,d:5,color:P.deep,white:P.white,edgeOpacity:.20,
    lit:[0,5]
  }).reflectionSources);

  // Deep city: window field is a graphic mass, not a fully described building.
  box(fill,[17,18,4],[1.5,9,-27],standard(0x070a12));
  const farZ=-24.96;
  for(let row=0;row<7;row++){
    for(let col=0;col<8;col++){
      if((row*3+col*5)%7===0 || (row+col)%5===0) continue;
      const x=-5.1+col*1.9,y=3.0+row*1.85;
      const width=.62+((row+col)%3)*.07,height=.72+((row*2+col)%2)*.08;
      panel(fill,[width,height],[x,y,farZ],P.white,.86);
      if(row<2) reflectionSources.push({x,z:farZ,width,intensity:.36,color:'white'});
    }
  }

  reflectionSources.push(...addDiner(fill,strokes,glow));

  strokeSegments(strokes,[
    [-7.7,.025,12,-6.2,.025,-10.5],
    [7.7,.025,12,6.0,.025,-10.5]
  ],{color:P.white,width:1.3,opacity:.25});

  const crosswalk=[];
  for(let i=-3;i<=3;i++) crosswalk.push([i*1.2-.42,.035,3.1,i*1.2+.40,.035,3.1]);
  strokeSegments(strokes,crosswalk,{color:P.gold,width:3.0,opacity:.68});

  reflectionSources.push(addLamp(fill,strokes,glow,-6.5,4.3,6.0));
  reflectionSources.push(addLamp(fill,strokes,glow,6.9,-4.7,6.4));

  addPuppetCharacter(fill,{kind:'detective',position:[-.6,.02,4.2],strokeScale:.92});
  addPuppetCharacter(fill,{kind:'pedestrian',position:[-8.1,.02,-5.5],scale:.0064,mirror:true,strokeScale:.92});
  addPuppetCharacter(fill,{kind:'pedestrian',position:[7.8,.02,-2.0],scale:.0060,strokeScale:.92});
  addPuppetCharacter(fill,{kind:'pedestrian',position:[1.9,.02,-10.8],scale:.0036,mirror:true,strokeScale:.88});
  addPuppetCharacter(fill,{kind:'pedestrian',position:[3.0,.02,-10.5],scale:.0033,strokeScale:.88});
  addPuppetCharacter(fill,{kind:'pedestrian',position:[-2.6,.02,-10.9],scale:.0035,mirror:true,strokeScale:.88});

  // Vector props use the same ingestion path planned for AI-authored SVG assets.
  addSvgProp(fill,taxiSvg,{
    position:[6.2,.03,5.4],scale:.013,pivot:[150,110],rotationY:-.14,strokeScale:.92
  });
  reflectionSources.push({x:6.2,z:5.4,width:1.3,intensity:.45,color:'gold'});

  addReflectionField(glow,{
    sources:reflectionSources,
    bounds:{minX:-20,maxX:20,minZ:-27,maxZ:15},
    opacity:.56
  });

  const ring=new THREE.Mesh(new THREE.RingGeometry(2.7,2.84,96),basic(P.gold,{
    transparent:true,opacity:.88,side:THREE.DoubleSide
  }));
  ring.position.set(-8.5,12.2,-29);fill.add(ring);
  const dot=new THREE.Mesh(new THREE.CircleGeometry(.25,24),basic(P.gold));
  dot.position.set(-5.85,10.1,-28.9);fill.add(dot);

  addForegroundFrame(fill,strokes);
  addRain(strokes);

  return {
    fill,strokes,glow,
    setMode(mode){
      fill.visible=true;strokes.visible=true;glow.visible=true;
      if(mode==='shape'){strokes.visible=false;glow.visible=false;}
      if(mode==='line'){fill.visible=false;glow.visible=false;}
    },
    resize(width,height){resizeVectorStrokes(width,height);}
  };
}
