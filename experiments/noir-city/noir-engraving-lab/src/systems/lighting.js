import * as THREE from 'three';
import { RectAreaLightUniformsLib } from 'three/addons/lights/RectAreaLightUniformsLib.js';

export function createLighting(scene, profile, lampPositions) {
  RectAreaLightUniformsLib.init();

  const p = profile.palette;
  const cfg = profile.lighting;

  const group = new THREE.Group();
  const lightCones = new THREE.Group();
  group.name = 'Lighting';
  lightCones.name = 'NarrativeLightCones';
  scene.add(group, lightCones);

  const key = new THREE.DirectionalLight(cfg.keyColor, cfg.keyIntensity);
  key.position.fromArray(cfg.keyPosition);
  key.castShadow = true;
  key.shadow.mapSize.set(2048, 2048);
  key.shadow.camera.left = -70;
  key.shadow.camera.right = 70;
  key.shadow.camera.top = 48;
  key.shadow.camera.bottom = -48;
  key.shadow.camera.near = 1;
  key.shadow.camera.far = 120;
  group.add(key);

  const fill = new THREE.DirectionalLight(cfg.fillColor, cfg.fillIntensity);
  fill.position.fromArray(cfg.fillPosition);
  group.add(fill);

  const ambient = new THREE.HemisphereLight(
    cfg.ambientSky,
    cfg.ambientGround,
    cfg.ambientIntensity,
  );
  group.add(ambient);

  const coneMaterial = new THREE.MeshBasicMaterial({
    color: p.warm,
    transparent: true,
    opacity: 0.010,
    depthWrite: false,
    blending: THREE.AdditiveBlending,
    side: THREE.DoubleSide,
  });

  // Narrative lights are bounded in space; Object3D layers do not link lights.
  const work = cfg.warehouseWork;
  const warehouseWork = new THREE.RectAreaLight(work.color, work.intensity, work.width, work.height);
  warehouseWork.position.fromArray(work.position);
  warehouseWork.lookAt(...work.target);
  group.add(warehouseWork);

  // A distant cinematography source keeps falloff across the wall restrained.
  // Its cone covers the slot; the mouth canopy and landings make the hard cuts.
  // The cone still excludes the neighbouring street roofs, which are checked
  // in the warehouse and city-compression regression views.
  const cut = cfg.alleyCut;
  const alleyCut = new THREE.SpotLight(
    cut.color, cut.intensity, cut.distance, cut.angle, cut.penumbra, cut.decay,
  );
  alleyCut.position.fromArray(cut.position);
  alleyCut.target.position.fromArray(cut.target);
  alleyCut.shadow.bias = -0.00035;
  alleyCut.castShadow = true;
  alleyCut.shadow.mapSize.set(1024, 1024);
  alleyCut.shadow.camera.near = 1;
  alleyCut.shadow.camera.far = cut.distance;
  group.add(alleyCut, alleyCut.target);

  const lift = cfg.alleyLift;
  const alleyAmbient = new THREE.PointLight(lift.color, lift.intensity, lift.distance, lift.decay);
  alleyAmbient.position.fromArray(lift.position);
  group.add(alleyAmbient);

  const door = cfg.alleyDoor;
  const alleyDoor = new THREE.PointLight(door.color, door.intensity, door.distance, door.decay);
  alleyDoor.position.fromArray(door.position);
  group.add(alleyDoor);

  // Street lamps are visual punctuation by default, not automatic light emitters.
  // This avoids soft circular pools competing with narrative lighting. The optional
  // cone geometry remains available for explicit future experiments.
  for (const position of lampPositions) {
    const cone = new THREE.Mesh(
      new THREE.ConeGeometry(2.8, 4.0, 24, 1, true),
      coneMaterial,
    );
    cone.position.set(position[0], 2.0, position[2]);
    lightCones.add(cone);
  }

  return {
    group,
    lightCones,
    setConesVisible(visible) {
      lightCones.visible = visible;
    },
  };
}

