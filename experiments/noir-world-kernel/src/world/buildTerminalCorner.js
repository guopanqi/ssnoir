import * as THREE from 'three';
import { addFacade } from './FacadeGrammar.js';
import { addReflectionField } from './ReflectionField.js';
import { addPuppetCharacter } from './PuppetCharacter.js';
import { addVectorProp } from './StreetProps.js';
import { strokePath, strokeSegments, resizeVectorStrokes } from '../style/VectorStroke.js';

const P={ink:0x010205,deep:0x090b10,wall:0x11151b,wall2:0x171b22,white:0xf2efe6,gold:0xe2b63d};
const basic=(color,options={})=>new THREE.MeshBasicMaterial({color,toneMapped:false,...options});
const standard=color=>new THREE.MeshStandardMaterial({color,roughness:.96,metalness:0});

function box(parent,size,pos,color,rotationY=0){
  const mesh=new THREE.Mesh(new THREE.BoxGeometry(...size),standard(color));
  mesh.position.set(...pos);mesh.rotation.y=rotationY;mesh.receiveShadow=true;parent.add(mesh);return mesh;
}
function panel(parent,size,pos,color=P.white,opacity=1){
  const mesh=new THREE.Mesh(new THREE.PlaneGeometry(...size),basic(color,{
    transparent:opacity<1,opacity,side:THREE.DoubleSide,depthWrite:opacity===1
  }));
  mesh.position.set(...pos);mesh.renderOrder=6;parent.add(mesh);return mesh;
}
function canvasTexture(draw,w=256,h=256){
  const canvas=document.createElement('canvas');canvas.width=w;canvas.height=h;
  const ctx=canvas.getContext('2d');draw(ctx,w,h);
  const texture=new THREE.CanvasTexture(canvas);texture.colorSpace=THREE.SRGBColorSpace;return texture;
}
function haloTexture(){
  return canvasTexture((ctx,w,h)=>{
    const g=ctx.createRadialGradient(w/2,h/2,0,w/2,h/2,w/2);
    g.addColorStop(0,'rgba(255,252,236,.85)');
    g.addColorStop(.12,'rgba(255,252,236,.26)');
    g.addColorStop(1,'rgba(255,252,236,0)');
    ctx.fillStyle=g;ctx.fillRect(0,0,w,h);
  });
}
function lamp(fill,strokes,glow,x,z,h=5.8){
  strokeSegments(strokes,[[x,0,z,x,h,z],[x,h,z,x+.58,h,z]],{color:P.white,width:1.45,opacity:.48});
  panel(fill,[.16,.08],[x+.58,h-.02,z+.02],P.white,1);
  const halo=new THREE.Sprite(new THREE.SpriteMaterial({
    map:haloTexture(),transparent:true,opacity:.36,depthWrite:false,blending:THREE.AdditiveBlending
  }));
  halo.position.set(x+.58,h,z+.12);halo.scale.set(3.0,3.0,1);glow.add(halo);
  return {x:x+.58,z,width:.24,intensity:.55,color:'white'};
}

export function buildTerminalCorner(scene){
  const root=new THREE.Group(),fill=new THREE.Group(),strokes=new THREE.Group(),glow=new THREE.Group();
  root.add(fill,strokes,glow);scene.add(root);
  box(fill,[42,.12,44],[0,-.09,-6],P.ink);

  const sources=[];
  sources.push(...addFacade(fill,strokes,{
    x:-11.5,z:-18,w:10,h:13,d:5,color:P.wall2,white:P.white,
    litDensity:.58,seed:121,windowCols:7,windowRows:7,windowScale:[.30,.30],edgeOpacity:.14
  }).reflectionSources);
  sources.push(...addFacade(fill,strokes,{
    x:11.8,z:-17,w:9,h:11,d:5,color:P.wall,white:P.white,roof:'setback',
    litDensity:.32,seed:42,windowCols:6,windowRows:6,windowScale:[.28,.30],edgeOpacity:.17
  }).reflectionSources);

  // Low terminal volume with a long glowing horizontal band.
  box(fill,[13,4.2,4.2],[0,2.1,-13.4],P.deep);
  panel(fill,[10.2,.48],[0,3.45,-11.27],P.white,.42);
  strokeSegments(strokes,[
    [-6.5,.12,-11.25,-6.5,4.2,-11.25],
    [-6.5,4.2,-11.25,6.5,4.2,-11.25],
    [6.5,4.2,-11.25,6.5,.2,-11.25]
  ],{color:P.white,width:1.55,opacity:.48});
  sources.push({x:0,z:-11.27,width:4.3,intensity:.55,color:'white'});

  addVectorProp(fill,'street.awning',{position:[-1.0,2.7,-11.20],scale:.018,strokeScale:.82});
  addVectorProp(fill,'street.phone-booth',{position:[-5.2,.02,-8.4],scale:.0108,strokeScale:.86});
  addVectorProp(fill,'street.bench',{position:[3.8,.02,-8.6],scale:.0115,strokeScale:.84});
  addVectorProp(fill,'street.trash-can',{position:[6.2,.02,-8.3],scale:.0090,strokeScale:.80});
  addVectorProp(fill,'street.sign',{position:[-7.8,.02,-5.1],scale:.0104,strokeScale:.86});
  addVectorProp(fill,'nature.tree-column',{position:[7.5,.02,-4.7],scale:.0102,strokeScale:.74});

  addVectorProp(fill,'vehicle.sedan',{position:[5.7,.02,4.2],scale:.0105,rotationY:-.10,strokeScale:.78});
  sources.push({x:5.7,z:4.2,width:.48,intensity:.18,color:'gold'});

  addPuppetCharacter(fill,{kind:'detective-walk',position:[1.6,.02,5.8],scale:.0113,strokeScale:.84});
  addPuppetCharacter(fill,{kind:'worker',position:[-3.6,.02,-6.8],scale:.0060,strokeScale:.82});
  addPuppetCharacter(fill,{kind:'dress',position:[2.4,.02,-7.2],scale:.0057,mirror:true,strokeScale:.82});
  addPuppetCharacter(fill,{kind:'shortcoat',position:[5.0,.02,-9.6],scale:.0042,strokeScale:.78});
  addPuppetCharacter(fill,{kind:'detective-turn',position:[-1.2,.02,-9.8],scale:.0040,mirror:true,strokeScale:.76});

  sources.push(lamp(fill,strokes,glow,-6.5,2.2,5.9));
  sources.push(lamp(fill,strokes,glow,6.4,-1.8,6.1));

  addReflectionField(glow,{
    sources,bounds:{minX:-20,maxX:20,minZ:-25,maxZ:15},opacity:.28
  });

  // Terminal platform and curb lines frame the street without outlining the whole ground.
  strokeSegments(strokes,[
    [-8.5,.03,-5.2,8.5,.03,-5.2],
    [-8.5,.03,-5.6,-8.5,.03,7.5],
    [8.5,.03,-5.6,8.5,.03,7.5]
  ],{color:P.white,width:1.0,opacity:.18});

  const arc=[];
  for(let i=0;i<=32;i++){
    const a=.25+Math.PI*1.2*(i/32);
    arc.push([-3.8+Math.cos(a)*1.25,11.8+Math.sin(a)*1.25,-28.8]);
  }
  strokePath(strokes,arc,{color:P.gold,width:2.3,opacity:.72});

  return {
    root,fill,strokes,glow,
    setVisible(visible){root.visible=visible;},
    setMode(mode){
      fill.visible=true;strokes.visible=true;glow.visible=true;
      if(mode==='shape'){strokes.visible=false;glow.visible=false;}
      if(mode==='line'){fill.visible=false;glow.visible=false;}
    },
    resize(width,height){resizeVectorStrokes(width,height);}
  };
}
