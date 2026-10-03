import * as THREE from 'three';
import { toon, unlit, line } from '../style/materials.js';

const DEG = Math.PI / 180;

function seeded(seed=19) {
  let s = seed >>> 0;
  return () => {
    s = (s * 1664525 + 1013904223) >>> 0;
    return s / 4294967296;
  };
}

function addEdges(mesh, group, material, threshold=32) {
  const edges = new THREE.EdgesGeometry(mesh.geometry, threshold);
  const lines = new THREE.LineSegments(edges, material);
  lines.position.copy(mesh.position);
  lines.rotation.copy(mesh.rotation);
  lines.scale.copy(mesh.scale);
  lines.renderOrder = 4;
  group.add(lines);
  return lines;
}

function box(parent, lines, mat, edgeMat, size, pos, {rotY=0, edges=true, threshold=35}={}) {
  const mesh = new THREE.Mesh(new THREE.BoxGeometry(...size), mat);
  mesh.position.set(...pos);
  mesh.rotation.y = rotY;
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  parent.add(mesh);
  if (edges) addEdges(mesh, lines, edgeMat, threshold);
  return mesh;
}

function plane(parent, mat, size, pos, rot=[-Math.PI/2,0,0]) {
  const mesh = new THREE.Mesh(new THREE.PlaneGeometry(...size), mat);
  mesh.position.set(...pos);
  mesh.rotation.set(...rot);
  mesh.receiveShadow = true;
  parent.add(mesh);
  return mesh;
}

function cylinder(parent, lines, mat, edgeMat, radius, height, pos, sides=10) {
  const mesh = new THREE.Mesh(new THREE.CylinderGeometry(radius,radius,height,sides),mat);
  mesh.position.set(...pos);
  mesh.castShadow = true;
  parent.add(mesh);
  addEdges(mesh,lines,edgeMat,25);
  return mesh;
}

function discBillboard(parent, mat, radius, pos) {
  const mesh = new THREE.Mesh(new THREE.CircleGeometry(radius,64),mat);
  mesh.position.set(...pos);
  mesh.rotation.y = Math.PI;
  parent.add(mesh);
  return mesh;
}

function makeSign(text, fg='#e9e2d1', bg='rgba(0,0,0,0)') {
  const canvas = document.createElement('canvas');
  canvas.width = 1024; canvas.height = 256;
  const ctx = canvas.getContext('2d');
  ctx.fillStyle = bg; ctx.fillRect(0,0,canvas.width,canvas.height);
  ctx.fillStyle = fg;
  ctx.font = '700 128px Georgia, serif';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.letterSpacing = '14px';
  ctx.fillText(text,512,135);
  const tex = new THREE.CanvasTexture(canvas);
  tex.colorSpace = THREE.SRGBColorSpace;
  return tex;
}

function addWindowGrid(parent, wall, matOn, matOff, {x0,y0,z,cols,rows,dx,dy,w=.62,h=.95,seed=1}) {
  const rand = seeded(seed);
  for (let y=0;y<rows;y++) {
    for (let x=0;x<cols;x++) {
      const on = rand() > 0.46;
      const m = new THREE.Mesh(new THREE.PlaneGeometry(w,h), on ? matOn : matOff);
      m.position.set(x0 + x*dx, y0 + y*dy, z);
      m.renderOrder = 3;
      parent.add(m);
    }
  }
}

function addLamp(root, lines, mats, x,z, height=6.8) {
  cylinder(root,lines,mats.ink,mats.lineDim,.11,height,[x,height/2,z],8);
  const arm = box(root,lines,mats.ink,mats.lineDim,[1.35,.12,.12],[x+.55,height-.18,z],{edges:true});
  arm.rotation.z = -5*DEG;
  const shade = new THREE.Mesh(new THREE.ConeGeometry(.48,.42,12,1,true),mats.paper);
  shade.position.set(x+1.08,height-.5,z);
  shade.rotation.z = Math.PI;
  root.add(shade);

  const bulb = new THREE.PointLight(0xf3d89b,6.5,17,1.9);
  bulb.position.set(x+1.08,height-.75,z);
  bulb.castShadow = true;
  bulb.shadow.mapSize.set(512,512);
  root.add(bulb);

  const cone = new THREE.Mesh(
    new THREE.ConeGeometry(3.9,7.4,32,1,true),
    new THREE.MeshBasicMaterial({color:0xf2ddb0,transparent:true,opacity:.045,depthWrite:false,side:THREE.DoubleSide,blending:THREE.AdditiveBlending})
  );
  cone.position.set(x+1.08,height-4.4,z);
  cone.rotation.x = Math.PI;
  root.add(cone);
}

function addFigure(root, lines, mats, pos, scale=1, hat=true) {
  const g = new THREE.Group();
  g.position.set(...pos);
  g.scale.setScalar(scale);
  root.add(g);

  const lineRoot = new THREE.Group();
  lineRoot.position.set(...pos);
  lineRoot.scale.setScalar(scale);
  lines.add(lineRoot);

  // long coat: broad shoulders, narrow hem gives a readable 1930s silhouette.
  const coat = new THREE.Mesh(new THREE.ConeGeometry(.72,2.55,7,1,false),mats.ink);
  coat.position.y = 1.55;
  coat.rotation.y = Math.PI/7;
  coat.castShadow = true;
  g.add(coat);
  addEdges(coat,lineRoot,mats.lineBright,28);

  const head = new THREE.Mesh(new THREE.IcosahedronGeometry(.34,1),mats.paper);
  head.position.y = 3.08;
  head.castShadow = true;
  g.add(head);

  const neck = new THREE.Mesh(new THREE.CylinderGeometry(.16,.18,.35,8),mats.ink);
  neck.position.y = 2.75; g.add(neck);

  for (const side of [-1,1]) {
    const leg = new THREE.Mesh(new THREE.CylinderGeometry(.12,.14,1.5,7),mats.ink);
    leg.position.set(side*.22,.32,0);
    leg.rotation.z = side*3*DEG;
    leg.castShadow = true;
    g.add(leg);
  }

  if (hat) {
    const brim = new THREE.Mesh(new THREE.CylinderGeometry(.58,.58,.08,20),mats.ink);
    brim.position.y = 3.38; g.add(brim);
    const crown = new THREE.Mesh(new THREE.CylinderGeometry(.31,.34,.35,10),mats.ink);
    crown.position.y = 3.58; g.add(crown);
  }
  return g;
}

function addRain(linesGroup, material) {
  const rand = seeded(3301);
  const verts=[];
  for(let i=0;i<420;i++){
    const x=(rand()-.5)*58;
    const y=rand()*25+1;
    const z=(rand()-.5)*58;
    const len=.35+rand()*.95;
    verts.push(x,y,z,x-.13,y-len,z+.03);
  }
  const geo=new THREE.BufferGeometry();
  geo.setAttribute('position',new THREE.Float32BufferAttribute(verts,3));
  const rain=new THREE.LineSegments(geo,material);
  rain.renderOrder=8;
  linesGroup.add(rain);
}

export function buildWorld(scene, palette) {
  const fill = new THREE.Group();
  const lines = new THREE.Group();
  const atmosphere = new THREE.Group();
  scene.add(fill,lines,atmosphere);

  const mats = {
    ink: toon(palette.ink),
    charcoal: toon(palette.charcoal),
    mid: toon(palette.mid),
    paper: toon(palette.paper),
    white: toon(palette.white),
    gold: toon(palette.gold),
    goldDark: toon(palette.goldDark),
    lineBright: line(palette.paper,.96),
    lineDim: line(0xaaa69b,.58),
    lineGold: line(palette.gold,.95),
    windowOff: unlit(0x101214),
    windowOn: unlit(0xd9d2c0),
    windowGold: unlit(palette.gold),
    rain: line(0xc8c5bd,.13)
  };

  // Ground and streets: a tilted intersection, not a sterile grid.
  plane(fill,mats.charcoal,[80,68],[0,-.04,0]);
  plane(fill,mats.ink,[16,68],[-4,.02,0],[ -Math.PI/2,0, -12*DEG ]);
  plane(fill,mats.ink,[70,13],[3,.03,6],[ -Math.PI/2,0, 6*DEG ]);

  // Sidewalk masses.
  box(fill,lines,mats.mid,mats.lineDim,[17,.36,37],[-18,.12,-4],{rotY:-12*DEG,threshold:42});
  box(fill,lines,mats.mid,mats.lineDim,[22,.36,36],[16,.12,-6],{rotY:6*DEG,threshold:42});
  box(fill,lines,mats.mid,mats.lineDim,[21,.36,13],[-17,.12,20],{rotY:6*DEG,threshold:42});

  // Gold moon / graphic disc anchors the negative space.
  const moon = discBillboard(atmosphere,unlit(0xd5a62f),5.8,[-10,15,-30]);
  moon.rotation.y = 0;

  // Theatre block.
  box(fill,lines,mats.charcoal,mats.lineBright,[15,11,9],[-17,5.7,-8],{rotY:-4*DEG});
  box(fill,lines,mats.charcoal,mats.lineDim,[13.2,3.2,2.0],[-15.8,4.1,-2.6],{rotY:-4*DEG});
  box(fill,lines,mats.paper,mats.lineBright,[11.8,.42,2.6],[-15.3,5.9,-1.7],{rotY:-4*DEG});
  for(let i=0;i<5;i++){
    box(fill,lines,mats.mid,mats.lineDim,[.6,8.2,.45],[-22.8+i*3.05,6.2,-3.25],{rotY:-4*DEG,threshold:48});
  }

  const facadeWash = new THREE.Mesh(
    new THREE.PlaneGeometry(12.7,4.8),
    new THREE.MeshBasicMaterial({
      color:0xd6d0c2,transparent:true,opacity:.11,depthWrite:false,toneMapped:false
    })
  );
  facadeWash.position.set(-15.65,6.25,-2.73);
  facadeWash.rotation.y=-4*DEG;
  atmosphere.add(facadeWash);

  const signTex = makeSign('NOCTURNE');
  const sign = new THREE.Mesh(new THREE.PlaneGeometry(10.6,2.7),new THREE.MeshBasicMaterial({map:signTex,transparent:true,toneMapped:false}));
  sign.position.set(-15.2,9.0,-2.78);
  sign.rotation.y = -4*DEG;
  fill.add(sign);

  // Theatre lit doors.
  for(let i=0;i<4;i++){
    const door=new THREE.Mesh(new THREE.PlaneGeometry(1.55,2.9), i===1 ? mats.windowGold : mats.windowOn);
    door.position.set(-19.7+i*2.6,2.0,-2.52);
    door.rotation.y=-4*DEG;
    fill.add(door);
  }

  // Office tower with setbacks: large clean shapes first.
  box(fill,lines,mats.mid,mats.lineBright,[16,20,13],[17,10,-13],{rotY:3*DEG});
  box(fill,lines,mats.charcoal,mats.lineDim,[12,7,10],[18,23,-14],{rotY:3*DEG});
  box(fill,lines,mats.charcoal,mats.lineDim,[7,6,8],[19,29.5,-14],{rotY:3*DEG});

  addWindowGrid(fill,null,mats.windowOn,mats.windowOff,{
    x0:10.4,y0:2.6,z:-6.14,cols:6,rows:8,dx:2.35,dy:2.05,w:.78,h:1.02,seed:84
  });

  // Low storefronts closing the rear street.
  for(let i=0;i<4;i++){
    const x=-5+i*6.3;
    const h=5+(i%2)*1.7;
    box(fill,lines,i===2?mats.mid:mats.charcoal,mats.lineDim,[5.7,h,7],[x,h/2,-21+i*.7],{rotY:(i-1)*2*DEG});
    const window=new THREE.Mesh(new THREE.PlaneGeometry(3.8,1.5),i===2?mats.windowGold:mats.windowOff);
    window.position.set(x,2.0,-17.45+i*.7);
    fill.add(window);
  }

  // Fire escape / structural drawing in the alley.
  for(let y=5;y<14;y+=3.0){
    const balcony=box(fill,lines,mats.ink,mats.lineBright,[4.5,.16,1.2],[-8,y,-14],{edges:true});
    for(const sx of [-1,1]){
      const railGeo=new THREE.BufferGeometry().setFromPoints([
        new THREE.Vector3(-8+sx*2.0,y,-13.4),
        new THREE.Vector3(-8+sx*2.0,y+1.1,-13.4)
      ]);
      lines.add(new THREE.Line(railGeo,mats.lineDim));
    }
  }

  // Crosswalk: bright graphic rhythm.
  for(let i=-3;i<=3;i++){
    const stripe=new THREE.Mesh(new THREE.PlaneGeometry(1.0,6.4),mats.paper);
    stripe.rotation.x=-Math.PI/2;
    stripe.rotation.z=8*DEG;
    stripe.position.set(-2+i*1.75,.065,7.3+i*.18);
    fill.add(stripe);
  }

  addLamp(fill,lines,mats,-5,10,7.1);
  addLamp(fill,lines,mats,8,-2,6.4);

  const lightPool = new THREE.Mesh(
    new THREE.CircleGeometry(7.4,64),
    new THREE.MeshBasicMaterial({
      color:0xd7caa6, transparent:true, opacity:.12,
      depthWrite:false, toneMapped:false, blending:THREE.AdditiveBlending
    })
  );
  lightPool.rotation.x=-Math.PI/2;
  lightPool.scale.set(1.55,.58,1);
  lightPool.position.set(-2.0,.085,5.0);
  atmosphere.add(lightPool);

  const heroSpot = new THREE.SpotLight(0xffe9ba,52,28,0.46,0.45,1.4);
  heroSpot.position.set(-10,17,14);
  heroSpot.target.position.set(-2.3,1.0,4.8);
  heroSpot.castShadow=true;
  heroSpot.shadow.mapSize.set(1024,1024);
  scene.add(heroSpot,heroSpot.target);

  addFigure(fill,lines,mats,[-2.3,.18,4.8],1.28,true);
  addFigure(fill,lines,mats,[-10.2,.18,-6.5],.78,true);
  addFigure(fill,lines,mats,[7.5,.18,10.0],.68,false);

  // A taxi-like procedural prop, kept as a graphic wedge.
  box(fill,lines,mats.ink,mats.lineBright,[4.5,1.2,2.0],[8.2,.8,5.8],{rotY:-20*DEG});
  box(fill,lines,mats.goldDark,mats.lineGold,[2.4,.85,1.75],[8.0,1.72,5.72],{rotY:-20*DEG});
  for(const [x,z] of [[6.5,5.2],[9.8,6.4]]){
    const wheel=new THREE.Mesh(new THREE.CylinderGeometry(.42,.42,.24,14),mats.ink);
    wheel.rotation.z=Math.PI/2; wheel.rotation.y=-20*DEG;
    wheel.position.set(x,.42,z); fill.add(wheel);
  }

  // Foreground framing geometry.
  box(fill,lines,mats.ink,mats.lineBright,[4,15,7],[-29,7.5,13],{rotY:-8*DEG});

  addRain(atmosphere,mats.rain);

  return {
    fill,lines,atmosphere,mats,
    setMode(mode,scene){
      scene.overrideMaterial = null;
      fill.visible=true; lines.visible=true; atmosphere.visible=true;
      if(mode==='shape'){
        scene.overrideMaterial = new THREE.MeshBasicMaterial({color:palette.paper});
        lines.visible=false; atmosphere.visible=false;
      } else if(mode==='line'){
        fill.visible=false; atmosphere.visible=false; lines.visible=true;
      }
    }
  };
}
