import * as THREE from 'three';
import { SVGLoader } from 'three/addons/loaders/SVGLoader.js';
import detectiveSvg from '../assets/detective-study.svg?raw';

const loader=new SVGLoader();
const parsed=loader.parse(detectiveSvg);

function fillMaterial(color){
  return new THREE.MeshBasicMaterial({
    color,side:THREE.DoubleSide,toneMapped:false,depthWrite:true
  });
}

export function createSvgDetective({scale=.0102}={}){
  const root=new THREE.Group();
  const artwork=new THREE.Group();
  artwork.scale.set(scale,-scale,scale);
  artwork.position.set(-.61,3.18,0);
  root.add(artwork);

  let order=10;
  for(const path of parsed.paths){
    const style=path.userData.style;

    if(style.fill && style.fill!=='none'){
      const shapes=SVGLoader.createShapes(path);
      for(const shape of shapes){
        const mesh=new THREE.Mesh(
          new THREE.ShapeGeometry(shape),
          fillMaterial(new THREE.Color(style.fill))
        );
        mesh.position.z=0;
        mesh.renderOrder=order++;
        artwork.add(mesh);
      }
    }

    if(style.stroke && style.stroke!=='none'){
      const strokeMat=new THREE.MeshBasicMaterial({
        color:new THREE.Color(style.stroke),
        side:THREE.DoubleSide,
        toneMapped:false,
        transparent:Number(style.strokeOpacity ?? 1)<1,
        opacity:Number(style.strokeOpacity ?? 1),
        depthWrite:false
      });
      for(const subPath of path.subPaths){
        const geo=SVGLoader.pointsToStroke(subPath.getPoints(36),style);
        if(!geo) continue;
        const stroke=new THREE.Mesh(geo,strokeMat);
        stroke.position.z=.025;
        stroke.renderOrder=order++;
        artwork.add(stroke);
      }
    }
  }
  return root;
}

export function addSvgDetective(parent,{x=0,y=.02,z=4.2,scale=.0102}={}){
  const figure=createSvgDetective({scale});
  figure.position.set(x,y,z);
  parent.add(figure);
  return figure;
}
