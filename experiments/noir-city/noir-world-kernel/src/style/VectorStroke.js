import * as THREE from 'three';
import { Line2 } from 'three/addons/lines/Line2.js';
import { LineGeometry } from 'three/addons/lines/LineGeometry.js';
import { LineMaterial } from 'three/addons/lines/LineMaterial.js';
import { LineSegments2 } from 'three/addons/lines/LineSegments2.js';
import { LineSegmentsGeometry } from 'three/addons/lines/LineSegmentsGeometry.js';

const materials=new Set();

function makeMaterial(color=0xffffff,width=2,opacity=1){
  const material=new LineMaterial({
    color,linewidth:width,worldUnits:false,
    transparent:opacity<1,opacity,depthWrite:false,toneMapped:false,alphaToCoverage:true
  });
  material.resolution.set(1440,900);
  materials.add(material);
  return material;
}

export function strokePath(parent,points,{color=0xffffff,width=2,opacity=1,closed=false,renderOrder=8}={}){
  const positions=closed?[...points,points[0]]:points;
  const geometry=new LineGeometry();
  geometry.setPositions(positions.flat());
  const line=new Line2(geometry,makeMaterial(color,width,opacity));
  line.computeLineDistances();line.renderOrder=renderOrder;parent.add(line);return line;
}

export function strokeSmooth(parent,points,{color=0xffffff,width=2,opacity=1,closed=false,samples=48,renderOrder=8}={}){
  const curve=new THREE.CatmullRomCurve3(
    points.map(point=>new THREE.Vector3(...point)),closed,'catmullrom',.18
  );
  return strokePath(parent,curve.getPoints(samples).map(v=>[v.x,v.y,v.z]),{
    color,width,opacity,closed:false,renderOrder
  });
}

export function strokeSegments(parent,pairs,{color=0xffffff,width=1.5,opacity=1,renderOrder=8}={}){
  if(!pairs.length) return null;
  const geometry=new LineSegmentsGeometry();
  geometry.setPositions(pairs.flat());
  const line=new LineSegments2(geometry,makeMaterial(color,width,opacity));
  line.computeLineDistances();line.renderOrder=renderOrder;parent.add(line);return line;
}

export function resizeVectorStrokes(width,height){
  for(const material of materials) material.resolution.set(width,height);
}
