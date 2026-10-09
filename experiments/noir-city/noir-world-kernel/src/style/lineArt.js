import * as THREE from 'three';
import { Line2 } from 'three/addons/lines/Line2.js';
import { LineGeometry } from 'three/addons/lines/LineGeometry.js';
import { LineMaterial } from 'three/addons/lines/LineMaterial.js';
import { LineSegments2 } from 'three/addons/lines/LineSegments2.js';
import { LineSegmentsGeometry } from 'three/addons/lines/LineSegmentsGeometry.js';

export const lineMaterials = new Set();

function material(color=0xffffff, width=2, opacity=1) {
  const m = new LineMaterial({
    color,
    linewidth: width,
    worldUnits: false,
    transparent: opacity < 1,
    opacity,
    depthWrite: false,
    toneMapped: false,
    alphaToCoverage: true
  });
  m.resolution.set(1440,900);
  lineMaterials.add(m);
  return m;
}

export function polyline(parent, points, {
  color=0xffffff,width=2,opacity=1,closed=false,renderOrder=8
}={}) {
  const pts = closed ? [...points, points[0]] : points;
  const geometry = new LineGeometry();
  geometry.setPositions(pts.flat());
  const line = new Line2(geometry, material(color,width,opacity));
  line.computeLineDistances();
  line.renderOrder=renderOrder;
  parent.add(line);
  return line;
}

export function segments(parent, pairs, {
  color=0xffffff,width=1.5,opacity=1,renderOrder=8
}={}) {
  const geometry = new LineSegmentsGeometry();
  geometry.setPositions(pairs.flat());
  const line = new LineSegments2(geometry, material(color,width,opacity));
  line.computeLineDistances();
  line.renderOrder=renderOrder;
  parent.add(line);
  return line;
}

export function rect(parent,x,y,z,w,h,options={}) {
  return polyline(parent,[
    [x-w/2,y-h/2,z],
    [x+w/2,y-h/2,z],
    [x+w/2,y+h/2,z],
    [x-w/2,y+h/2,z]
  ],{...options,closed:true});
}

export function smoothPolyline(parent, points, {
  color=0xffffff,width=2,opacity=1,closed=false,segments=48,renderOrder=8
}={}) {
  const curve=new THREE.CatmullRomCurve3(
    points.map(p=>new THREE.Vector3(...p)),
    closed,
    'catmullrom',
    0.18
  );
  const sampled=curve.getPoints(segments).map(v=>[v.x,v.y,v.z]);
  return polyline(parent,sampled,{color,width,opacity,closed:false,renderOrder});
}

export function resizeLineMaterials(w,h) {
  for(const m of lineMaterials) m.resolution.set(w,h);
}
