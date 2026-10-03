import * as THREE from 'three';
import { createCharacter } from './characters.js';

function meshMat(color, extra = {}) {
  return new THREE.MeshBasicMaterial({ color, ...extra });
}

function addEdges(mesh, group, material, threshold = 24) {
  const edge = new THREE.LineSegments(new THREE.EdgesGeometry(mesh.geometry, threshold), material);
  edge.position.copy(mesh.position);
  edge.quaternion.copy(mesh.quaternion);
  edge.scale.copy(mesh.scale);
  group.add(edge);
  return edge;
}

function addBox({ parent, lines, materials, x, y, z, w, h, d, tone = 'surface', edge = 'secondary' }) {
  const mesh = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), materials[tone]);
  mesh.position.set(x, y, z);
  parent.add(mesh);
  if (edge) addEdges(mesh, lines, edge === 'primary' ? materials.line : materials.lineDim, 26);
  return mesh;
}

function addWindowWall({ parent, x, y, z, cols, rows, dx, dy, width = 0.38, height = 0.58, material }) {
  const group = new THREE.Group();
  for (let iy = 0; iy < rows; iy++) {
    for (let ix = 0; ix < cols; ix++) {
      if (((ix * 7 + iy * 11) % 13) === 0) continue;
      const window = new THREE.Mesh(new THREE.PlaneGeometry(width, height), material);
      window.position.set(x + (ix - (cols - 1) / 2) * dx, y + iy * dy, z);
      group.add(window);
    }
  }
  parent.add(group);
}

function addPolyline(parent, material, points, closed = false) {
  const pts = points.map((p) => new THREE.Vector3(...p));
  if (closed) pts.push(pts[0].clone());
  const geo = new THREE.BufferGeometry().setFromPoints(pts);
  parent.add(new THREE.Line(geo, material));
}

function addStreetFurniture(groups, materials) {
  for (const [x, z] of [[-8,-8],[8,-3],[-8,12],[8,22]]) {
    const post = new THREE.Mesh(new THREE.CylinderGeometry(0.055,0.07,4.4,8), materials.surface);
    post.position.set(x,2.2,z);
    groups.solids.add(post);
    addEdges(post, groups.lines, materials.lineDim, 18);

    addPolyline(groups.lines, materials.line, [
      [x,4.28,z],[x + (x<0?0.78:-0.78),4.28,z]
    ]);
    const bulb = new THREE.Mesh(new THREE.SphereGeometry(0.12,10,8), materials.white);
    bulb.position.set(x + (x<0?0.82:-0.82),4.22,z);
    groups.emissive.add(bulb);
  }

  const bin = addBox({
    parent:groups.solids, lines:groups.lines, materials,
    x:8.9,y:0.8,z:9.0,w:1.3,h:1.6,d:1.3,tone:'surfaceLift',edge:'primary',
  });
  void bin;
}

function addArchitecture(groups, materials) {
  // Tall illuminated office tower: the dominant reference-like background rhythm.
  addBox({
    parent:groups.solids, lines:groups.lines, materials,
    x:-20,y:19,z:-38,w:18,h:38,d:10,tone:'surfaceLift',edge:'secondary',
  });
  addWindowWall({
    parent:groups.emissive, x:-20,y:3,z:-32.97,
    cols:11,rows:14,dx:1.36,dy:2.18,width:0.54,height:0.78,material:materials.window,
  });

  // Low diner / club.
  addBox({
    parent:groups.solids, lines:groups.lines, materials,
    x:-2,y:4,z:-20,w:25,h:8,d:8,tone:'surfaceMid',edge:'primary',
  });
  const awning = addBox({
    parent:groups.solids, lines:groups.lines, materials,
    x:-2,y:5.05,z:-14.95,w:12,h:0.58,d:2.2,tone:'surfaceLift',edge:'primary',
  });
  void awning;
  const sign = new THREE.Mesh(new THREE.PlaneGeometry(8.8,1.08), materials.sign);
  sign.position.set(-2,6.12,-15.98);
  groups.emissive.add(sign);
  for (let i=0;i<6;i++) {
    const divider = new THREE.Mesh(new THREE.PlaneGeometry(0.055,0.78), materials.surface);
    divider.position.set(-5.2+i*1.28,6.12,-15.94);
    groups.solids.add(divider);
  }

  // Right-hand theater mass and upper balcony.
  addBox({
    parent:groups.solids, lines:groups.lines, materials,
    x:18,y:8,z:-15,w:13,h:16,d:12,tone:'surface',edge:'primary',
  });
  addWindowWall({
    parent:groups.emissive, x:11.47,y:3,z:-15,
    cols:3,rows:4,dx:2.25,dy:2.4,width:0.56,height:0.92,material:materials.windowDim,
  });

  addBox({
    parent:groups.solids, lines:groups.lines, materials,
    x:19,y:5,z:10,w:12,h:10,d:17,tone:'surfaceLift',edge:'primary',
  });

  // Architectural line detail that reads as designed drawing, not auto-wireframe.
  for (const z of [-10,0,10,20]) {
    addPolyline(groups.lines, materials.lineDim, [[11.8,0.2,z],[11.8,6.8,z],[11.8,6.8,z+5]]);
    addPolyline(groups.lines, materials.lineDim, [[-10.8,0.2,z],[-10.8,3.8,z],[-8.0,3.8,z]]);
  }
  for (let y=1.2;y<7;y+=1.1) {
    addPolyline(groups.lines, materials.lineDim, [[12.0,y,-7],[12.0,y,5]]);
  }
}

function addGround(groups, materials) {
  const ground = new THREE.Mesh(new THREE.PlaneGeometry(46,110), materials.ground);
  ground.rotation.x = -Math.PI/2;
  ground.position.set(0,0,8);
  groups.solids.add(ground);

  // Clean perspective seams.
  for (let x=-10;x<=10;x+=5) {
    addPolyline(groups.lines, materials.lineDim, [[x,0.018,-28],[x*1.55,0.018,52]]);
  }
  for (let z=-24;z<=46;z+=7) {
    addPolyline(groups.lines, materials.lineDim, [[-13.5,0.019,z],[13.5,0.019,z+0.25]]);
  }

  // Wet pavement reflections: broad, soft, sparse.
  for (const [x,z,w,d,o] of [
    [-6,4,7,1.2,.13],[3,8,5,0.8,.10],[-2,18,9,1.5,.11],[6,28,5,0.9,.10],[-7,33,4,0.8,.08]
  ]) {
    const p = new THREE.Mesh(new THREE.PlaneGeometry(w,d), materials.reflection.clone());
    p.material.opacity = o;
    p.rotation.x = -Math.PI/2;
    p.position.set(x,0.024,z);
    groups.atmosphere.add(p);
  }

  // Controlled grass tufts: localized at curb edges, never full-frame scribble.
  const verts=[];
  let seed=17;
  const rand=()=>{ seed=(seed*16807)%2147483647; return (seed-1)/2147483646; };
  for (let i=0;i<110;i++) {
    const side=rand()>.5?1:-1;
    const x=side*(9.2+rand()*3.8);
    const z=-12+rand()*55;
    const h=.25+rand()*.85;
    verts.push(x,.03,z,x+(rand()-.5)*.24,h,z+(rand()-.5)*.18);
  }
  const grassGeo=new THREE.BufferGeometry();
  grassGeo.setAttribute('position',new THREE.Float32BufferAttribute(verts,3));
  groups.lines.add(new THREE.LineSegments(grassGeo,materials.lineDim));
}

function addSpotlight(groups, materials) {
  const beam = new THREE.Mesh(
    new THREE.CylinderGeometry(2.0,6.2,30,32,1,true),
    materials.beam,
  );
  beam.position.set(-6.5,14,-5);
  beam.rotation.z = -0.56;
  beam.rotation.x = 0.05;
  groups.atmosphere.add(beam);

  const pool = new THREE.Mesh(new THREE.CircleGeometry(5.4,48),materials.pool);
  pool.rotation.x=-Math.PI/2;
  pool.scale.set(1.5,.62,1);
  pool.position.set(-3.8,.035,-9);
  groups.atmosphere.add(pool);
}

function addCast(groups, materials) {
  createCharacter({
    name:'Detective',position:[4.0,0,21.5],yaw:3.25,scale:1.23,
    pose:'neutral',kind:'hero',materials,parent:groups.characters,
  });

  const crowd=[
    [-7.2,-7.2,.88,3.05],[-3.8,-7.8,.94,3.18],[-.6,-6.8,.86,3.02],
    [-8.0,-2.4,.80,3.12],[-4.6,-1.9,.96,3.20],[-1.0,-2.6,.82,2.98],
  ];
  crowd.forEach(([x,z,s,yaw],i)=>createCharacter({
    name:`Crowd ${i+1}`,position:[x,0,z],yaw,scale:s,
    pose:i%3===0?'gesture':i%2?'walk':'neutral',
    kind:'crowd',variant:i,materials,parent:groups.characters,
  }));
}

export function buildWorld(scene, profile) {
  const groups={
    solids:new THREE.Group(),
    lines:new THREE.Group(),
    emissive:new THREE.Group(),
    atmosphere:new THREE.Group(),
    characters:new THREE.Group(),
  };
  Object.values(groups).forEach(g=>scene.add(g));

  const materials={
    surface:meshMat(profile.palette.surface),
    surfaceLift:meshMat(profile.palette.surfaceLift),
    surfaceMid:meshMat(profile.palette.surfaceMid),
    ground:meshMat(0x141c2c),
    character:meshMat(0x010205),
    detail:meshMat(profile.palette.lineDim),
    line:new THREE.LineBasicMaterial({
      color:profile.palette.line,transparent:true,opacity:profile.line.primaryOpacity,
    }),
    lineDim:new THREE.LineBasicMaterial({
      color:profile.palette.lineDim,transparent:true,opacity:profile.line.secondaryOpacity,
    }),
    heroOutline:meshMat(profile.palette.line,{side:THREE.BackSide,depthWrite:false}),
    crowdOutline:meshMat(profile.palette.lineDim,{side:THREE.BackSide,transparent:true,opacity:.78,depthWrite:false}),
    white:meshMat(profile.palette.white),
    window:meshMat(profile.palette.white),
    windowDim:meshMat(profile.palette.lineDim),
    sign:meshMat(profile.palette.white),
    reflection:meshMat(profile.palette.lineDim,{transparent:true,opacity:.1,depthWrite:false}),
    beam:meshMat(profile.palette.white,{
      transparent:true,opacity:.085,side:THREE.DoubleSide,depthWrite:false,blending:THREE.AdditiveBlending,
    }),
    pool:meshMat(profile.palette.white,{
      transparent:true,opacity:.13,side:THREE.DoubleSide,depthWrite:false,blending:THREE.AdditiveBlending,
    }),
  };

  addGround(groups,materials);
  addArchitecture(groups,materials);
  addStreetFurniture(groups,materials);
  addSpotlight(groups,materials);
  addCast(groups,materials);

  return {groups,materials};
}
