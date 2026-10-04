import { addSvgProp } from './SVGProp.js';
import { vectorAsset } from './VectorAssetCatalog.js';

export function addVectorProp(parent,id,{
  position=[0,0,0],
  scale=null,
  rotationY=0,
  mirror=false,
  strokeScale=1,
  fillOverride=null,
  strokeOverride=null
}={}){
  const asset=vectorAsset(id);
  return addSvgProp(parent,asset.svg,{
    position,
    scale:scale??asset.scale,
    pivot:asset.pivot,
    rotationY,
    flipX:mirror,
    strokeScale,
    fillOverride,
    strokeOverride
  });
}
