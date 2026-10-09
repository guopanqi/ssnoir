import * as THREE from 'three';
import { Line2 } from 'three/addons/lines/Line2.js';
import { LineGeometry } from 'three/addons/lines/LineGeometry.js';
import { LineMaterial } from 'three/addons/lines/LineMaterial.js';
import { Reflector } from 'three/addons/objects/Reflector.js';
import { createCharacter } from './characters.js';

function surfaceMat(color, fogColor, fogDensity, noiseScale = 9, noiseAmount = .12) {
  return new THREE.ShaderMaterial({
    uniforms:{
      uColor:{value:new THREE.Color(color)},
      uFogColor:{value:new THREE.Color(fogColor)},
      uFogDensity:{value:fogDensity},
      uNoiseScale:{value:noiseScale},
      uNoiseAmount:{value:noiseAmount},
    },
    vertexShader:`
      varying vec2 vUv;
      varying float vDepth;
      void main(){
        vUv=uv;
        vec4 mv=modelViewMatrix*vec4(position,1.0);
        vDepth=-mv.z;
        gl_Position=projectionMatrix*mv;
      }
    `,
    fragmentShader:`
      uniform vec3 uColor;
      uniform vec3 uFogColor;
      uniform float uFogDensity;
      uniform float uNoiseScale;
      uniform float uNoiseAmount;
      varying vec2 vUv;
      varying float vDepth;

      float hash(vec2 p){
        p=fract(p*vec2(123.34,456.21));
        p+=dot(p,p+45.32);
        return fract(p.x*p.y);
      }

      void main(){
        vec2 p=vUv*uNoiseScale;
        float fine=hash(floor(p*42.0));
        float broad=hash(floor(p*7.0));
        float scratch=step(.982,hash(vec2(floor(p.x*22.0),floor(p.y*4.0))));
        float grain=(fine-.5)*.65+(broad-.5)*.35;
        vec3 c=uColor*(1.0+grain*uNoiseAmount)+vec3(.035)*scratch;

        float fog=1.0-exp(-uFogDensity*uFogDensity*vDepth*vDepth);
        c=mix(c,uFogColor,clamp(fog,0.0,1.0));
        gl_FragColor=vec4(c,1.0);
      }
    `,
  });
}

function meshMat(color, extra = {}) {
  return new THREE.MeshBasicMaterial({ color, ...extra });
}


const WetReflectorShader = {
  name: 'GraphicWetReflector',
  uniforms: {
    color: { value: null },
    tDiffuse: { value: null },
    textureMatrix: { value: null },
  },
  vertexShader: `
    uniform mat4 textureMatrix;
    varying vec4 vReflectUv;
    varying vec2 vLocalUv;

    void main() {
      vLocalUv = uv;
      vReflectUv = textureMatrix * vec4(position, 1.0);
      gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
    }
  `,
  fragmentShader: `
    uniform vec3 color;
    uniform sampler2D tDiffuse;
    varying vec4 vReflectUv;
    varying vec2 vLocalUv;

    float hash(vec2 p) {
      p = fract(p * vec2(123.34, 456.21));
      p += dot(p, p + 45.32);
      return fract(p.x * p.y);
    }

    float valueNoise(vec2 p) {
      vec2 i = floor(p);
      vec2 f = fract(p);
      f = f * f * (3.0 - 2.0 * f);
      float a = hash(i);
      float b = hash(i + vec2(1.0, 0.0));
      float c = hash(i + vec2(0.0, 1.0));
      float d = hash(i + vec2(1.0, 1.0));
      return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
    }

    void main() {
      vec3 reflected = texture2DProj(tDiffuse, vReflectUv).rgb;

      vec2 uv = vLocalUv;
      vec2 centered = abs(uv - 0.5);
      float edge = 1.0 - smoothstep(0.34, 0.50, max(centered.x, centered.y));

      float broad = valueNoise(uv * vec2(5.5, 13.0));
      float fine = valueNoise(uv * vec2(21.0, 48.0));
      float bands = 0.5 + 0.5 * sin(uv.y * 150.0 + fine * 5.0);
      float puddle = smoothstep(0.64, 0.84, broad * 0.70 + fine * 0.30);
      puddle *= mix(0.38, 1.0, smoothstep(0.58, 0.82, bands));
      puddle *= edge;

      float lum = dot(reflected, vec3(0.2126, 0.7152, 0.0722));
      vec3 tinted = mix(reflected, color, 0.54);
      tinted = mix(tinted, reflected, smoothstep(0.20, 0.78, lum));

      // Reflections stay graphic: bright windows/lamps survive; dark buildings
      // mostly disappear into the road.
      float alpha = puddle * (0.012 + smoothstep(0.08, 0.54, lum) * 0.34);
      gl_FragColor = vec4(tinted, alpha);
    }
  `,
};

function addPlanarWetReflection(groups) {
  const reflector = new Reflector(
    new THREE.PlaneGeometry(23.5, 67),
    {
      clipBias: 0.0025,
      textureWidth: 768,
      textureHeight: 432,
      color: 0x718096,
      multisample: 2,
      shader: WetReflectorShader,
    },
  );
  reflector.name = 'Graphic planar wet reflection';
  reflector.rotation.x = -Math.PI / 2;
  reflector.position.set(-1.2, 0.018, 8.5);
  reflector.material.transparent = true;
  reflector.material.depthWrite = false;
  reflector.material.depthTest = true;
  reflector.renderOrder = 1;
  groups.atmosphere.add(reflector);
  return reflector;
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

function addWindowGridSide({ parent, x, y, z, cols, rows, dz, dy, w, h, material, face = 'left', off=()=>false }) {
  const group=new THREE.Group();
  for(let iy=0;iy<rows;iy++){
    for(let iz=0;iz<cols;iz++){
      if(off(iz,iy)) continue;
      const m=new THREE.Mesh(new THREE.PlaneGeometry(w,h),material);
      m.position.set(x,y+iy*dy,z+(iz-(cols-1)/2)*dz);
      m.rotation.y=face==='left'?-Math.PI/2:Math.PI/2;
      group.add(m);
    }
  }
  parent.add(group);
  return group;
}

function makeTextTexture(text) {
  const canvas=document.createElement('canvas');
  canvas.width=768;
  canvas.height=128;
  const ctx=canvas.getContext('2d');
  ctx.clearRect(0,0,canvas.width,canvas.height);
  ctx.fillStyle='#eee9dc';
  ctx.shadowColor='rgba(238,233,220,0.26)';
  ctx.shadowBlur=8;
  ctx.font='600 58px Georgia, serif';
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
  const canvas=document.createElement('canvas');
  canvas.width=512;
  canvas.height=256;
  const ctx=canvas.getContext('2d');
  ctx.clearRect(0,0,512,256);

  // Broad, soft puddle masses. Detail comes from a handful of horizontal
  // reflections, not a repeated procedural dash pattern.
  for(const [cx,cy,rx,ry,a] of [
    [150,132,135,62,.34],[280,112,150,48,.29],[390,150,92,42,.22]
  ]){
    ctx.save();
    ctx.translate(cx,cy);
    ctx.scale(rx/ry,1);
    const g=ctx.createRadialGradient(0,0,0,0,0,ry);
    g.addColorStop(0,`rgba(198,207,218,${a})`);
    g.addColorStop(.55,`rgba(164,176,191,${a*.52})`);
    g.addColorStop(1,'rgba(120,135,154,0)');
    ctx.fillStyle=g;
    ctx.beginPath();
    ctx.arc(0,0,ry,0,Math.PI*2);
    ctx.fill();
    ctx.restore();
  }

  ctx.lineCap='round';
  for(const [x,y,w,a] of [
    [42,104,118,.28],[182,131,170,.23],[252,93,126,.20],
    [318,164,105,.18],[96,173,92,.14]
  ]){
    ctx.strokeStyle=`rgba(225,228,226,${a})`;
    ctx.lineWidth=2;
    ctx.beginPath();
    ctx.moveTo(x,y);
    ctx.lineTo(x+w,y+1);
    ctx.stroke();
  }

  const texture=new THREE.CanvasTexture(canvas);
  texture.colorSpace=THREE.SRGBColorSpace;
  texture.minFilter=THREE.LinearMipmapLinearFilter;
  texture.magFilter=THREE.LinearFilter;
  return new THREE.MeshBasicMaterial({
    map:texture,
    transparent:true,
    opacity:.92,
    depthWrite:false,
    side:THREE.DoubleSide,
  });
}

function makeDinerReflectionMaterial() {
  const canvas=document.createElement('canvas');
  canvas.width=640;
  canvas.height=1024;
  const ctx=canvas.getContext('2d');
  ctx.clearRect(0,0,canvas.width,canvas.height);

  let seed=91;
  const rand=()=>{seed=(seed*16807)%2147483647;return(seed-1)/2147483646;};

  // Six diner windows become vertical broken reflections toward the camera.
  const xs=[104,184,264,344,424,504];
  for(const x of xs){
    const g=ctx.createLinearGradient(0,80,0,930);
    g.addColorStop(0,'rgba(242,240,225,0.22)');
    g.addColorStop(.18,'rgba(225,228,224,0.16)');
    g.addColorStop(.55,'rgba(190,200,210,0.08)');
    g.addColorStop(1,'rgba(150,165,185,0)');
    ctx.fillStyle=g;

    for(let i=0;i<22;i++){
      const y=90+i*(28+rand()*20);
      const w=8+rand()*20;
      const h=3+rand()*8;
      const jitter=(rand()-.5)*26;
      ctx.globalAlpha=.45+rand()*.45;
      ctx.fillRect(x+jitter-w/2,y,w,h);
    }
  }

  // One stronger lamp reflection with irregular breakup.
  const lg=ctx.createLinearGradient(0,120,0,900);
  lg.addColorStop(0,'rgba(255,251,230,.42)');
  lg.addColorStop(.22,'rgba(236,236,225,.18)');
  lg.addColorStop(1,'rgba(190,200,215,0)');
  ctx.fillStyle=lg;
  for(let i=0;i<18;i++){
    const y=150+i*33;
    const w=24+rand()*54;
    ctx.globalAlpha=.4+rand()*.5;
    ctx.fillRect(520-w/2+(rand()-.5)*18,y,w,4+rand()*9);
  }
  ctx.globalAlpha=1;

  const tex=new THREE.CanvasTexture(canvas);
  tex.colorSpace=THREE.SRGBColorSpace;
  tex.minFilter=THREE.LinearMipmapLinearFilter;
  tex.magFilter=THREE.LinearFilter;

  return new THREE.MeshBasicMaterial({
    map:tex,
    transparent:true,
    opacity:.86,
    depthWrite:false,
    depthTest:true,
    side:THREE.DoubleSide,
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

  // Left foreground tower closes the empty side of the composition and gives
  // the window rhythm a second depth plane.
  addBox({
    parent:groups.solids,lines:groups.edges,materials,
    x:-31,y:14.5,z:-18,w:12,h:29,d:9,tone:'surface',edge:'secondary',
  });
  addWindowGridFront({
    parent:groups.emissive,x:-31,y:2.5,z:-13.46,
    cols:6,rows:10,dx:1.45,dy:2.35,w:.48,h:.72,material:materials.windowDim,
    off:(x,y)=>((x*5+y*9)%11===0),
  });
  addWindowGridSide({
    parent:groups.emissive,x:-24.96,y:2.5,z:-18,
    cols:4,rows:10,dz:1.65,dy:2.35,w:.48,h:.72,material:materials.windowDim,
    face:'right',off:(x,y)=>((x*3+y*7)%9===0),
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
    const win=new THREE.Mesh(new THREE.PlaneGeometry(1.72,2.45),materials.windowDark);
    win.position.set(-7.5+i*2.05,2.30,-15.96);
    groups.solids.add(win);
    addStroke(groups.strokes,materials.strokePrimary,[
      [-8.34+i*2.05,1.05,-15.93],[-8.34+i*2.05,3.45,-15.93]
    ]);
  }
  addStroke(groups.strokes,materials.strokePrimary,[[-8.45,3.52,-15.93],[4.55,3.52,-15.93]]);
  addStroke(groups.strokes,materials.strokePrimary,[[-8.45,1.00,-15.92],[4.55,1.00,-15.92]]);
  for(const x of [-8.45,4.55]){
    addStroke(groups.strokes,materials.strokePrimary,[[x,1.00,-15.92],[x,4.30,-15.92]]);
  }

  const door=new THREE.Mesh(new THREE.PlaneGeometry(1.65,3.15),materials.door);
  door.position.set(6.1,1.72,-15.95);
  groups.solids.add(door);
  addStroke(groups.strokes,materials.strokePrimary,[
    [5.27,.14,-15.92],[5.27,3.30,-15.92],[6.93,3.30,-15.92],[6.93,.14,-15.92]
  ]);

  const sign=new THREE.Mesh(
    new THREE.PlaneGeometry(9.6,1.45),
    new THREE.MeshBasicMaterial({map:makeTextTexture('THE ORIOLE'),transparent:true,depthWrite:false}),
  );
  sign.position.set(-2.4,6.18,-15.98);
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

  // Bright horizontal windows on the left-facing wall break the large dark
  // mass into a recognisable urban facade.
  for(const [y,z,w] of [[3.2,4.0,4.6],[5.6,9.0,5.4],[7.7,14.2,4.0]]){
    const win=new THREE.Mesh(new THREE.PlaneGeometry(w,.62),materials.windowBand);
    win.position.set(12.97,y,z);
    win.rotation.y=-Math.PI/2;
    groups.emissive.add(win);
  }
  addStroke(groups.strokes,materials.strokePrimary,[[12.93,1.0,1.0],[12.93,9.0,1.0],[12.93,9.0,18.0]]);

  // Front facade facing the primary camera: sparse authored windows and trim
  // prevent the near building from reading as an empty black blocker.
  for(const [x,y,w,h] of [
    [16.0,2.7,1.7,1.0],[19.1,2.7,1.7,1.0],[22.1,2.7,1.5,1.0],
    [16.2,5.5,1.5,.78],[19.2,5.5,1.9,.78],[22.0,5.5,1.45,.78],
    [16.5,8.0,1.35,.65],[20.0,8.0,1.6,.65]
  ]){
    const panel=new THREE.Mesh(new THREE.PlaneGeometry(w,h),materials.door);
    panel.position.set(x,y,18.53);
    groups.solids.add(panel);
    addStroke(groups.strokes,materials.strokeDim,[
      [x-w/2,y-h/2,18.55],[x+w/2,y-h/2,18.55],
      [x+w/2,y+h/2,18.55],[x-w/2,y+h/2,18.55],
      [x-w/2,y-h/2,18.55]
    ]);
  }

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

  // Diner interior: counter, stools and pendants.
  addStroke(groups.strokes,materials.strokePrimary,[[-7.7,1.72,-15.88],[3.0,1.72,-15.88]]);
  for(const x of [-6.2,-3.8,-1.4,1.0]){
    addStroke(groups.strokes,materials.strokeDim,[[x,.55,-15.87],[x,1.24,-15.87]]);
    addStroke(groups.strokes,materials.strokeDim,[[x-.30,1.24,-15.87],[x+.30,1.24,-15.87]]);
  }
  for(const x of [-6.2,-2.6,1.0]){
    addStroke(groups.strokes,materials.strokeDim,[[x,3.48,-15.86],[x,2.78,-15.86]]);
    const pendant=new THREE.Mesh(new THREE.CircleGeometry(.095,16),materials.white);
    pendant.position.set(x,2.70,-15.84);
    groups.emissive.add(pendant);
  }
  addStroke(groups.strokes,materials.strokePrimary,[[-8.5,4.34,-15.89],[4.0,4.34,-15.89]]);

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

  // Sparse pavement boundaries. Avoid a full technical grid.
  for(const x of [-7,1,9]){
    addStroke(groups.strokes,materials.strokeDim,[[x,.025,-24],[x*1.25,.025,48]]);
  }
  for(const z of [-12,9,30,44]){
    addStroke(groups.strokes,materials.strokeDim,[[-13.5,.026,z],[13.0,.026,z+.18]]);
  }
  addStroke(groups.strokes,materials.strokeDim,[[-11,.027,21],[-4,.027,18],[2,.027,19],[8,.027,16]]);

  // A foreground curb and rail to frame the shot.
  addStroke(groups.strokes,materials.strokePrimary,[[-14,.15,31],[-12.5,.35,22],[-11.8,.35,12]]);
  for(let z=13;z<30;z+=5.6){
    addStroke(groups.strokes,materials.strokeDim,[[-12.2,.2,z],[-12.2,1.6,z]]);
  }
  addStroke(groups.strokes,materials.strokePrimary,[[-12.2,1.6,12],[-12.2,1.6,30]]);

  addPlanarWetReflection(groups);

  // Stylized highlight streaks sit over the real planar reflection.
  for(const [x,z,w,d,o] of [
    [-5,2,6.8,.9,.12],[2,8,4.7,.65,.10],[-1,16,8.4,1.0,.105],[6,26,4.6,.75,.08],[-7,34,4.0,.6,.07]
  ]){
    const p=new THREE.Mesh(new THREE.PlaneGeometry(w,d),materials.reflection.clone());
    p.material.opacity=o;
    p.rotation.x=-Math.PI/2;
    p.position.set(x,.03,z);
    groups.atmosphere.add(p);
  }

  const dinerReflection=new THREE.Mesh(
    new THREE.PlaneGeometry(15.5,24),
    makeDinerReflectionMaterial(),
  );
  dinerReflection.rotation.x=-Math.PI/2;
  dinerReflection.position.set(-2.5,.041,-3.2);
  groups.atmosphere.add(dinerReflection);

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
  groups.edges.add(new THREE.LineSegments(geo,materials.foliageLine));

  // Brighter foreground grass in three clumps rather than an even fence.
  const foreground=[];
  const clumps=[[-12.2,15.5],[-11.3,22.0],[-12.4,28.5]];
  for(const [cx,cz] of clumps){
    for(let i=0;i<34;i++){
      const x=cx+(rand()-.5)*1.8;
      const z=cz+(rand()-.5)*3.4;
      const h=.38+rand()*1.28;
      const lean=(rand()-.5)*.62;
      foreground.push(x,.04,z,x+lean,h,z+(rand()-.5)*.28);
    }
  }
  const foregroundGeo=new THREE.BufferGeometry();
  foregroundGeo.setAttribute('position',new THREE.Float32BufferAttribute(foreground,3));
  groups.edges.add(new THREE.LineSegments(foregroundGeo,materials.foliageBright));
}

function addStreetFurniture(groups,materials) {
  for(const [x,z] of [[-8,-6],[8,-2],[-8,13],[8,24]]){
    const post=new THREE.Mesh(new THREE.CylinderGeometry(.045,.06,4.4,8),materials.surface);
    post.position.set(x,2.2,z);
    groups.solids.add(post);
    addStroke(groups.strokes,materials.strokeDim,[[x,.08,z],[x,4.32,z],[x+(x<0?.82:-.82),4.32,z]]);
    const lightX=x+(x<0?.85:-.85);
    const bulb=new THREE.Mesh(new THREE.SphereGeometry(.11,12,8),materials.white);
    bulb.position.set(lightX,4.28,z);
    groups.emissive.add(bulb);

    const halo=new THREE.Sprite(new THREE.SpriteMaterial({
      map:materials.haloTexture,
      color:0xf0eee2,
      transparent:true,
      opacity:z>10?.16:.21,
      depthWrite:false,
      depthTest:true,
      blending:THREE.NormalBlending,
    }));
    halo.position.set(lightX,4.28,z+.03);
    const haloSize=z>10?5.4:6.6;
    halo.scale.set(haloSize,haloSize,1);
    groups.atmosphere.add(halo);

    if(z===-6){
      const atmosphericHalo=new THREE.Sprite(new THREE.SpriteMaterial({
        map:materials.haloTexture,
        color:0xdfe2e4,
        transparent:true,
        opacity:.16,
        depthWrite:false,
        depthTest:false,
        blending:THREE.NormalBlending,
      }));
      atmosphericHalo.position.set(lightX,5.0,z+.4);
      atmosphericHalo.scale.set(15.5,15.5,1);
      atmosphericHalo.renderOrder=2;
      groups.atmosphere.add(atmosphericHalo);
    }
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
  const smokeTexture=makeSoftTexture();
  for(const [x,y,z,s,o] of [
    [-10.5,1.8,9.0,8.4,.34],[-9.5,2.1,-2.0,7.2,.31],[-7.6,3.3,-4.2,5.8,.22],[-1.8,4.7,-17.0,4.8,.11]
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

  const streetGlow=new THREE.Mesh(new THREE.CircleGeometry(8.0,64),materials.streetGlow);
  streetGlow.rotation.x=-Math.PI/2;
  streetGlow.scale.set(1.55,.72,1);
  streetGlow.position.set(-4.8,.029,15.5);
  groups.atmosphere.add(streetGlow);
}

function addCast(groups,materials,outlineTargets) {
  createCharacter({
    name:'Detective',position:[5.8,0,21.5],scale:1.06,
    pose:'neutral',kind:'hero',materials,parent:groups.characters,
  });

  const crowd=[
    [-7.2,-6.8,.88,0],[-4.3,-7.7,.94,0],[-1.2,-6.8,.86,0],
    [-8.0,-2.4,.80,0],[-5.0,-1.8,.96,0],[-2.1,-2.5,.82,0],
    [1.0,-4.0,.76,0],
  ];
  crowd.forEach(([x,z,s,yaw],i)=>createCharacter({
    name:`Crowd ${i+1}`,position:[x,0,z],scale:s,
    pose:i%3===0?'gesture':i%2?'walk':'neutral',
    kind:'crowd',variant:i,materials,parent:groups.characters,
  }));
}

export function buildWorld(scene,profile){
  const outlineTargets=[];
  const groups={
    solids:new THREE.Group(),
    edges:new THREE.Group(),
    strokes:new THREE.Group(),
    emissive:new THREE.Group(),
    atmosphere:new THREE.Group(),
    characters:new THREE.Group(),
  };
  Object.values(groups).forEach(g=>scene.add(g));

  const materials={
    surface:surfaceMat(profile.palette.surface,profile.palette.background,.0075,8,.18),
    surfaceLift:surfaceMat(profile.palette.surfaceLift,profile.palette.background,.0075,10,.15),
    surfaceMid:surfaceMat(profile.palette.surfaceMid,profile.palette.background,.0075,12,.12),
    ground:surfaceMat(0x202c41,profile.palette.background,.0075,7,.16),
    character:meshMat(0x010204,{side:THREE.DoubleSide}),
    characterLineColor:'#e8e5dc',
    characterDimLineColor:'#aeb5c1',
    detailLine:new THREE.LineBasicMaterial({color:profile.palette.lineDim,transparent:true,opacity:.80,depthTest:true}),
    edgePrimary:new THREE.LineBasicMaterial({
      color:profile.palette.line,transparent:true,opacity:.74,fog:true,
    }),
    edgeSecondary:new THREE.LineBasicMaterial({
      color:profile.palette.lineDim,transparent:true,opacity:.42,fog:true,
    }),
    foliageLine:new THREE.LineBasicMaterial({
      color:profile.palette.lineDim,transparent:true,opacity:.58,fog:true,
    }),
    foliageBright:new THREE.LineBasicMaterial({
      color:profile.palette.line,transparent:true,opacity:.78,fog:true,
    }),
    strokePrimary:strokeMaterial(profile.palette.line,.92,1.02),
    strokeDim:strokeMaterial(profile.palette.lineDim,.62,.76),
    white:meshMat(profile.palette.white),
    window:meshMat(profile.palette.white),
    windowDim:meshMat(0xb8c0cb),
    windowBand:meshMat(0xd4d5d0),
    windowDark:meshMat(0x8f9aab),
    door:meshMat(0x344157),
    sideSign:meshMat(0x9da4ad),
    reflection:meshMat(profile.palette.lineDim,{transparent:true,opacity:.1,depthWrite:false}),
    wetWash:meshMat(0x99a5b5,{transparent:true,opacity:.12,depthWrite:false,side:THREE.DoubleSide}),
    hazeDisc:meshMat(0xc5c9d0,{transparent:true,opacity:.31,depthWrite:false,side:THREE.DoubleSide}),
    beam:meshMat(profile.palette.white,{
      transparent:true,opacity:.026,side:THREE.DoubleSide,depthWrite:false,blending:THREE.AdditiveBlending,
    }),
    pool:meshMat(profile.palette.white,{
      transparent:true,opacity:.075,side:THREE.DoubleSide,depthWrite:false,blending:THREE.AdditiveBlending,
    }),
    haloTexture:makeSoftTexture(),
    streetGlow:meshMat(0xaeb7c4,{
      transparent:true,opacity:.075,side:THREE.DoubleSide,depthWrite:false,
    }),
  };

  addGround(groups,materials);
  addOfficeTower(groups,materials);
  addDiner(groups,materials);
  addRightArchitecture(groups,materials);
  addCityDrawingDetails(groups,materials);
  addStreetFurniture(groups,materials);
  addAtmosphere(groups,materials);
  addCast(groups,materials,outlineTargets);

  return {groups,materials,outlineTargets};
}
