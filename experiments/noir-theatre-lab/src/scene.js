import * as THREE from 'three';
import { FBXLoader } from 'three/addons/loaders/FBXLoader.js';

const loader = new FBXLoader();
const CITY_URL = new URL('../assets/models/city.fbx', import.meta.url).href;
const THEATRE_URL = new URL('../assets/models/theatre.fbx', import.meta.url).href;
const ALLEY_URL = new URL('../assets/models/alley.fbx', import.meta.url).href;

function makeMat(color, emissive = 0x000000) {
  return new THREE.MeshStandardMaterial({
    color,
    emissive,
    emissiveIntensity: emissive ? 0.28 : 0,
    roughness: 0.95,
    metalness: 0.01,
    flatShading: true,
  });
}

function replaceMaterials(root, palette, role='context') {
  let index = 0;
  const discard=[];
  root.traverse(o=>{if(o.isMesh && (/^(描线|壳_)/.test(o.name) || /^(郊野地面|河面|驳岸|逃者|追者)/.test(o.name))) discard.push(o);});
  for(const o of discard) o.removeFromParent();
  const colors = role === 'hero'
    ? [palette.pale, palette.mid, 0x7c796f]
    : [palette.mid, 0x444642, 0x303230, 0x686862];
  root.traverse((o) => {
    if (!o.isMesh) return;
    let color=colors[index++ % colors.length];
    if(/GROUND|PADS|干道|底板|巷底|街$|路缘|沟/.test(o.name)) color=0x202222;
    if(/窗|WIN_/.test(o.name)) color=0x17191a;
    if(/BUILDINGS|楼_/.test(o.name)) color=0x666763;
    if(/ROOFTOPS|屋顶/.test(o.name)) color=0x79796f;
    o.material = makeMat(color);
    o.castShadow = true;
    o.receiveShadow = true;
    o.frustumCulled = true;
  });
}

function normalize(root, extent, rotationY=0) {
  // FBX already carries its authored up-axis and scale. Preserve both.
  root.rotateOnWorldAxis(new THREE.Vector3(0,1,0), rotationY);
  root.updateMatrixWorld(true);
  const box = new THREE.Box3().setFromObject(root);
  const size = box.getSize(new THREE.Vector3());
  const scale = extent / Math.max(size.x, size.z, 0.001);
  root.scale.multiplyScalar(scale);
  root.updateMatrixWorld(true);
  const b2 = new THREE.Box3().setFromObject(root);
  const center = b2.getCenter(new THREE.Vector3());
  root.position.x -= center.x;
  root.position.z -= center.z;
  root.position.y -= b2.min.y;
  root.updateMatrixWorld(true);
  return new THREE.Box3().setFromObject(root);
}

function createLineLayer(root, profile) {
  // Only authored cornices receive geometric ink. Never outline the imported city.
  const layer=new THREE.Group();
  const material=new THREE.LineBasicMaterial({color:profile.palette.ink,transparent:true,opacity:profile.line.structuralOpacity});
  root.updateMatrixWorld(true);
  root.traverse(o=>{
    if(!o.isMesh||!o.userData.structuralInk)return;
    const edges=new THREE.LineSegments(new THREE.EdgesGeometry(o.geometry,35),material);
    edges.applyMatrix4(o.matrixWorld);layer.add(edges);
  });
  return layer;
}

function boxMesh(size, position, material) {
  const m = new THREE.Mesh(new THREE.BoxGeometry(...size), material);
  m.userData.structuralInk=size[0]>2 && size[0]<20 && size[1]<.5 && size[2]>2 && size[2]<20;
  m.position.fromArray(position); m.castShadow=true; m.receiveShadow=true; return m;
}

function stageFloor(radius){
  const material=makeMat(0x171b1c);
  material.onBeforeCompile=shader=>{
    shader.vertexShader='varying vec3 vStagePosition;\n'+shader.vertexShader;
    shader.vertexShader=shader.vertexShader.replace('#include <begin_vertex>','#include <begin_vertex>\n vStagePosition=(modelMatrix*vec4(transformed,1.)).xyz;');
    shader.fragmentShader='varying vec3 vStagePosition;\n'+shader.fragmentShader;
    shader.fragmentShader=shader.fragmentShader.replace('#include <opaque_fragment>',`outgoingLight *= pow(1.0-smoothstep(${(radius*.40).toFixed(2)},${radius.toFixed(2)},length(vStagePosition.xz)),2.0);\n #include <opaque_fragment>`);
  };
  material.customProgramCacheKey=()=>`stage-floor-${radius}`;
  return material;
}

function addCityStage(root, palette) {
  const stone=makeMat(palette.paper), ink=makeMat(palette.dark);
  // A setback fly tower, auditorium and colonnade form one architectural landmark.
  root.add(boxMesh([10,6,8],[-7,3,-9],stone));
  root.add(boxMesh([6,12,5],[-7,9,-11],stone));
  root.add(boxMesh([4.1,3,3.8],[-7,16.5,-11],stone));
  root.add(boxMesh([1.2,3,1.2],[-7,19.5,-11],stone));
  root.add(boxMesh([10.8,.45,2.7],[-7,3.2,-4.4],ink));
  for(let x=-10.8;x<-2.5;x+=1.65){
    root.add(boxMesh([.36,3,.45],[x,1.5,-3.6],stone));
    root.add(boxMesh([.66,.20,.65],[x,.1,-3.6],stone));
  }
  for(let y=6;y<14;y+=2.3)for(let x=-8.7;x<-4.8;x+=1.7)
    root.add(boxMesh([.42,1.15,.08],[x,y,-8.45],ink));
}

function addCityMasses(root,palette){
  const blocks=[[-23,12,8,7,7],[-22,-16,7,9,6],[15,13,7,10,7],[25,-6,7,8,8],[7,-25,8,7,6],[22,27,9,6,6],[-7,25,8,5,5]];
  const wall=makeMat(0x535550),trim=makeMat(0x72746d),glass=makeMat(palette.ink);
  for(const [x,z,w,h,d] of blocks){
    root.add(boxMesh([w,h,d],[x,h/2,z],wall));
    root.add(boxMesh([w+.4,.22,d+.4],[x,h+.11,z],trim));
    for(let y=2;y<h-1;y+=2)for(let dx=-w/2+1;dx<w/2-.5;dx+=1.5)
      root.add(boxMesh([.55,.85,.08],[x+dx,y,z+d/2+.02],glass));
  }
  const distant=makeMat(0x232728);
  for(let i=0;i<10;i++){
    const h=4+(i*7%9);root.add(boxMesh([4.8,h,5],[-36+i*7+(i%3)*1.1,h/2,-37-(i%2)*4],distant));
  }
}

function addAlleyStage(root, palette) {
  const ink=makeMat(palette.ink);
  const glow=new THREE.MeshBasicMaterial({color:palette.paper});
  // Existing alley follows an L bend. A service door on the west wall lights that route.
  root.add(boxMesh([.06,2.8,1.5],[16.85,1.4,-26],glow));
  const figure=new THREE.Group();
  const coat=new THREE.Mesh(new THREE.CylinderGeometry(.27,.38,.78,6),ink);
  coat.position.y=.83; figure.add(coat);
  figure.add(boxMesh([.62,.52,.36],[0,1.30,0],ink));
  for(const side of [-1,1]){
    const sleeve=boxMesh([.19,.73,.23],[side*.35,1.04,0],ink);sleeve.rotation.z=side*.10;figure.add(sleeve);
  }
  for(const x of [-.16,.16])figure.add(boxMesh([.17,.6,.2],[x,.3,0],ink));
  const head=new THREE.Mesh(new THREE.SphereGeometry(.23,8,5),makeMat(0x030303));head.position.y=1.78;figure.add(head);
  const hat=new THREE.Mesh(new THREE.CylinderGeometry(.38,.38,.065,8),ink);hat.position.y=1.94;figure.add(hat);
  const crown=new THREE.Mesh(new THREE.CylinderGeometry(.23,.25,.20,8),ink);crown.position.y=2.06;figure.add(crown);
  figure.position.set(14.6,0,-25.2);figure.rotation.y=-.45;root.add(figure);
}

function setupLightRig(sceneRoot, sceneName, palette) {
  const group = new THREE.Group();
  const hemi = new THREE.HemisphereLight(0xaeb6b9, 0x080808, sceneName==='city' ? .14 : .12);
  group.add(hemi);
  const key = new THREE.DirectionalLight(0xf2e7d2, sceneName==='city' ? 4.8 : 1.2);
  key.position.set(sceneName==='city' ? 28 : 12, sceneName==='city' ? 32 : 18, sceneName==='city' ? 20 : 10);
  key.castShadow=true;
  key.shadow.mapSize.set(2048,2048);
  key.shadow.camera.near=.5; key.shadow.camera.far=130;
  key.shadow.camera.left=-55; key.shadow.camera.right=55; key.shadow.camera.top=55; key.shadow.camera.bottom=-55;
  key.shadow.bias=-0.00018;
  group.add(key);
  if(sceneName==='alley'){
    const door = new THREE.SpotLight(palette.paper, 140, 22, Math.PI*.27, 0, 1);
    door.position.set(16.8,2.4,-26); door.target.position.set(11.5,0,-22); door.castShadow=true;
    group.add(door,door.target);
    const rim = new THREE.DirectionalLight(0xc8d2d5,2.0); rim.position.set(10,16,-40); group.add(rim);
  }
  sceneRoot.add(group);
  return group;
}

export async function buildWorld(scene, profile) {
  const modelBounds = {};
  const roots = {city:new THREE.Group(), alley:new THREE.Group()};
  roots.city.name='city'; roots.alley.name='alley';
  scene.add(roots.city, roots.alley);

  const [cityModel, theatreModel, alleyModel] = await Promise.all([
    loader.loadAsync(CITY_URL),
    loader.loadAsync(THEATRE_URL),
    loader.loadAsync(ALLEY_URL),
  ]);

  for (const [name, model] of Object.entries({city:cityModel,theatre:theatreModel,alley:alleyModel})) {
    model.updateMatrixWorld(true);
    const box=new THREE.Box3().setFromObject(model);
    modelBounds[name]={min:box.min.toArray(),max:box.max.toArray(),size:box.getSize(new THREE.Vector3()).toArray(),meshes:[]};
    model.traverse(o=>{if(o.isMesh){const b=new THREE.Box3().setFromObject(o);modelBounds[name].meshes.push({name:o.name,size:b.getSize(new THREE.Vector3()).toArray()});}});
  }
  const cityDetails=[];
  cityModel.traverse(o=>{if(o.isMesh&&!/^(BUILDINGS$|ROOFTOPS$|PADS$|干道$|酒店$|OldTownBar_Body$|地块_|Apartment_C2_Body$|桥廊公寓$|上层桥廊$|公园主体$|GREEN$|WIN_)/.test(o.name))cityDetails.push(o);});
  for(const o of cityDetails)o.removeFromParent();
  replaceMaterials(cityModel, profile.palette, 'context');
  normalize(cityModel, profile.city.extent, profile.city.rotationY);
  roots.city.add(cityModel);

  theatreModel.traverse(o=>{if(o.isMesh) o.material=makeMat(profile.palette.pale);});
  normalize(theatreModel, 13, -0.05);
  theatreModel.position.set(-7,0,-9);
  theatreModel.scale.multiplyScalar(1.28);
  // The theatre export consists only of outline shells. Keep its bounds as reference; use a clean stage landmark.
  addCityStage(roots.city, profile.palette);
  addCityMasses(roots.city, profile.palette);

  replaceMaterials(alleyModel, profile.palette, 'context');
  normalize(alleyModel, profile.alley.extent, profile.alley.rotationY);
  roots.alley.add(alleyModel);
  addAlleyStage(roots.alley, profile.palette);

  const lines = { city: createLineLayer(roots.city,profile), alley: createLineLayer(roots.alley,profile) };
  roots.city.add(lines.city); roots.alley.add(lines.alley);

  setupLightRig(roots.city,'city',profile.palette);
  setupLightRig(roots.alley,'alley',profile.palette);

  const floorMat = stageFloor(56);
  const cityFloor = boxMesh([150,.6,150],[0,-.35,0],floorMat); roots.city.add(cityFloor);
  const alleyFloor = boxMesh([60,.4,96],[0,-.25,0],stageFloor(80)); roots.alley.add(alleyFloor);

  return {
    roots, lines, modelBounds,
    setActive(name){
      roots.city.visible = name==='city';
      roots.alley.visible = name==='alley';
    },
    setLinesVisible(v){ lines.city.visible=v; lines.alley.visible=v; },
  };
}

