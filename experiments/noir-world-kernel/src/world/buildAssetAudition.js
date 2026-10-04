import * as THREE from 'three';
import { addPuppetCharacter } from './PuppetCharacter.js';
import { addVectorProp } from './StreetProps.js';
import { vectorAsset } from './VectorAssetCatalog.js';
import { strokeSegments, resizeVectorStrokes } from '../style/VectorStroke.js';

const P={ink:0x010205,deep:0x0b0e14,white:0xf2efe6,gold:0xe2b63d};
const standard=color=>new THREE.MeshStandardMaterial({color,roughness:.98,metalness:0});

export function buildAssetAudition(scene){
  const root=new THREE.Group();
  const stage=new THREE.Group();
  const strokes=new THREE.Group();
  root.add(stage,strokes);
  scene.add(root);

  const floor=new THREE.Mesh(new THREE.PlaneGeometry(12,8),standard(P.ink));
  floor.rotation.x=-Math.PI/2;floor.position.z=0;stage.add(floor);

  const back=new THREE.Mesh(new THREE.PlaneGeometry(11,5.5),standard(P.deep));
  back.position.set(0,2.7,-1.2);stage.add(back);

  strokeSegments(strokes,[
    [-3.0,.02,.1,3.0,.02,.1],
    [0,.02,.1,0,3.5,.1],
    [-2.2,1.0,.1,2.2,1.0,.1],
    [-2.2,2.0,.1,2.2,2.0,.1],
    [-2.2,3.0,.1,2.2,3.0,.1]
  ],{color:P.white,width:.75,opacity:.17});

  let current=null;
  let currentId=null;
  let currentType=null;

  function clearAsset(){
    if(current){
      stage.remove(current);
      current.traverse(object=>{
        object.geometry?.dispose?.();
        if(Array.isArray(object.material)) object.material.forEach(material=>material.dispose?.());
        else object.material?.dispose?.();
      });
      current=null;
    }
  }

  function setAsset(id){
    clearAsset();
    const asset=vectorAsset(id);
    currentId=id;currentType=asset.type;
    if(asset.type==='puppet'){
      current=addPuppetCharacter(stage,{kind:id,position:[0,.02,.2],strokeScale:.88});
    }else{
      current=addVectorProp(stage,id,{position:[0,.02,.2],strokeScale:.86});
    }
    return asset;
  }

  return {
    root,stage,strokes,
    setVisible(visible){root.visible=visible;},
    setMode(mode){
      stage.visible=mode!=='line';
      strokes.visible=mode!=='shape';
    },
    setAsset,
    info(){return {assetId:currentId,type:currentType};},
    cameraSpec(){
      return currentType==='puppet'
        ? {pos:[0,1.75,7.4],target:[0,1.55,.15],fov:28}
        : {pos:[0,1.55,6.5],target:[0,.85,.15],fov:30};
    },
    resize(width,height){resizeVectorStrokes(width,height);}
  };
}
