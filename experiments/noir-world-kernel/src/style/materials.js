import * as THREE from 'three';

function gradientMap() {
  const data = new Uint8Array([18, 92, 176, 255]);
  const tex = new THREE.DataTexture(data, 4, 1, THREE.RedFormat);
  tex.needsUpdate = true;
  tex.magFilter = THREE.NearestFilter;
  tex.minFilter = THREE.NearestFilter;
  return tex;
}

const gradient = gradientMap();

export function toon(color, {roughness=1}={}) {
  const material = new THREE.MeshToonMaterial({
    color,
    gradientMap: gradient,
    side: THREE.FrontSide
  });
  material.userData.roughness = roughness;
  return material;
}

export function unlit(color, options={}) {
  return new THREE.MeshBasicMaterial({color, ...options});
}

export function line(color, opacity=1) {
  return new THREE.LineBasicMaterial({
    color,
    transparent: opacity < 1,
    opacity,
    depthWrite: false,
    toneMapped: false
  });
}
