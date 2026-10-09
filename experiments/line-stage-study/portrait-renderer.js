import {defaultInk,effectiveAdjustments} from './portrait-settings.js';
function patternImage(kind,color,opacity){
 let lines='';if(kind==='contour')for(let y=5;y<100;y+=8)lines+=`<path d="M-10 ${y}Q50 ${y+15} 110 ${y}"/>`;else if(kind==='hatch')for(let x=-100;x<200;x+=7)lines+=`<path d="M${x} 0l100 100"/>`;else if(kind==='blueprint'){lines='<path d="M50 0v100M20 0v100M80 0v100M0 25h100M0 50h100M0 75h100"/>';for(let y=5;y<100;y+=5)lines+=`<path d="M47 ${y}h6"/>`}
 return 'data:image/svg+xml,'+encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><g fill="none" stroke="${color}" stroke-width=".3" opacity="${opacity/100}">${lines}</g></svg>`);
}
function applySilhouette(stage,s){
 for(const actor of ['neil','night']){
  const parent=stage.querySelector('.'+actor);let backing=parent.querySelector('.silhouette-backing');
  if(!backing){backing=document.createElement('div');backing.className='silhouette-backing';parent.prepend(backing)}
  const active=s.silhouette&&stage.dataset.style==='originalNeon'&&(s.silhouetteTarget===actor||s.silhouetteTarget==='both');
  backing.style.display=active?'block':'none';backing.style.background=s.silhouetteColor;backing.style.opacity=s.silhouetteOpacity/100;
  backing.style.maskImage=`url(assets/original-neon/${actor}-silhouette.svg)`;backing.style.filter=`blur(${s.silhouetteSoftness*stage.clientWidth/1600}px)`;
 }
}
export function applyInk(stage,settings){
 const s=effectiveAdjustments(settings);
 applySilhouette(stage,s);
 let svg=stage.querySelector('.sprite-filters');
 if(!svg){svg=document.createElementNS('http://www.w3.org/2000/svg','svg');svg.classList.add('sprite-filters');svg.setAttribute('aria-hidden','true');stage.append(svg)}
 const id=`${stage.dataset.svgPrefix}-ink`,scale=stage.clientWidth/1600,keep=s.detail/100,radius=s.width*scale,threshold=s.threshold/100;
 const color=(value,result,mask,opacity=1)=>`<feFlood flood-color="${value}" flood-opacity="${opacity}" result="${result}Color"/><feComposite in="${result}Color" in2="${mask}" operator="in" result="${result}"/>`;
 const edge=s.side==='inner'?`<feMorphology in="SourceAlpha" operator="erode" radius="${radius}" result="contracted"/><feComposite in="SourceAlpha" in2="contracted" operator="out" result="edge"/>`:s.side==='all'?`<feMorphology in="SourceAlpha" operator="dilate" radius="${radius}" result="expanded"/><feComposite in="expanded" in2="SourceAlpha" operator="out" result="edge"/>`:`<feOffset in="SourceAlpha" dx="${(s.side==='left'?-1:1)*radius}" result="expanded"/><feComposite in="expanded" in2="SourceAlpha" operator="out" result="edge"/>`;
 const channelTable=channel=>{const lo=parseInt(s.fill.slice(1+channel*2,3+channel*2),16)/255,hi=parseInt(s.paletteLight.slice(1+channel*2,3+channel*2),16)/255;return Array.from({length:s.levels},(_,i)=>lo+(hi-lo)*i/(s.levels-1)).join(' ')};
 const quant=s.levels>=2?`<feColorMatrix in="body" type="saturate" values="0" result="body"/><feComponentTransfer in="body" result="body">${['R','G','B'].map((channel,i)=>`<feFunc${channel} type="discrete" tableValues="${channelTable(i)}"/>`).join('')}</feComponentTransfer>`:'';
 const internal=s.internal?`<feColorMatrix in="SourceGraphic" type="matrix" values="0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 .2126 .7152 .0722 0 0" result="luma"/><feComponentTransfer in="luma" result="luma"><feFuncA type="linear" slope="${1/(1-threshold)}" intercept="${-threshold/(1-threshold)}"/></feComponentTransfer><feComposite in="luma" in2="SourceAlpha" operator="in" result="luma"/>${color(s.line,'internal','luma',s.internal/100)}`:'';
 const pattern=s.pattern!=='none'?`<feImage href="${patternImage(s.pattern,s.patternColor,s.patternOpacity)}" x="0" y="0" width="100%" height="100%" preserveAspectRatio="none" result="patternSource"/><feComposite in="patternSource" in2="SourceAlpha" operator="in" result="pattern"/>`:'';
 const accent=s.accent?`<feColorMatrix in="SourceGraphic" type="matrix" values="0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 3 0 -3 0 -.08" result="warm"/><feComposite in="warm" in2="SourceAlpha" operator="in" result="warm"/>${color(s.accentColor,'accent','warm',s.accent/100)}`:'';
 const grain=s.grain?`<feTurbulence type="fractalNoise" baseFrequency=".7" numOctaves="2" seed="17" result="noise"/><feColorMatrix in="noise" type="saturate" values="0" result="noise"/><feComponentTransfer in="noise" result="noise"><feFuncA type="linear" slope="${s.grain/100}"/></feComponentTransfer><feComposite in="noise" in2="SourceAlpha" operator="in" result="noise"/>`:'';
 const shift=s.shift?`<feOffset in="edge" dx="${s.shift*scale}" result="redEdge"/>${color('#b96565','red','redEdge',.35)}<feOffset in="edge" dx="${-s.shift*scale}" result="blueEdge"/>${color('#719bc8','blue','blueEdge',.35)}`:'';
 svg.innerHTML=`<defs><filter id="${id}" x="-20%" y="-20%" width="140%" height="140%" color-interpolation-filters="sRGB">${color(s.fill,'solid','SourceAlpha')}<feComposite in="SourceGraphic" in2="solid" operator="arithmetic" k1="0" k2="${keep}" k3="${1-keep}" k4="0" result="body"/>${quant}${edge}${color(s.line,'outline','edge',s.lineOpacity/100)}${internal}${pattern}${accent}${grain}${shift}<feMerge result="edgeLines"><feMergeNode in="outline"/>${s.internal?'<feMergeNode in="internal"/>':''}</feMerge>${s.glow?`<feGaussianBlur in="edgeLines" stdDeviation="${s.glow*scale}" result="glow"/>`:''}<feMerge>${s.glow?'<feMergeNode in="glow"/>':''}${shift?'<feMergeNode in="red"/><feMergeNode in="blue"/>':''}<feMergeNode in="body"/><feMergeNode in="edgeLines"/>${pattern?'<feMergeNode in="pattern"/>':''}${accent?'<feMergeNode in="accent"/>':''}${grain?'<feMergeNode in="noise"/>':''}</feMerge></filter></defs>`;
 const hasTreatment=s.detail!==100||s.width>0||s.internal>0||s.levels>=2||s.pattern!=='none'||s.accent>0||s.grain>0||s.shift>0;
 const grade=stage.dataset.person==='tone'?' saturate(.32) brightness(.85) contrast(1.12)':stage.dataset.person==='light'?' saturate(.3) brightness(.8) contrast(1.12)':'';
 for(const actor of ['neil','night']){const img=stage.querySelector('.'+actor+' img'),active=(s.brightnessEnabled||s.colorEnabled||s.edgesEnabled||s.textureEnabled)&&(actor===s.target||s.target==='both');img.style.filter=active?`${hasTreatment?`url(#${id}) `:''}brightness(${s.brightness/100})${grade}`:'';img.style.maskImage=active&&s.fade?`linear-gradient(to bottom,#000 ${100-s.fade}%,transparent 100%)`:''}
}
