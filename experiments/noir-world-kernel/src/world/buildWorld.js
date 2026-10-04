import * as THREE from 'three';
import { strokePath, strokeSegments, resizeVectorStrokes } from '../style/VectorStroke.js';
import { addFacade } from './FacadeGrammar.js';
import { addReflectionField } from './ReflectionField.js';
import { addPuppetCharacter } from './PuppetCharacter.js';
import { addSvgProp } from './SVGProp.js';
import taxiSvg from '../assets/taxi-study.svg?raw';

const P={
  void:0x020309,ink:0x010205,deep:0x090b10,wall:0x10131a,wall2:0x151921,
  white:0xf2efe6,dim:0x8a9098,gold:0xe2b63d
};

const basic=(color,options={})=>new THREE.MeshBasicMaterial({color,toneMapped:false,...options});
const standard=(color,options={})=>new THREE.MeshStandardMaterial({color,roughness:.96,metalness:0,...options});

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

function groundPanel(parent,size,pos,color=P.white,opacity=1,rotationZ=0){
  const mesh=new THREE.Mesh(new THREE.PlaneGeometry(...size),basic(color,{
    transparent:opacity<1,opacity,side:THREE.DoubleSide,depthWrite:false
  }));
  mesh.rotation.x=-Math.PI/2;mesh.rotation.z=rotationZ;
  mesh.position.set(...pos);mesh.renderOrder=5;parent.add(mesh);return mesh;
}

function canvasTexture(draw,w=512,h=256){
  const canvas=document.createElement('canvas');canvas.width=w;canvas.height=h;
  const context=canvas.getContext('2d');draw(context,w,h);
  const texture=new THREE.CanvasTexture(canvas);texture.colorSpace=THREE.SRGBColorSpace;return texture;
}

function haloTexture(){
  return canvasTexture((ctx,w,h)=>{
    const gradient=ctx.createRadialGradient(w/2,h/2,0,w/2,h/2,w/2);
    gradient.addColorStop(0,'rgba(255,252,236,.92)');
    gradient.addColorStop(.07,'rgba(255,252,236,.46)');
    gradient.addColorStop(.28,'rgba(255,252,236,.12)');
    gradient.addColorStop(1,'rgba(255,252,236,0)');
    ctx.fillStyle=gradient;ctx.fillRect(0,0,w,h);
  },256,256);
}

function addGroundGlow(parent,x,z,{width=6,depth=3,opacity=.10}={}){
  const material=new THREE.MeshBasicMaterial({
    map:haloTexture(),transparent:true,opacity,depthWrite:false,toneMapped:false,
    blending:THREE.AdditiveBlending,side:THREE.DoubleSide
  });
  const mesh=new THREE.Mesh(new THREE.PlaneGeometry(width,depth),material);
  mesh.rotation.x=-Math.PI/2;mesh.position.set(x,.018,z);mesh.renderOrder=3;parent.add(mesh);
  return mesh;
}

function textTexture(text){
  return canvasTexture((ctx,w,h)=>{
    ctx.clearRect(0,0,w,h);ctx.fillStyle='#f2efe6';
    ctx.font='600 38px Georgia,serif';ctx.textAlign='center';ctx.textBaseline='middle';
    ctx.fillText(text,w/2,h/2);
  },512,96);
}

function shapeMesh(parent,shape,z,color=P.ink){
  const mesh=new THREE.Mesh(new THREE.ShapeGeometry(shape,32),basic(color,{side:THREE.DoubleSide}));
  mesh.position.z=z;parent.add(mesh);return mesh;
}

function addDiner(fill,strokes,glow){
  box(fill,[11.2,4.5,4.0],[0,2.25,-15.0],standard(P.deep));

  const shape=new THREE.Shape();
  shape.moveTo(-5.6,-2.05);shape.lineTo(4.35,-2.05);
  shape.quadraticCurveTo(5.55,-2.05,5.55,-.80);
  shape.lineTo(5.55,.85);shape.quadraticCurveTo(5.55,2.05,4.35,2.05);
  shape.lineTo(-5.6,2.05);shape.closePath();
  const front=shapeMesh(fill,shape,-12.98,P.deep);front.position.y=2.25;

  const outline=shape.getSpacedPoints(84).map(point=>[point.x,point.y+2.25,-12.95]);
  strokePath(strokes,outline,{color:P.white,width:2.05,opacity:.86,closed:true});

  strokeSegments(strokes,[
    [-5.35,3.72,-12.93,5.15,3.72,-12.93],
    [-5.35,.74,-12.93,5.08,.74,-12.93]
  ],{color:P.white,width:2.35,opacity:.88});

  const winZ=-12.91;
  panel(fill,[3.15,1.48],[-2.75,2.10,winZ],0xd8d6ce,.20);
  panel(fill,[3.25,1.48],[2.05,2.10,winZ],0xd8d6ce,.23);

  // Window borders and mullions do more work than the luminous fill.
  strokeSegments(strokes,[
    [-4.33,1.36,-12.89,-1.17,1.36,-12.89],[-4.33,2.84,-12.89,-1.17,2.84,-12.89],
    [-4.33,1.36,-12.89,-4.33,2.84,-12.89],[-1.17,1.36,-12.89,-1.17,2.84,-12.89],
    [-2.75,1.36,-12.89,-2.75,2.84,-12.89],
    [.42,1.36,-12.89,3.68,1.36,-12.89],[.42,2.84,-12.89,3.68,2.84,-12.89],
    [.42,1.36,-12.89,.42,2.84,-12.89],[3.68,1.36,-12.89,3.68,2.84,-12.89],
    [2.05,1.36,-12.89,2.05,2.84,-12.89]
  ],{color:P.white,width:1.15,opacity:.58});

  // Counter and stools keep the interior legible without making a literal room.
  strokeSegments(strokes,[
    [-4.05,1.72,-12.87,-1.45,1.72,-12.87],
    [.68,1.72,-12.87,3.42,1.72,-12.87],
    [-3.65,1.40,-12.87,-3.65,1.12,-12.87],[-2.75,1.40,-12.87,-2.75,1.10,-12.87],
    [1.10,1.40,-12.87,1.10,1.12,-12.87],[2.25,1.40,-12.87,2.25,1.10,-12.87]
  ],{color:P.white,width:.85,opacity:.45});

  const sign=new THREE.Mesh(new THREE.PlaneGeometry(3.7,.58),new THREE.MeshBasicMaterial({
    map:textTexture('NIGHT CAFE'),transparent:true,toneMapped:false,depthWrite:false
  }));
  sign.position.set(-.25,3.93,-12.89);sign.renderOrder=8;fill.add(sign);

  const halo=new THREE.Sprite(new THREE.SpriteMaterial({
    map:haloTexture(),transparent:true,opacity:.10,depthWrite:false,blending:THREE.AdditiveBlending
  }));
  halo.position.set(0,2.45,-12.65);halo.scale.set(10.5,5.2,1);glow.add(halo);

  return [
    {x:-2.75,z:winZ,width:1.0,intensity:.52,color:'white'},
    {x:2.05,z:winZ,width:1.05,intensity:.58,color:'white'},
    {x:-.25,z:-12.89,width:.7,intensity:.32,color:'white'}
  ];
}

function addTreeSilhouette(fill,strokes,x,z,scale=1){
  const group=new THREE.Group();
  group.position.set(x,0,z);group.scale.setScalar(scale);fill.add(group);

  box(group,[.22,2.8,.16],[0,1.4,0],standard(0x05070b));

  const crown=new THREE.Shape();
  crown.moveTo(-.90,2.25);
  crown.bezierCurveTo(-1.30,2.55,-1.18,3.12,-.72,3.28);
  crown.bezierCurveTo(-.92,3.72,-.48,4.05,-.08,3.92);
  crown.bezierCurveTo(.12,4.35,.72,4.26,.84,3.84);
  crown.bezierCurveTo(1.25,3.78,1.38,3.20,1.02,2.94);
  crown.bezierCurveTo(1.25,2.55,.78,2.18,.42,2.30);
  crown.bezierCurveTo(.06,2.08,-.45,2.05,-.90,2.25);
  crown.closePath();
  const mesh=shapeMesh(group,crown,.03,0x0a0c10);mesh.renderOrder=5;

  const lineGroup=new THREE.Group();
  lineGroup.position.copy(group.position);lineGroup.scale.copy(group.scale);strokes.add(lineGroup);
  const outline=crown.getSpacedPoints(72).map(point=>[point.x,point.y,.05]);
  strokePath(lineGroup,outline,{color:P.white,width:1.05,opacity:.18,closed:true});

  strokeSegments(lineGroup,[
    [0,2.15,.05,-.35,2.85,.05],
    [0,2.20,.05,.42,3.02,.05],
    [-.05,2.55,.05,.55,3.52,.05]
  ],{color:P.white,width:.65,opacity:.12});
}

function addLamp(fill,strokes,glow,x,z,h=6.2){
  strokeSegments(strokes,[[x,0,z,x,h,z],[x,h,z,x+.72,h,z]],{
    color:P.white,width:1.65,opacity:.54
  });
  panel(fill,[.20,.09],[x+.72,h-.03,z+.02],P.white,1);
  const halo=new THREE.Sprite(new THREE.SpriteMaterial({
    map:haloTexture(),transparent:true,opacity:.42,depthWrite:false,blending:THREE.AdditiveBlending
  }));
  halo.position.set(x+.72,h-.03,z+.2);halo.scale.set(3.6,3.6,1);glow.add(halo);
  return {x:x+.72,z,width:.26,intensity:.72,color:'white'};
}

function addGrassPatch(strokes,x,z,{count=18,scale=1,seed=1}={}){
  let state=seed>>>0;
  const rnd=()=>((state=Math.imul(state,1664525)+1013904223>>>0)/4294967296);
  const blades=[];
  for(let i=0;i<count;i++){
    const bx=x+(rnd()-.5)*1.8*scale,bz=z+(rnd()-.5)*.65*scale;
    const h=.18+rnd()*.75*scale,lean=(rnd()-.5)*.28*scale;
    blades.push([bx,.02,bz,bx+lean,h,bz+(rnd()-.5)*.08]);
  }
  strokeSegments(strokes,blades,{color:P.white,width:.75,opacity:.30});
}

function addRain(strokes){
  let seed=4481;
  const rnd=()=>((seed=Math.imul(seed,1664525)+1013904223>>>0)/4294967296);
  const rain=[];
  for(let i=0;i<92;i++){
    const x=-20+rnd()*40,y=1+rnd()*14,z=-24+rnd()*40,length=.18+rnd()*.46;
    rain.push([x,y,z,x-.10,y-length,z+.02]);
  }
  strokeSegments(strokes,rain,{color:P.white,width:.55,opacity:.060});
}

function addForegroundFrame(fill,strokes){
  box(fill,[2.2,13,4.0],[-18.7,6.5,9.7],standard(P.ink),-.10);
  strokeSegments(strokes,[
    [-17.6,.2,7.8,-17.6,12.0,7.8],
    [-17.6,12.0,7.8,-15.2,12.0,7.8]
  ],{color:P.white,width:.95,opacity:.18});
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
    litDensity:.68,seed:84,windowCols:7,windowRows:8,windowScale:[.34,.34],edgeOpacity:.16
  }).reflectionSources);

  reflectionSources.push(...addFacade(fill,strokes,{
    x:-12.4,z:-16,w:9.0,h:11.5,d:5,color:P.wall,white:P.white,
    litDensity:.24,seed:31,windowCols:6,windowRows:6,windowScale:[.30,.30],edgeOpacity:.20
  }).reflectionSources);

  reflectionSources.push(...addFacade(fill,strokes,{
    x:-13.8,z:-2.5,w:8.0,h:8.5,d:5,color:P.deep,white:P.white,
    litDensity:.13,seed:7,windowCols:5,windowRows:5,windowScale:[.28,.28],edgeOpacity:.14
  }).reflectionSources);

  reflectionSources.push(...addFacade(fill,strokes,{
    x:13.8,z:-4,w:8.3,h:9.5,d:5,color:P.deep,white:P.white,
    litDensity:.18,seed:18,windowCols:5,windowRows:5,windowScale:[.28,.30],edgeOpacity:.14
  }).reflectionSources);

  box(fill,[17,18,4],[1.5,9,-27],standard(0x06080d));
  const farZ=-24.96;
  for(let row=0;row<8;row++){
    for(let col=0;col<10;col++){
      if((row*3+col*5)%9===0 || (row+col)%7===0) continue;
      const x=-6.1+col*1.45,y=2.6+row*1.65;
      const width=.38+((row+col)%3)*.05,height=.48+((row*2+col)%2)*.06;
      panel(fill,[width,height],[x,y,farZ],P.white,.76);
      if(row<2) reflectionSources.push({x,z:farZ,width:.22,intensity:.18,color:'white'});
    }
  }

  reflectionSources.push(...addDiner(fill,strokes,glow));

  // A real graphic crosswalk, closer to the source game's bold shape language.
  for(let i=-3;i<=3;i++){
    const gold=i%2===0;
    groundPanel(fill,[.50,3.45],[i*1.12,.025,.30],gold?P.gold:P.white,gold?.48:.25,-.03);
  }

  reflectionSources.push(addLamp(fill,strokes,glow,-6.5,4.4,6.0));
  reflectionSources.push(addLamp(fill,strokes,glow,6.7,-4.8,6.3));

  addPuppetCharacter(fill,{kind:'detective',position:[2.8,.02,5.8],scale:.0118,strokeScale:.84});
  addPuppetCharacter(fill,{kind:'pedestrian',position:[-8.0,.02,-5.8],scale:.0062,mirror:true,strokeScale:.86});
  addPuppetCharacter(fill,{kind:'pedestrian',position:[7.6,.02,-2.3],scale:.0058,strokeScale:.86});
  addPuppetCharacter(fill,{kind:'pedestrian',position:[1.8,.02,-10.7],scale:.0034,mirror:true,strokeScale:.82});
  addPuppetCharacter(fill,{kind:'pedestrian',position:[3.0,.02,-10.5],scale:.0031,strokeScale:.82});
  addPuppetCharacter(fill,{kind:'pedestrian',position:[-2.6,.02,-10.9],scale:.0033,mirror:true,strokeScale:.82});

  addSvgProp(fill,taxiSvg,{
    position:[-5.7,.03,3.9],scale:.0108,pivot:[150,110],rotationY:.10,strokeScale:.78
  });
  reflectionSources.push({x:-5.7,z:3.9,width:.60,intensity:.22,color:'gold'});

  addTreeSilhouette(fill,strokes,1.7,-10.0,.96);

  addGroundGlow(glow,-5.4,4.0,{width:7.4,depth:3.2,opacity:.075});
  addGroundGlow(glow,.5,-5.5,{width:8.5,depth:3.8,opacity:.045});

  addReflectionField(glow,{
    sources:reflectionSources,
    bounds:{minX:-20,maxX:20,minZ:-27,maxZ:15},
    opacity:.31
  });

  addGrassPatch(strokes,-8.2,5.5,{count:25,scale:1.2,seed:11});
  addGrassPatch(strokes,-4.4,-11.2,{count:18,scale:.8,seed:27});

  const ring=new THREE.Mesh(new THREE.RingGeometry(2.45,2.56,96),basic(P.gold,{
    transparent:true,opacity:.80,side:THREE.DoubleSide
  }));
  ring.position.set(-8.5,12.0,-29);fill.add(ring);

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
