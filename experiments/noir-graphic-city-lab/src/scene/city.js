import * as THREE from 'three';
import { Line2 } from 'three/addons/lines/Line2.js';
import { LineGeometry } from 'three/addons/lines/LineGeometry.js';
import { LineMaterial } from 'three/addons/lines/LineMaterial.js';
import { createCharacter } from './characters.js';

function meshMat(color, extra = {}) {
  return new THREE.MeshBasicMaterial({ color, ...extra });
}

function addEdges(mesh, group, material, threshold = 26) {
  const edge = new THREE.LineSegments(new THREE.EdgesGeometry(mesh.geometry, threshold), material);
  edge.position.copy(mesh.position);
  edge.quaternion.copy(mesh.quaternion);
  edge.scale.copy(mesh.scale);
  group.add(edge);
  return edge;
}

function strokeMaterial(color, opacity, width) {
  return new LineMaterial({
    color,
    transparent: true,
    opacity,
    linewidth: width,
    worldUnits: false,
    depthWrite: false,
    depthTest: true,
    alphaToCoverage: true,
  });
}

function addStroke(parent, material, points, closed = false) {
  const coords = [];
  for (const point of points) coords.push(...point);
  if (closed && points.length) coords.push(...points[0]);
  const geometry = new LineGeometry();
  geometry.setPositions(coords);
  const line = new Line2(geometry, material);
  line.computeLineDistances();
  parent.add(line);
  return line;
}

function addBox({ parent, lines, materials, x, y, z, w, h, d, tone='surface', edge='secondary' }) {
  const mesh = new THREE.Mesh(new THREE.BoxGeometry(w,h,d), materials[tone]);
  mesh.position.set(x,y,z);
  parent.add(mesh);
  if (edge) addEdges(mesh,lines,edge==='primary'?materials.edgePrimary:materials.edgeSecondary,28);
  return mesh;
}

function addWindowGridFront({ parent, x, y, z, cols, rows, dx, dy, w, h, material, off=()=>false }) {
  const group = new THREE.Group();
  for (let iy=0; iy<rows; iy++) {
    for (let ix=0; ix<cols; ix++) {
      if (off(ix,iy)) continue;
      const m = new THREE.Mesh(new THREE.PlaneGeometry(w,h),material);
      m.position.set(x + (ix-(cols-1)/2)*dx, y + iy*dy, z);
      group.add(m);
    }
  }
  parent.add(group);
  return group;
}

function makeSurfaceTexture(seed = 1337) {
  const canvas=document.createElement('canvas');
  canvas.width=160;
  canvas.height=160;
  const ctx=canvas.getContext('2d');
  ctx.fillStyle='rgb(236,236,236)';
  ctx.fillRect(0,0,160,160);

  let state=seed;
  const rand=()=>{state=(state*16807)%2147483647;return(state-1)/2147483646;};

  for(let i=0;i<3400;i++){
    const v=Math.floor(188+rand()*62);
    const a=.10+rand()*.24;
    ctx.fillStyle=`rgba(${v},${v},${v},${a})`;
    const size=rand()>.94?2:1;
    ctx.fillRect(Math.floor(rand()*160),Math.floor(rand()*160),size,size);
  }

  ctx.lineWidth=1;
  for(let i=0;i<32;i++){
    const v=Math.floor(205+rand()*32);
    ctx.strokeStyle=`rgba(${v},${v},${v},${.10+rand()*.10})`;
    const y=rand()*160;
    ctx.beginPath();
    ctx.moveTo(rand()*40,y);
    ctx.lineTo(75+rand()*85,y+(rand()-.5)*2);
    ctx.stroke();
  }

  const texture=new THREE.CanvasTexture(canvas);
  texture.wrapS=THREE.RepeatWrapping;
  texture.wrapT=THREE.RepeatWrapping;
  texture.repeat.set(4,4);
  texture.minFilter=THREE.LinearMipmapLinearFilter;
  texture.magFilter=THREE.LinearFilter;
  return texture;
}

function makeTextTexture(text) {
  const canvas=document.createElement('canvas');
  canvas.width=768;
  canvas.height=128;
  const ctx=canvas.getContext('2d');
  ctx.clearRect(0,0,canvas.width,canvas.height);
  ctx.fillStyle='rgba(9,13,24,0.96)';
  ctx.fillRect(0,0,canvas.width,canvas.height);
  ctx.strokeStyle='rgba(232,229,220,0.85)';
  ctx.lineWidth=3;
  ctx.strokeRect(5,5,canvas.width-10,canvas.height-10);
  ctx.fillStyle='#e8e5dc';
  ctx.font='34px Georgia, serif';
  ctx.textAlign='center';
  ctx.textBaseline='middle';
  ctx.fillText(text,canvas.width/2,canvas.height/2+2);
  const tex=new THREE.CanvasTexture(canvas);
  tex.colorSpace=THREE.SRGBColorSpace;
  tex.minFilter=THREE.LinearFilter;
  tex.magFilter=THREE.LinearFilter;
  return tex;
}

function makeWetPatchMaterial() {
  return new THREE.ShaderMaterial({
    transparent:true,
    depthWrite:false,
    side:THREE.DoubleSide,
    uniforms:{uColor:{value:new THREE.Color(0x8b96a8)}},
    vertexShader:`
      varying vec2 vUv;
      void main(){vUv=uv;gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.0);}
    `,
    fragmentShader:`
      uniform vec3 uColor;
      varying vec2 vUv;
      float hash(vec2 p){
        p=fract(p*vec2(127.1,311.7));
        p+=dot(p,p+34.5);
        return fract(p.x*p.y);
      }
      void main(){
        vec2 cell=floor(vUv*vec2(72.0,24.0));
        float n=hash(cell);
        float streak=.5+.5*sin(vUv.y*95.0+hash(floor(vUv.xx*19.0))*6.283);
        float edge=smoothstep(0.0,.16,vUv.x)*smoothstep(0.0,.14,1.0-vUv.x)*smoothstep(0.0,.12,vUv.y)*smoothstep(0.0,.12,1.0-vUv.y);
        float alpha=(.045+.17*smoothstep(.48,.92,n)*(.45+.55*streak))*edge;
        gl_FragColor=vec4(uColor,alpha);
      }
    `,
  });
}

function makeSoftTexture() {
  const canvas=document.createElement('canvas');
  canvas.width=256;
  canvas.height=256;
  const ctx=canvas.getContext('2d');
  const g=ctx.createRadialGradient(128,128,0,128,128,128);
  g.addColorStop(0,'rgba(235,238,240,0.85)');
  g.addColorStop(.32,'rgba(205,210,218,0.38)');
  g.addColorStop(.72,'rgba(160,168,180,0.12)');
  g.addColorStop(1,'rgba(120,130,145,0)');
  ctx.fillStyle=g;
  ctx.fillRect(0,0,256,256);
  const tex=new THREE.CanvasTexture(canvas);
  tex.colorSpace=THREE.SRGBColorSpace;
  return tex;
}

function addOfficeTower(groups,materials) {
  addBox({
    parent:groups.solids,lines:groups.edges,materials,
    x:-20,y:18,z:-38,w:18,h:36,d:10,tone:'surfaceLift',edge:'secondary',
  });

  addWindowGridFront({
    parent:groups.emissive,x:-20,y:2.6,z:-32.96,
    cols:11,rows:14,dx:1.34,dy:2.15,w:.50,h:.76,material:materials.window,
    off:(x,y)=>((x*7+y*11)%17===0)||((x+y)%19===0),
  });

  // Structural facade drawing.
  for (let i=0;i<=11;i++) {
    const x=-20+(i-5.5)*1.34;
    addStroke(groups.strokes,materials.strokeDim,[[x,1.2,-32.90],[x,34.6,-32.90]]);
  }
  for (let j=0;j<=14;j+=2) {
    const y=1.8+j*2.15;
    addStroke(groups.strokes,materials.strokeDim,[[-27.6,y,-32.89],[-12.4,y,-32.89]]);
  }

  addBox({
    parent:groups.solids,lines:groups.edges,materials,
    x:-7.8,y:12,z:-44,w:7,h:24,d:7,tone:'surface',edge:'secondary',
  });
  addStroke(groups.strokes,materials.strokePrimary,[[-11.3,24,-40.45],[-11.3,28,-40.45],[-4.3,28,-40.45]]);
}

function addDiner(groups,materials) {
  addBox({
    parent:groups.solids,lines:groups.edges,materials,
    x:-2.4,y:4,z:-20,w:25,h:8,d:8,tone:'surfaceMid',edge:'primary',
  });
  addBox({
    parent:groups.solids,lines:groups.edges,materials,
    x:-2.4,y:5.0,z:-14.9,w:13.2,h:.54,d:2.4,tone:'surfaceLift',edge:'primary',
  });

  // Window band under the awning.
  for (let i=0;i<6;i++) {
    const win=new THREE.Mesh(new THREE.PlaneGeometry(1.65,2.35),materials.windowDark);
    win.position.set(-7.5+i*2.05,2.25,-15.96);
    groups.solids.add(win);
    addStroke(groups.strokes,materials.strokePrimary,[
      [-8.34+i*2.05,1.05,-15.93],[-8.34+i*2.05,3.45,-15.93]
    ]);
  }
  addStroke(groups.strokes,materials.strokePrimary,[[-8.45,3.52,-15.93],[4.55,3.52,-15.93]]);

  const sign=new THREE.Mesh(
    new THREE.PlaneGeometry(7.2,1.22),
    new THREE.MeshBasicMaterial({map:makeTextTexture('THE ORIOLE'),transparent:true,depthWrite:false}),
  );
  sign.position.set(-2.4,6.15,-15.98);
  groups.emissive.add(sign);

  // Entry steps and rails.
  for (let i=0;i<3;i++) {
    addStroke(groups.strokes,materials.strokeDim,[
      [-5.4+i*.35,.06,-14.3+i*.48],[.8+i*.35,.06,-14.3+i*.48]
    ]);
  }
  addStroke(groups.strokes,materials.strokePrimary,[[-5.4,.2,-14.1],[-5.4,2.0,-14.1],[-3.8,2.0,-14.1]]);
}

function addRightArchitecture(groups,materials) {
  addBox({
    parent:groups.solids,lines:groups.edges,materials,
    x:17,y:8,z:-15,w:13,h:16,d:12,tone:'surface',edge:'primary',
  });
  addBox({
    parent:groups.solids,lines:groups.edges,materials,
    x:19,y:5,z:10,w:12,h:10,d:17,tone:'surfaceLift',edge:'primary',
  });

  // Intentional facade linework and balcony.
  for (let y=1.4;y<13;y+=2.2) {
    addStroke(groups.strokes,materials.strokeDim,[[10.45,y,-19.8],[10.45,y,-10.2]]);
  }
  for (let z=-18;z<-10;z+=2.0) {
    addStroke(groups.strokes,materials.strokeDim,[[10.44,1.0,z],[10.44,12.4,z]]);
  }

  addStroke(groups.strokes,materials.strokePrimary,[[12.1,6.8,-5],[12.1,6.8,4],[16.8,6.8,4]]);
  for(let z=-4;z<=4;z+=1.5){
    addStroke(groups.strokes,materials.strokeDim,[[12.1,.25,z],[12.1,5.8,z]]);
  }

  // A simple exterior stair/fire escape.
  for(let i=0;i<5;i++){
    const y=.8+i*.85;
    addStroke(groups.strokes,materials.strokeDim,[[12.2,y,7.5],[16.3,y+0.7,7.5]]);
  }
  addStroke(groups.strokes,materials.strokePrimary,[[12.0,.2,7.5],[16.5,4.7,7.5]]);
}

function addCityDrawingDetails(groups,materials) {
  // Overhead utility lines create long, clean gestures across otherwise empty sky.
  addStroke(groups.strokes,materials.strokeDim,[
    [-30,16.2,-22],[-18,17.4,-18],[-5,17.0,-12],[9,16.1,-6],[25,15.5,-1]
  ]);
  addStroke(groups.strokes,materials.strokeDim,[
    [-28,15.5,-18],[-13,16.0,-13],[2,15.3,-8],[18,14.8,-3]
  ]);

  // Rooftop circular sign.
  const ring=[];
  const cx=6.3,cy=9.4,cz=-19.3,r=.72;
  for(let i=0;i<28;i++){
    const a=i/28*Math.PI*2;
    ring.push([cx+Math.cos(a)*r,cy+Math.sin(a)*r,cz]);
  }
  addStroke(groups.strokes,materials.strokePrimary,ring,true);
  addStroke(groups.strokes,materials.strokeDim,[[cx-.38,8.72,cz],[cx-.38,8.1,cz],[cx+.38,8.1,cz],[cx+.38,8.72,cz]]);

  // Diner interior: counter and stools, visible as drawing rather than geometry clutter.
  addStroke(groups.strokes,materials.strokePrimary,[[-7.7,1.72,-15.88],[3.0,1.72,-15.88]]);
  for(const x of [-6.2,-3.8,-1.4,1.0]){
    addStroke(groups.strokes,materials.strokeDim,[[x,.55,-15.87],[x,1.24,-15.87]]);
    addStroke(groups.strokes,materials.strokeDim,[[x-.30,1.24,-15.87],[x+.30,1.24,-15.87]]);
  }

  // Side-mounted luminous panel on the theater block.
  const sideSign=new THREE.Mesh(new THREE.PlaneGeometry(5.4,1.05),materials.sideSign);
  sideSign.position.set(10.42,5.1,-15.0);
  sideSign.rotation.y=-Math.PI/2;
  groups.emissive.add(sideSign);
  addStroke(groups.strokes,materials.strokePrimary,[[10.40,4.55,-17.7],[10.40,5.65,-17.7],[10.40,5.65,-12.3],[10.40,4.55,-12.3],[10.40,4.55,-17.7]]);

  // Antennas and roof machinery.
  addStroke(groups.strokes,materials.strokeDim,[[-7.8,24,-40.4],[-7.8,28.5,-40.4]]);
  addStroke(groups.strokes,materials.strokeDim,[[-8.6,27.1,-40.4],[-7.8,28.5,-40.4],[-7.0,27.1,-40.4]]);
}

function addGround(groups,materials) {
  const ground=new THREE.Mesh(new THREE.PlaneGeometry(48,112),materials.ground);
  ground.rotation.x=-Math.PI/2;
  ground.position.set(0,0,8);
  groups.solids.add(ground);

  // Designed perspective seams.
  for(const x of [-12,-7,-2,3,8,13]){
    addStroke(groups.strokes,materials.strokeDim,[[x,.025,-28],[x*1.35,.025,50]]);
  }
  for(let z=-25;z<=47;z+=6.5){
    addStroke(groups.strokes,materials.strokeDim,[[-14,.026,z],[14,.026,z+.22]]);
  }

  // A foreground curb and rail to frame the shot.
  addStroke(groups.strokes,materials.strokePrimary,[[-14,.15,31],[-12.5,.35,22],[-11.8,.35,12]]);
  for(let z=13;z<30;z+=3.4){
    addStroke(groups.strokes,materials.strokeDim,[[-12.2,.2,z],[-12.2,1.6,z]]);
  }
  addStroke(groups.strokes,materials.strokePrimary,[[-12.2,1.6,12],[-12.2,1.6,30]]);

  // Wet reflections as restrained long shapes.
  for(const [x,z,w,d,o] of [
    [-5,2,6.8,.9,.12],[2,8,4.7,.65,.10],[-1,16,8.4,1.0,.105],[6,26,4.6,.75,.08],[-7,34,4.0,.6,.07]
  ]){
    const p=new THREE.Mesh(new THREE.PlaneGeometry(w,d),materials.reflection.clone());
    p.material.opacity=o;
    p.rotation.x=-Math.PI/2;
    p.position.set(x,.03,z);
    groups.atmosphere.add(p);
  }

  // Broad, textured wet areas create a readable mid-value plane without flattening the whole road.
  const wetMaterial=makeWetPatchMaterial();
  for(const [x,z,w,d,r] of [
    [-5.8,15.5,12.5,20.0,-.06],
    [-8.8,31.5,7.5,13.0,.08],
    [5.4,7.0,5.0,9.0,.04]
  ]){
    const patch=new THREE.Mesh(new THREE.PlaneGeometry(w,d),wetMaterial);
    patch.rotation.set(-Math.PI/2,0,r);
    patch.position.set(x,.032,z);
    groups.atmosphere.add(patch);
  }

  // Localized grass, kept near curbs.
  const verts=[];
  let seed=37;
  const rand=()=>{seed=(seed*16807)%2147483647;return(seed-1)/2147483646;};
  for(let i=0;i<150;i++){
    const side=rand()>.48?1:-1;
    const x=side*(9.6+rand()*3.3);
    const z=-12+rand()*58;
    const h=.18+rand()*.78;
    verts.push(x,.035,z,x+(rand()-.5)*.22,h,z+(rand()-.5)*.14);
  }
  const geo=new THREE.BufferGeometry();
  geo.setAttribute('position',new THREE.Float32BufferAttribute(verts,3));
  groups.edges.add(new THREE.LineSegments(geo,materials.edgeSecondary));
}

function addStreetFurniture(groups,materials) {
  for(const [x,z] of [[-8,-6],[8,-2],[-8,13],[8,24]]){
    const post=new THREE.Mesh(new THREE.CylinderGeometry(.045,.06,4.4,8),materials.surface);
    post.position.set(x,2.2,z);
    groups.solids.add(post);
    addStroke(groups.strokes,materials.strokeDim,[[x,.08,z],[x,4.32,z],[x+(x<0?.82:-.82),4.32,z]]);
    const bulb=new THREE.Mesh(new THREE.SphereGeometry(.11,12,8),materials.white);
    bulb.position.set(x+(x<0?.85:-.85),4.28,z);
    groups.emissive.add(bulb);
  }

  addBox({
    parent:groups.solids,lines:groups.edges,materials,
    x:8.8,y:.8,z:8.8,w:1.25,h:1.6,d:1.25,tone:'surfaceLift',edge:'primary',
  });

  // Small bench at left midground.
  addBox({
    parent:groups.solids,lines:groups.edges,materials,
    x:-8.7,y:.55,z:3.5,w:3.3,h:.18,d:.75,tone:'surfaceLift',edge:'secondary',
  });
  for(const x of [-9.9,-7.5]) addStroke(groups.strokes,materials.strokeDim,[[x,.05,3.5],[x,.6,3.5]]);
}

function addAtmosphere(groups,materials) {
  const moon=new THREE.Mesh(new THREE.CircleGeometry(5.4,64),materials.hazeDisc);
  moon.position.set(-8.2,9.0,-26.0);
  groups.atmosphere.add(moon);

  const smokeTexture=makeSoftTexture();
  for(const [x,y,z,s,o] of [
    [-9.5,2.1,-2.0,7.2,.24],[-7.6,3.3,-4.2,5.8,.18],[-1.8,4.7,-17.0,4.8,.10]
  ]){
    const sprite=new THREE.Sprite(new THREE.SpriteMaterial({
      map:smokeTexture,color:0xc5cad2,transparent:true,opacity:o,
      depthWrite:false,depthTest:true,
    }));
    sprite.position.set(x,y,z);
    sprite.scale.set(s,s*.78,1);
    groups.atmosphere.add(sprite);
  }

  const source=new THREE.Vector3(-15,25,3);
  const target=new THREE.Vector3(-4.8,.4,-8.2);
  const direction=target.clone().sub(source);
  const length=direction.length();
  const beam=new THREE.Mesh(new THREE.ConeGeometry(4.7,length,48,1,true),materials.beam);
  beam.position.copy(source).add(target).multiplyScalar(.5);
  beam.quaternion.setFromUnitVectors(new THREE.Vector3(0,-1,0),direction.clone().normalize());
  groups.atmosphere.add(beam);

  const pool=new THREE.Mesh(new THREE.CircleGeometry(5.0,64),materials.pool);
  pool.rotation.x=-Math.PI/2;
  pool.scale.set(1.45,.62,1);
  pool.position.set(target.x,.04,target.z);
  groups.atmosphere.add(pool);
}

function addCast(groups,materials) {
  createCharacter({
    name:'Detective',position:[9.4,0,22.5],yaw:0,scale:1.04,
    pose:'neutral',kind:'hero',materials,parent:groups.characters,
  });

  const crowd=[
    [-7.2,-6.8,.88,0],[-4.3,-7.7,.94,0],[-1.2,-6.8,.86,0],
    [-8.0,-2.4,.80,0],[-5.0,-1.8,.96,0],[-2.1,-2.5,.82,0],
    [1.0,-4.0,.76,0],
  ];
  crowd.forEach(([x,z,s,yaw],i)=>createCharacter({
    name:`Crowd ${i+1}`,position:[x,0,z],yaw,scale:s,
    pose:i%3===0?'gesture':i%2?'walk':'neutral',
    kind:'crowd',variant:i,materials,parent:groups.characters,
  }));
}

export function buildWorld(scene,profile){
  const groups={
    solids:new THREE.Group(),
    edges:new THREE.Group(),
    strokes:new THREE.Group(),
    emissive:new THREE.Group(),
    atmosphere:new THREE.Group(),
    characters:new THREE.Group(),
  };
  Object.values(groups).forEach(g=>scene.add(g));

  const surfaceTexture=makeSurfaceTexture(1948);
  const groundTexture=surfaceTexture.clone();
  groundTexture.repeat.set(8,18);
  groundTexture.needsUpdate=true;

  const materials={
    surface:meshMat(profile.palette.surface,{map:surfaceTexture}),
    surfaceLift:meshMat(profile.palette.surfaceLift,{map:surfaceTexture}),
    surfaceMid:meshMat(profile.palette.surfaceMid,{map:surfaceTexture}),
    ground:meshMat(0x202c41,{map:groundTexture}),
    character:meshMat(0x010204,{side:THREE.DoubleSide}),
    detail:meshMat(profile.palette.lineDim,{side:THREE.DoubleSide}),
    edgePrimary:new THREE.LineBasicMaterial({
      color:profile.palette.line,transparent:true,opacity:.74,fog:true,
    }),
    edgeSecondary:new THREE.LineBasicMaterial({
      color:profile.palette.lineDim,transparent:true,opacity:.42,fog:true,
    }),
    strokePrimary:strokeMaterial(profile.palette.line,.92,1.02),
    strokeDim:strokeMaterial(profile.palette.lineDim,.62,.76),
    white:meshMat(profile.palette.white),
    window:meshMat(profile.palette.white),
    windowDark:meshMat(0x697587),
    sideSign:meshMat(0x9da4ad),
    reflection:meshMat(profile.palette.lineDim,{transparent:true,opacity:.1,depthWrite:false}),
    hazeDisc:meshMat(0xc5c9d0,{transparent:true,opacity:.31,depthWrite:false,side:THREE.DoubleSide}),
    beam:meshMat(profile.palette.white,{
      transparent:true,opacity:.026,side:THREE.DoubleSide,depthWrite:false,blending:THREE.AdditiveBlending,
    }),
    pool:meshMat(profile.palette.white,{
      transparent:true,opacity:.075,side:THREE.DoubleSide,depthWrite:false,blending:THREE.AdditiveBlending,
    }),
  };

  addGround(groups,materials);
  addOfficeTower(groups,materials);
  addDiner(groups,materials);
  addRightArchitecture(groups,materials);
  addCityDrawingDetails(groups,materials);
  addStreetFurniture(groups,materials);
  addAtmosphere(groups,materials);
  addCast(groups,materials);

  return {groups,materials};
}
