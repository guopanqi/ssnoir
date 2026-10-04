import manifest from '../assets/generated/manifest.generated.json';

const svgModules=import.meta.glob('../assets/generated/*.svg',{
  query:'?raw',
  import:'default',
  eager:true
});

export const GENERATED_VECTOR_ASSETS=Object.freeze(Object.fromEntries(
  manifest.map(entry=>{
    const key=`../assets/generated/${entry.file}`;
    const svg=svgModules[key];
    if(!svg) throw new Error(`Generated vector manifest references missing file: ${entry.file}`);
    return [entry.id,{
      svg,
      type:entry.type,
      pivot:entry.pivot,
      scale:entry.scale,
      tags:entry.tags??['generated'],
      provenance:{
        provider:entry.provider??null,
        model:entry.model??null,
        seed:entry.seed??null
      }
    }];
  })
));
