import * as THREE from 'three';
import { SVGLoader } from 'three/addons/loaders/SVGLoader.js';

const loader=new SVGLoader();
const parsedCache=new Map();

function parsed(svgText){
  if(!parsedCache.has(svgText)) parsedCache.set(svgText,loader.parse(svgText));
  return parsedCache.get(svgText);
}

function colorMaterial(color,{depthWrite=true,opacity=1}={}){
  return new THREE.MeshBasicMaterial({
    color:new THREE.Color(color),
    side:THREE.DoubleSide,
    toneMapped:false,
    transparent:opacity<1,
    opacity,
    depthWrite
  });
}

export function createSvgArtwork(svgText,{
  scale=.01,
  pivot=[0,0],
  rotationY=0,
  flipX=false,
  strokeScale=1,
  fillOverride=null,
  strokeOverride=null
}={}){
  const root=new THREE.Group();
  root.rotation.y=rotationY;

  const artwork=new THREE.Group();
  const sx=flipX?-scale:scale;
  artwork.scale.set(sx,-scale,scale);
  artwork.position.set((flipX?pivot[0]:-pivot[0])*scale,pivot[1]*scale,0);
  root.add(artwork);

  let order=10;
  for(const path of parsed(svgText).paths){
    const style=path.userData.style;

    if(style.fill && style.fill!=='none'){
      const fill=fillOverride??style.fill;
      const opacity=Number(style.fillOpacity??1);
      for(const shape of SVGLoader.createShapes(path)){
        const mesh=new THREE.Mesh(new THREE.ShapeGeometry(shape),colorMaterial(fill,{opacity}));
        mesh.renderOrder=order++;artwork.add(mesh);
      }
    }

    if(style.stroke && style.stroke!=='none'){
      const stroke=strokeOverride??style.stroke;
      const strokeStyle={
        ...style,
        stroke,
        strokeWidth:String(Math.max(.25,parseFloat(style.strokeWidth??'1')*strokeScale))
      };
      const material=colorMaterial(stroke,{
        opacity:Number(style.strokeOpacity??1),
        depthWrite:false
      });
      for(const subPath of path.subPaths){
        const geometry=SVGLoader.pointsToStroke(subPath.getPoints(42),strokeStyle);
        if(!geometry) continue;
        const mesh=new THREE.Mesh(geometry,material);
        mesh.position.z=.025;mesh.renderOrder=order++;artwork.add(mesh);
      }
    }
  }
  return root;
}

export function addSvgProp(parent,svgText,{
  position=[0,0,0],
  ...options
}={}){
  const prop=createSvgArtwork(svgText,options);
  prop.position.set(...position);parent.add(prop);return prop;
}
