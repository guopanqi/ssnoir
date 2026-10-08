import { addSvgProp } from './SVGProp.js';
import { vectorAsset } from './VectorAssetCatalog.js';

const ALIASES=Object.freeze({
  detective:'character.detective.stand',
  pedestrian:'character.pedestrian.coat',
  'detective-walk':'character.detective.walk',
  'detective-turn':'character.detective.turn',
  dress:'character.pedestrian.dress',
  worker:'character.pedestrian.worker',
  shortcoat:'character.pedestrian.shortcoat'
});

export function addPuppetCharacter(parent,{
  kind='detective',
  position=[0,.02,0],
  scale=null,
  rotationY=0,
  mirror=false,
  strokeScale=1
}={}){
  const id=ALIASES[kind]??kind;
  const asset=vectorAsset(id);
  if(asset.type!=='puppet') throw new Error(`${id} is not a puppet asset`);
  return addSvgProp(parent,asset.svg,{
    position,
    scale:scale??asset.scale,
    pivot:asset.pivot,
    rotationY,
    flipX:mirror,
    strokeScale
  });
}

export const puppetLibrary=Object.freeze(Object.values(ALIASES));
