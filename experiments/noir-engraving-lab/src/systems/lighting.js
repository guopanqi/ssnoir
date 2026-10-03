import * as THREE from 'three';

export function createLighting(scene, profile, lampPositions) {
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
  key.shadow.camera.left = -55;
  key.shadow.camera.right = 55;
  key.shadow.camera.top = 42;
  key.shadow.camera.bottom = -42;
  key.shadow.camera.near = 1;
  key.shadow.camera.far = 95;
  group.add(key);

  const fill = new THREE.DirectionalLight(cfg.fillColor, cfg.fillIntensity);
  fill.position.fromArray(cfg.fillPosition);
  group.add(fill);

  group.add(new THREE.HemisphereLight(
    cfg.ambientSky,
    cfg.ambientGround,
    cfg.ambientIntensity,
  ));

  const coneMaterial = new THREE.MeshBasicMaterial({
    color: p.warm,
    transparent: true,
    opacity: 0.045,
    depthWrite: false,
    blending: THREE.AdditiveBlending,
    side: THREE.DoubleSide,
  });

  for (const position of lampPositions) {
    const light = new THREE.PointLight(
      cfg.streetColor,
      cfg.streetIntensity,
      cfg.streetDistance,
      2.1,
    );
    light.position.fromArray(position);
    light.castShadow = false;
    group.add(light);

    // ConeGeometry 顶点在 +Y，底面在 -Y；灯头高约 4.4m，
    // 因此高 4.4m 的锥体中心放 2.2m，正好从灯头落到地面。
    const cone = new THREE.Mesh(
      new THREE.ConeGeometry(3.4, 4.4, 24, 1, true),
      coneMaterial,
    );
    cone.position.set(position[0], 2.2, position[2]);
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
