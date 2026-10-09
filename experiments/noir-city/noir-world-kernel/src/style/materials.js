import * as THREE from 'three';
import { LineMaterial } from 'three/addons/lines/LineMaterial.js';

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
  return new THREE.MeshBasicMaterial({color, toneMapped:false, ...options});
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


export function wideLine(color, opacity=1, width=1.4) {
  const material = new LineMaterial({
    color,
    linewidth: width,
    transparent: opacity < 1,
    opacity,
    depthWrite: false,
    toneMapped: false
  });
  material.resolution.set(
    typeof window !== 'undefined' ? window.innerWidth : 1440,
    typeof window !== 'undefined' ? window.innerHeight : 900
  );
  return material;
}
