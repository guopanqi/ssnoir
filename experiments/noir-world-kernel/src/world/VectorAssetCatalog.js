import detectiveStand from '../assets/detective-study.svg?raw';
import detectiveWalk from '../assets/detective-walk.svg?raw';
import detectiveTurn from '../assets/detective-turn.svg?raw';
import pedestrianCoat from '../assets/pedestrian-study.svg?raw';
import pedestrianDress from '../assets/pedestrian-dress.svg?raw';
import pedestrianWorker from '../assets/pedestrian-worker.svg?raw';
import pedestrianShortcoat from '../assets/pedestrian-shortcoat.svg?raw';
import taxi from '../assets/taxi-study.svg?raw';
import sedan from '../assets/sedan-study.svg?raw';
import phoneBooth from '../assets/phone-booth.svg?raw';
import bench from '../assets/bench.svg?raw';
import trashCan from '../assets/trash-can.svg?raw';
import awning from '../assets/awning.svg?raw';
import streetSign from '../assets/street-sign.svg?raw';
import treeColumn from '../assets/tree-column.svg?raw';

export const VECTOR_ASSETS=Object.freeze({
  'character.detective.stand':{svg:detectiveStand,type:'puppet',pivot:[60,310],scale:.0104,tags:['detective','coat','fedora','stand']},
  'character.detective.walk':{svg:detectiveWalk,type:'puppet',pivot:[60,310],scale:.0104,tags:['detective','coat','fedora','walk']},
  'character.detective.turn':{svg:detectiveTurn,type:'puppet',pivot:[60,310],scale:.0104,tags:['detective','coat','fedora','turn']},
  'character.pedestrian.coat':{svg:pedestrianCoat,type:'puppet',pivot:[60,310],scale:.0092,tags:['pedestrian','coat']},
  'character.pedestrian.dress':{svg:pedestrianDress,type:'puppet',pivot:[60,310],scale:.0090,tags:['pedestrian','dress']},
  'character.pedestrian.worker':{svg:pedestrianWorker,type:'puppet',pivot:[60,310],scale:.0091,tags:['pedestrian','worker','cap']},
  'character.pedestrian.shortcoat':{svg:pedestrianShortcoat,type:'puppet',pivot:[60,310],scale:.0091,tags:['pedestrian','shortcoat']},
  'vehicle.taxi':{svg:taxi,type:'prop',pivot:[150,110],scale:.0108,tags:['vehicle','taxi','gold']},
  'vehicle.sedan':{svg:sedan,type:'prop',pivot:[150,110],scale:.0108,tags:['vehicle','sedan']},
  'street.phone-booth':{svg:phoneBooth,type:'prop',pivot:[60,244],scale:.0104,tags:['street','phone','booth']},
  'street.bench':{svg:bench,type:'prop',pivot:[120,111],scale:.0102,tags:['street','bench']},
  'street.trash-can':{svg:trashCan,type:'prop',pivot:[50,144],scale:.0098,tags:['street','trash']},
  'street.awning':{svg:awning,type:'prop',pivot:[130,78],scale:.0120,tags:['street','awning','gold']},
  'street.sign':{svg:streetSign,type:'prop',pivot:[60,270],scale:.0100,tags:['street','sign','gold']},
  'nature.tree-column':{svg:treeColumn,type:'prop',pivot:[90,311],scale:.0105,tags:['nature','tree']}
});

export function vectorAsset(id){
  const asset=VECTOR_ASSETS[id];
  if(!asset) throw new Error(`Unknown vector asset: ${id}`);
  return asset;
}

export function vectorAssetIds({type=null}={}){
  return Object.entries(VECTOR_ASSETS)
    .filter(([,asset])=>!type||asset.type===type)
    .map(([id])=>id);
}
