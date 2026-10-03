import * as THREE from 'three';
import { polyline, segments, rect, resizeLineMaterials } from '../style/lineArt.js';

const P={
  void:0x030409, ink:0x010205, deep:0x080b12, wall:0x0d1119, wall2:0x121722,
  white:0xf2efe6, dim:0x8b919a, gold:0xe1b53d
};

const basic=(color,opts={})=>new THREE.MeshBasicMaterial({color,toneMapped:false,...opts});
const standard=(color,opts={})=>new THREE.MeshStandardMaterial({
  color,roughness:.88,metalness:.02,...opts
});

function box(parent,size,pos,mat,rotY=0){
  const m=new THREE.Mesh(new THREE.BoxGeometry(...size),mat);
  m.position.set(...pos);m.rotation.y=rotY;
  m.castShadow=true;m.receiveShadow=true;parent.add(m);return m;
}

function panel(parent,size,pos,color=P.white,opacity=1){
  const m=new THREE.Mesh(new THREE.PlaneGeometry(...size),basic(color,{
    transparent:opacity<1,opacity,side:THREE.DoubleSide,depthWrite:opacity===1
  }));
  m.position.set(...pos);m.renderOrder=6;parent.add(m);return m;
}

function canvasTexture(draw,w=512,h=128){
  const c=document.createElement('canvas');c.width=w;c.height=h;
  const ctx=c.getContext('2d');draw(ctx,w,h);
  const t=new THREE.CanvasTexture(c);t.colorSpace=THREE.SRGBColorSpace;return t;
}

function haloTexture(){
  return canvasTexture((ctx,w,h)=>{
    const g=ctx.createRadialGradient(w/2,h/2,0,w/2,h/2,w/2);
    g.addColorStop(0,'rgba(255,250,230,.92)');
    g.addColorStop(.08,'rgba(255,250,230,.50)');
    g.addColorStop(.32,'rgba(255,250,230,.15)');
    g.addColorStop(1,'rgba(255,250,230,0)');
    ctx.fillStyle=g;ctx.fillRect(0,0,w,h);
  },256,256);
}

function mistTexture(){
  return canvasTexture((ctx,w,h)=>{
    const g=ctx.createRadialGradient(w*.5,h*.5,5,w*.5,h*.5,w*.5);
    g.addColorStop(0,'rgba(235,238,242,.13)');
    g.addColorStop(.55,'rgba(210,215,222,.05)');
    g.addColorStop(1,'rgba(200,205,215,0)');
    ctx.fillStyle=g;ctx.fillRect(0,0,w,h);
  },512,256);
}

function textPanel(parent,text,size,pos,{color=P.white,font='600 42px Georgia'}={}){
  const tex=canvasTexture((ctx,w,h)=>{
    ctx.clearRect(0,0,w,h);ctx.fillStyle='#f4f0e6';ctx.font=font;
    ctx.textAlign='center';ctx.textBaseline='middle';ctx.fillText(text,w/2,h/2);
  });
  const m=new THREE.Mesh(new THREE.PlaneGeometry(...size),new THREE.MeshBasicMaterial({
    map:tex,transparent:true,toneMapped:false,depthWrite:false
  }));
  m.position.set(...pos);m.renderOrder=9;parent.add(m);return m;
}

function addFacade(fill,lines,{x,y=0,z,w,h,d=3.5,bays=4,floors=4,color=P.wall,lit=[]}){
  box(fill,[w,h,d],[x,y+h/2,z],standard(color));
  const front=z+d/2+.025;
  rect(lines,x,y+h/2,front,w,h,{color:P.white,width:2.2,opacity:.72});
  segments(lines,[
    [x-w/2,y+h*.90,front+.01,x+w/2,y+h*.90,front+.01],
    [x-w/2,y+.22,front+.01,x+w/2,y+.22,front+.01]
  ],{color:P.white,width:2.7,opacity:.75});
  const dx=w/(bays+1),dy=h/(floors+1);
  const minor=[];
  for(let f=0;f<floors;f++){
    for(let b=0;b<bays;b++){
      const wx=x-w/2+dx*(b+1),wy=y+dy*(f+1),idx=f*bays+b;
      if(lit.includes(idx)){
        panel(fill,[dx*.46,dy*.42],[wx,wy,front+.02],P.white,.96);
      }else{
        rect(lines,wx,wy,front+.015,dx*.42,dy*.36,{color:P.white,width:1.0,opacity:.30});
      }
    }
  }
  for(let f=1;f<floors;f++){
    const yy=y+h*f/floors;
    minor.push([x-w/2,yy,front,x+w/2,yy,front]);
  }
  segments(lines,minor,{color:P.dim,width:.8,opacity:.16});
}

function addCafe(fill,lines,glow){
  box(fill,[12,4.6,5.4],[0,2.3,-16],standard(P.deep));
  const z=-13.27;
  rect(lines,0,2.3,z,12,4.6,{color:P.white,width:2.8,opacity:.95});
  panel(fill,[9.2,.92],[0,3.58,z+.02],P.white,.98);
  panel(fill,[2.2,2.0],[-3.4,1.55,z+.025],P.white,.82);
  panel(fill,[2.7,1.72],[2.8,1.62,z+.025],P.white,.90);
  rect(lines,-3.4,1.55,z+.04,2.2,2.0,{color:P.white,width:1.8,opacity:.72});
  rect(lines,2.8,1.62,z+.04,2.7,1.72,{color:P.white,width:1.8,opacity:.72});
  textPanel(fill,'NIGHT CAFE',[4.2,.72],[0,4.72,z+.06],{font:'600 38px Georgia'});
  const halo=new THREE.Sprite(new THREE.SpriteMaterial({
    map:haloTexture(),color:P.white,transparent:true,opacity:.17,depthWrite:false,
    blending:THREE.AdditiveBlending
  }));
  halo.position.set(0,3.0,-12.9);halo.scale.set(13,7,1);glow.add(halo);
}

function figureShape(profile=false){
  return profile
    ? [[-.50,.16],[-.46,1.28],[-.38,2.02],[-.50,2.36],[-.31,2.62],[-.15,2.75],[-.10,2.90],[.08,3.07],[.27,3.08],[.40,2.98],[.39,2.87],[.29,2.78],[.43,2.61],[.56,2.30],[.44,2.02],[.47,1.25],[.52,.16]]
    : [[-.60,.16],[-.54,1.25],[-.44,2.05],[-.56,2.34],[-.35,2.61],[-.18,2.76],[.18,2.76],[.35,2.61],[.56,2.34],[.44,2.05],[.54,1.25],[.60,.16]];
}

function addFigure(fill,lines,{x=0,y=0,z=0,s=1,profile=false,gold=false,flip=false}={}){
  const pts=figureShape(profile).map(([a,b])=>[flip?-a:a,b]);
  const g=new THREE.Group();g.position.set(x,y,z);g.scale.setScalar(s);fill.add(g);
  const lg=new THREE.Group();lg.position.copy(g.position);lg.scale.copy(g.scale);lines.add(lg);
  const shape=new THREE.Shape();shape.moveTo(...pts[0]);
  pts.slice(1).forEach(p=>shape.lineTo(...p));shape.closePath();
  const body=new THREE.Mesh(new THREE.ShapeGeometry(shape),basic(P.ink,{side:THREE.DoubleSide}));
  g.add(body);
  polyline(lg,pts.map(([a,b])=>[a,b,.018]),{
    color:gold?P.gold:P.white,width:3.1,opacity:.98,closed:true
  });
  segments(lg,[[-.46,2.78,.025,.46,2.78,.025]],{color:P.white,width:3.2,opacity:.96});
  polyline(lg,[[-.27,2.80,.025],[-.22,3.08,.025],[.20,3.08,.025],[.28,2.80,.025]],{
    color:P.white,width:2.5,opacity:.94,closed:true
  });
  if(profile){
    const sign=flip?-1:1;
    polyline(lg,[[.10*sign,2.91,.03],[.28*sign,2.91,.03],[.37*sign,2.85,.03]],{
      color:P.white,width:1.5,opacity:.66
    });
  }
  segments(lg,[[0,.16,.02,0,.74,.02]],{color:P.white,width:1.4,opacity:.42});
}

function addLamp(fill,lines,glow,x,z,h=6.4){
  segments(lines,[
    [x,0,z,x,h,z],[x,h,z,x+.78,h,z]
  ],{color:P.white,width:2.2,opacity:.72});
  panel(fill,[.30,.13],[x+.78,h-.03,z+.02],P.white,1);
  const halo=new THREE.Sprite(new THREE.SpriteMaterial({
    map:haloTexture(),color:P.white,transparent:true,opacity:.55,depthWrite:false,
    blending:THREE.AdditiveBlending
  }));
  halo.position.set(x+.78,h-.04,z+.2);halo.scale.set(3.5,3.5,1);glow.add(halo);
}

function addWetStreet(lines){
  let seed=39281;
  const rnd=()=>((seed=Math.imul(seed,1664525)+1013904223>>>0)/4294967296);
  const white=[],gold=[];
  for(let i=0;i<120;i++){
    const x=-17+rnd()*34,z=-1+rnd()*23,len=.25+rnd()*1.65;
    white.push([x,.035,z,x+len,.035,z+(rnd()-.5)*.10]);
  }
  for(let i=0;i<22;i++){
    const x=-8+rnd()*16,z=0+rnd()*16,len=.18+rnd()*1.0;
    gold.push([x,.04,z,x+len,.04,z+(rnd()-.5)*.07]);
  }
  segments(lines,white,{color:P.white,width:.75,opacity:.16});
  segments(lines,gold,{color:P.gold,width:1.0,opacity:.25});
}

function addRain(lines){
  let seed=1187;
  const rnd=()=>((seed=Math.imul(seed,1103515245)+12345>>>0)/4294967296);
  const rain=[];
  for(let i=0;i<170;i++){
    const x=-19+rnd()*38,y=.5+rnd()*14,z=-24+rnd()*38,l=.25+rnd()*.72;
    rain.push([x,y,z,x-.14,y-l,z+.04]);
  }
  segments(lines,rain,{color:P.white,width:.75,opacity:.12});
}

export function buildWorld(scene){
  const fill=new THREE.Group(),lines=new THREE.Group(),glow=new THREE.Group();
  scene.add(fill,lines,glow);

  const ground=box(fill,[42,.14,46],[0,-.10,-7],standard(P.ink));
  ground.receiveShadow=true;

  // Left and right street walls provide actual depth, but stay visually black.
  addFacade(fill,lines,{x:-12.0,z:-15,w:9,h:11,d:5,bays:3,floors:4,color:P.wall,lit:[1,7]});
  addFacade(fill,lines,{x:12.4,z:-18,w:10,h:14,d:5,bays:4,floors:5,color:P.wall2,lit:[2,13]});
  addFacade(fill,lines,{x:-13.5,z:-2,w:8,h:8,d:5,bays:3,floors:3,color:P.deep,lit:[4]});
  addFacade(fill,lines,{x:13.8,z:-4,w:8,h:9,d:5,bays:3,floors:3,color:P.deep,lit:[1,7]});
  addCafe(fill,lines,glow);

  // Perspective rails / curb lines intentionally define the stage-like street.
  segments(lines,[
    [-7.8,.03,12,-6.2,.03,-11],
    [7.8,.03,12,6.0,.03,-11],
    [-12,.04,-10,12,.04,-10]
  ],{color:P.white,width:1.7,opacity:.38});

  // Crosswalk is only a few graphic strokes.
  const cross=[];
  for(let i=-3;i<=3;i++) cross.push([i*1.25-.5,.045,3.0,i*1.25+.35,.045,3.0]);
  segments(lines,cross,{color:P.white,width:3.0,opacity:.62});

  addLamp(fill,lines,glow,-6.7,4.0,6.1);
  addLamp(fill,lines,glow,7.0,-4.5,6.5);

  addFigure(fill,lines,{x:-1.8,y:.02,z:4.5,s:1.42,profile:true});
  addFigure(fill,lines,{x:-8.2,y:.02,z:-5.6,s:.83,profile:false,flip:true});
  addFigure(fill,lines,{x:7.7,y:.02,z:-1.5,s:.72,profile:true,flip:true});

  // Gold is a sparse symbolic language, not a general material.
  const ring=new THREE.Mesh(new THREE.RingGeometry(3.7,3.88,96),basic(P.gold,{
    side:THREE.DoubleSide,transparent:true,opacity:.95
  }));
  ring.position.set(-9.6,13.5,-30);fill.add(ring);
  const dot=new THREE.Mesh(new THREE.CircleGeometry(.34,32),basic(P.gold));
  dot.position.set(-5.9,10.7,-29.9);fill.add(dot);

  // Thin gold trace on a taxi-like silhouette.
  box(fill,[4.1,1.0,1.8],[7.0,.58,5.3],basic(P.ink),-.22);
  rect(lines,7.0,1.05,6.22,4.0,1.1,{color:P.gold,width:2.4,opacity:.86});

  addWetStreet(lines);addRain(lines);

  // Localized fog/mist is sprite-based, never a solid cone.
  const mist=new THREE.Sprite(new THREE.SpriteMaterial({
    map:mistTexture(),transparent:true,opacity:.32,depthWrite:false
  }));
  mist.position.set(0,1.0,1.5);mist.scale.set(22,8,1);glow.add(mist);

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
