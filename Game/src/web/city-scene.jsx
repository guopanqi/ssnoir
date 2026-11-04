import React, { useEffect, useRef } from "react";
import * as THREE from "three";

/**
 * Presentation-only Three.js viewport. No game state or input is owned here.
 * This deliberately cheap procedural city will be replaced by authored spaces in P3.
 */
export function CityScene() {
  const root = useRef(null);
  useEffect(() => {
    const target = root.current;
    if (!target) return;
    let renderer;
    let observer;
    let request = 0;
    let disposed = false;
    try {
      renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
      renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
      renderer.outputColorSpace = THREE.SRGBColorSpace;
      target.appendChild(renderer.domElement);
      const scene = new THREE.Scene();
      scene.fog = new THREE.Fog(0x0e1316, 11, 32);
      const camera = new THREE.PerspectiveCamera(38, 1, 0.1, 100);
      camera.position.set(11, 7, 14);
      camera.lookAt(0, 2, 0);
      scene.add(new THREE.AmbientLight(0xcfd9e2, 0.9));
      const key = new THREE.DirectionalLight(0xffe4a5, 2.8);
      key.position.set(-3, 13, 6);
      scene.add(key);
      const geometry = new THREE.BoxGeometry(1, 1, 1);
      const wall = new THREE.MeshStandardMaterial({ color: 0x1b2328, roughness: 1 });
      const edges = new THREE.MeshStandardMaterial({ color: 0x394044, roughness: 1 });
      const ground = new THREE.Mesh(new THREE.PlaneGeometry(100, 100),
        new THREE.MeshStandardMaterial({ color: 0x11171a, roughness: 1 }));
      ground.rotation.x = -Math.PI / 2;
      scene.add(ground);
      for (let x = -5; x <= 5; x += 2) {
        for (let z = -5; z <= 5; z += 2) {
          if (Math.abs(x) <= 1 && Math.abs(z) <= 1) continue;
          const height = 1.2 + ((x * x + 3 * z * z + 101) % 7) * 0.58;
          const block = new THREE.Mesh(geometry, (x + z) % 4 === 0 ? edges : wall);
          block.scale.set(1.4, height, 1.4);
          block.position.set(x, height * 0.5, z);
          scene.add(block);
        }
      }
      const glow = new THREE.Mesh(new THREE.SphereGeometry(0.1, 8, 8),
        new THREE.MeshBasicMaterial({ color: 0xe4c589 }));
      glow.position.set(-1.2, 3.2, 1.4);
      scene.add(glow);
      const light = new THREE.PointLight(0xf7d7a0, 12, 7);
      light.position.copy(glow.position);
      scene.add(light);
      const resize = () => {
        const { width, height } = target.getBoundingClientRect();
        if (!width || !height) return;
        renderer.setSize(width, height, false);
        camera.aspect = width / height;
        camera.updateProjectionMatrix();
        renderer.render(scene, camera);
      };
      observer = new ResizeObserver(resize);
      observer.observe(target);
      resize();
      // Static camera: no permanent requestAnimationFrame cost for a UI backdrop.
      return () => {
        disposed = true;
        observer.disconnect();
        cancelAnimationFrame(request);
        scene.traverse(object => {
          if (object.isMesh) {
            object.geometry.dispose();
            if (Array.isArray(object.material)) object.material.forEach(m => m.dispose());
            else object.material.dispose();
          }
        });
        renderer.dispose();
        renderer.domElement.remove();
      };
    } catch (error) {
      console.warn("[SSNoir] WebGL backdrop unavailable", error);
      if (!disposed) {
        observer?.disconnect();
        renderer?.dispose();
        renderer?.domElement.remove();
      }
    }
  }, []);
  return <div ref={root} className="city-canvas" aria-hidden="true" />;
}
