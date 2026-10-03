import * as THREE from 'three';
import { FBXLoader } from 'three/addons/loaders/FBXLoader.js';

const loader = new FBXLoader();

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
  const colors = role === 'hero'
    ? [palette.pale, palette.mid, 0x7c796f]
    : [palette.mid, 0x444642, 0x303230, 0x686862];
  root.traverse((o) => {
    if (!o.isMesh) return;
    o.material = makeMat(colors[index++ % colors.length]);
    o.castShadow = true;
    o.receiveShadow = true;
    o.frustumCulled = true;
  });
}

function normalize(root, extent, rotationY=0) {
  root.rotation.set(0, rotationY, 0);
  root.updateMatrixWorld(true);
  const box = new THREE.Box3().setFromObject(root);
  const size = box.getSize(new THREE.Vector3());
  const scale = extent / Math.max(size.x, size.z, 0.001);
  root.scale.setScalar(scale);
  root.updateMatrixWorld(true);
  const b2 = new THREE.Box3().setFromObject(root);
  const center = b2.getCenter(new THREE.Vector3());
  root.position.x -= center.x;
  root.position.z -= center.z;
  root.position.y -= b2.min.y;
  root.updateMatrixWorld(true);
  return new THREE.Box3().setFromObject(root);
}

function createLineLayer() {
  // Imported FBX geometry is intentionally not converted to EdgesGeometry here.
  // Screen-space selective edges provide the diagnostic line layer without an
  // expensive CPU topology pass over the full city model.
  return new THREE.Group();
}

function boxMesh(size, position, material) {
  const m = new THREE.Mesh(new THREE.BoxGeometry(...size), material);
  m.position.fromArray(position); m.castShadow=true; m.receiveShadow=true; return m;
}

function addCityStage(root, palette) {
  const ink = makeMat(palette.ink);
  const pale = makeMat(palette.pale);
  // One explicit vertical landmark: a theatre-like fly tower and stepped crown.
  root.add(boxMesh([7.5,16,7.5],[-7,8,-9],pale));
  root.add(boxMesh([5.2,4.5,5.2],[-7,18.2,-9],pale));
  root.add(boxMesh([2.1,8,2.1],[-7,24.2,-9],pale));
  root.add(boxMesh([18,0.45,3.0],[-7,7.7,-4.8],ink));
  // foreground stage wings: strong negative shapes but not camera-specific shader tricks.
  root.add(boxMesh([8,28,45],[-40,14,10],ink));
  root.add(boxMesh([8,24,38],[40,12,-2],ink));
}

function addAlleyStage(root, palette) {
  const ink = makeMat(palette.ink);
  const pale = makeMat(palette.pale);
  const warm = new THREE.MeshStandardMaterial({color:palette.warm, emissive:palette.warm, emissiveIntensity:1.5, roughness:1});
  // Door glow, human silhouette, and a few stage-like foreground masks.
  root.add(boxMesh([2.8,4.8,.16],[-2.3,2.4,-8.5],warm));
  const figure = new THREE.Group();
  figure.add(boxMesh([1.0,2.5,.65],[0,2.1,0],ink));
  const head = new THREE.Mesh(new THREE.SphereGeometry(.48,8,6),ink); head.position.set(0,3.8,0); figure.add(head);
  figure.position.set(-.6,0,-5.6); root.add(figure);
  root.add(boxMesh([5,13,17],[-14,6.5,3],ink));
  root.add(boxMesh([4,11,16],[14,5.5,1],ink));
  // fire-escape rhythm independent of imported mesh fidelity
  for(let y=4.5;y<12;y+=3.1){
    const rail=boxMesh([6.5,.12,.16],[5.2,y,-5.4],pale); root.add(rail);
    for(let x=2.2;x<8.3;x+=1.0) root.add(boxMesh([.08,1.2,.08],[x,y+.55,-5.4],pale));
  }
}

function setupLightRig(sceneRoot, sceneName, palette) {
  const group = new THREE.Group();
  const hemi = new THREE.HemisphereLight(0xaeb6b9, 0x080808, sceneName==='city' ? .34 : .22);
  group.add(hemi);
  const key = new THREE.DirectionalLight(0xf2e7d2, sceneName==='city' ? 4.4 : 5.3);
  key.position.set(sceneName==='city' ? 28 : 12, sceneName==='city' ? 52 : 18, sceneName==='city' ? 20 : 10);
  key.castShadow=true;
  key.shadow.mapSize.set(2048,2048);
  key.shadow.camera.near=.5; key.shadow.camera.far=130;
  key.shadow.camera.left=-55; key.shadow.camera.right=55; key.shadow.camera.top=55; key.shadow.camera.bottom=-55;
  key.shadow.bias=-0.00018;
  group.add(key);
  if(sceneName==='alley'){
    const door = new THREE.SpotLight(palette.warm, 58, 27, Math.PI*.23, .15, 1.5);
    door.position.set(-2.0,7,-7.0); door.target.position.set(-.8,1,-1.2); door.castShadow=true;
    group.add(door,door.target);
    const rim = new THREE.DirectionalLight(0xc8d2d5,2.0); rim.position.set(-10,10,-17); group.add(rim);
  }
  sceneRoot.add(group);
  return group;
}

export async function buildWorld(scene, profile) {
  const roots = {city:new THREE.Group(), alley:new THREE.Group()};
  roots.city.name='city'; roots.alley.name='alley';
  scene.add(roots.city, roots.alley);

  const [cityModel, theatreModel, alleyModel] = await Promise.all([
    loader.loadAsync('/assets/models/city.fbx'),
    loader.loadAsync('/assets/models/theatre.fbx'),
    loader.loadAsync('/assets/models/alley.fbx'),
  ]);

  replaceMaterials(cityModel, profile.palette, 'context');
  normalize(cityModel, profile.city.extent, profile.city.rotationY);
  roots.city.add(cityModel);

  replaceMaterials(theatreModel, profile.palette, 'hero');
  normalize(theatreModel, 13, -0.05);
  theatreModel.position.set(-7,0,-9);
  theatreModel.scale.multiplyScalar(1.28);
  roots.city.add(theatreModel);
  addCityStage(roots.city, profile.palette);

  replaceMaterials(alleyModel, profile.palette, 'context');
  normalize(alleyModel, profile.alley.extent, profile.alley.rotationY);
  roots.alley.add(alleyModel);
  addAlleyStage(roots.alley, profile.palette);

  const lines = { city: createLineLayer(), alley: createLineLayer() };
  roots.city.add(lines.city); roots.alley.add(lines.alley);

  setupLightRig(roots.city,'city',profile.palette);
  setupLightRig(roots.alley,'alley',profile.palette);

  const floorMat = makeMat(0x1b1c1c);
  const cityFloor = boxMesh([96,.6,96],[0,-.35,0],floorMat); roots.city.add(cityFloor);
  const alleyFloor = boxMesh([34,.4,38],[0,-.25,0],floorMat); roots.alley.add(alleyFloor);

  return {
    roots, lines,
    setActive(name){
      roots.city.visible = name==='city';
      roots.alley.visible = name==='alley';
    },
    setLinesVisible(v){ lines.city.visible=v; lines.alley.visible=v; },
  };
}
