import detectiveSvg from '../assets/detective-study.svg?raw';
import pedestrianSvg from '../assets/pedestrian-study.svg?raw';
import { addSvgProp } from './SVGProp.js';

const LIBRARY={
  detective:{svg:detectiveSvg,pivot:[60,310],scale:.0104},
  pedestrian:{svg:pedestrianSvg,pivot:[60,310],scale:.0092}
};

export function addPuppetCharacter(parent,{
  kind='detective',
  position=[0,.02,0],
  scale=null,
  rotationY=0,
  mirror=false,
  strokeScale=1
}={}){
  const preset=LIBRARY[kind]??LIBRARY.detective;
  return addSvgProp(parent,preset.svg,{
    position,
    scale:scale??preset.scale,
    pivot:preset.pivot,
    rotationY,
    flipX:mirror,
    strokeScale
  });
}

export const puppetLibrary=Object.freeze(Object.keys(LIBRARY));
