export const defaultInk={enabled:false,target:'night',fill:'#32424d',detail:75,line:'#d3d7cd',width:1.5,lineOpacity:65,internal:0,threshold:55,glow:0,side:'all',fade:0,grain:0,shift:0,levels:0,pattern:'none',patternOpacity:25,accent:0,accentColor:'#d1ac6b'};
export const flatTreatments=[
 {id:'raw',name:'原图',value:{...defaultInk}},
 {id:'palette',name:'限色＋三档色阶',value:{...defaultInk,enabled:true,detail:100,fill:'#17232e',line:'#b9beb1',levels:3,width:0,lineOpacity:0}},
 {id:'edge',name:'限色＋三档色阶＋细描边',value:{...defaultInk,enabled:true,detail:100,fill:'#17232e',line:'#b9beb1',levels:3,width:1.2,lineOpacity:65}}
];
export const inkPresets=[
 ...flatTreatments.slice(1),
 {name:'原画',value:{...defaultInk}},
 {name:'暗填充与亮区提取',value:{fill:'#04060c',detail:0,line:'#cfe0ff',internal:90,width:1,lineOpacity:85}},
 {name:'单侧金色边光',value:{fill:'#04060c',detail:0,line:'#edd1a0',width:2,lineOpacity:95,side:'right',glow:3}},
 {name:'加粗外缘',value:{detail:35,fill:'#10151d',line:'#d4d0bd',width:2.5,lineOpacity:90,internal:30}},
 {name:'等高线覆盖示意',value:{detail:0,fill:'#04060c',line:'#8fa6d8',width:1,internal:20,pattern:'contour',patternOpacity:60}},
 {name:'青灰与金色色板',value:{fill:'#103238',detail:35,levels:3,line:'#cbb77e',width:1,lineOpacity:80,internal:20}},
 {name:'黑白高对比',value:{detail:0,fill:'#04060c',line:'#eae7de',width:1.2,internal:100,threshold:65}},
 {name:'斜向排线覆盖',value:{detail:0,fill:'#080b10',line:'#d8d0b9',pattern:'hatch',patternOpacity:60,internal:35}},
 {name:'施工辅助线覆盖',value:{detail:0,fill:'#081020',line:'#8fa6d8',width:1,internal:75,pattern:'blueprint',patternOpacity:35}},
 {name:'轮廓辉光',value:{detail:0,fill:'#04060c',line:'#9ecac3',width:1.2,internal:60,glow:4}},
 {name:'渐隐与颗粒',value:{detail:35,fill:'#1b2733',line:'#cfe0ff',width:.8,internal:30,glow:2,fade:30,grain:12}},
 {name:'暗填充与细线组合',value:{detail:0,fill:'#04060c',line:'#cfe0ff',width:1.1,internal:65,glow:1.5,pattern:'contour',patternOpacity:15,accent:25}}
];
const sliders=[['detail','保留原画',0,100,1,'%'],['width','新增线宽',0,5,.1,''],['lineOpacity','描线强度',0,100,1,'%'],['internal','提取原画亮线',0,100,1,'%'],['threshold','亮线明度门槛',5,95,1,'%'],['glow','辉光半径',0,8,.1,''],['fade','下端渐隐范围',0,60,1,'%'],['grain','人物颗粒',0,40,1,'%'],['shift','红蓝边缘错位',0,3,.1,''],['levels','主体色板档数',0,5,1,''],['patternOpacity','覆盖线强度',0,100,1,'%'],['accent','保留暖色区域',0,100,1,'%']];
export function mountInkEditor(onChange,getContext){
 const state={...defaultInk},root=document.querySelector('#ink-editor');
 root.innerHTML=`<summary>人物后处理</summary><p>对当前人物图片调色、提取亮区与添加边缘效果，可用限色、色阶和描边统一画法；人物轮廓和造型仍由原图决定。</p><div class="ink-presets">${inkPresets.map((p,i)=>`<button data-ink-preset="${i}">${p.name}</button>`).join('')}</div><div class="ink-tools"><label><input id="ink-enabled" type="checkbox">启用调整</label><label>人物 <select id="ink-target"><option value="night">夜莺</option><option value="neil">尼尔</option><option value="both">两个人</option></select></label>${[['fill','填充颜色'],['line','描线颜色'],['accentColor','暖色映射']].map(([key,label])=>`<label>${label} <input id="ink-${key}" type="color" value="${state[key]}"></label>`).join('')}<label>边缘位置 <select id="ink-side"><option value="all">完整轮廓</option><option value="left">左侧边光</option><option value="right">右侧边光</option><option value="inner">内侧细边</option></select></label><label>覆盖线 <select id="ink-pattern"><option value="none">关闭</option><option value="contour">弧形等高线示意</option><option value="hatch">斜向版画排线</option><option value="blueprint">施工辅助线示意</option></select></label>${sliders.map(([key,label,min,max,step])=>`<label>${label} <input id="ink-${key}" type="range" min="${min}" max="${max}" step="${step}" value="${state[key]}"><output id="ink-${key}-value"></output></label>`).join('')}<button id="ink-reset">重置调整</button><button id="ink-export">记录当前参数</button></div><textarea id="ink-record" aria-label="当前画面参数" readonly hidden></textarea><p>亮线按原图明度提取，填充与轮廓独立着色；暖色按原图偏暖像素估算，不自动添加红唇或围巾。覆盖线是示意，不是真正的身体等高线。色板 0 保留原色；2～5 将主体明暗映射到填充色与描线色之间的有限色板，辉光、颗粒和点缀会增加最终显示颜色。卷发、五官与身段仍由画稿决定。</p>`;
 function sync(){for(const [key,,,,,unit]of sliders)document.querySelector(`#ink-${key}-value`).textContent=state[key]+unit;onChange({...state})}
 function set(settings){Object.assign(state,settings);for(const key of Object.keys(state)){const input=document.querySelector('#ink-'+key);if(input.type==='checkbox')input.checked=state[key];else input.value=state[key]}sync()}
 for(const key of Object.keys(state)){const input=document.querySelector('#ink-'+key);input.addEventListener('input',()=>{state[key]=input.type==='checkbox'?input.checked:input.type==='range'?Number(input.value):input.value;sync()})}
 for(const button of root.querySelectorAll('[data-ink-preset]'))button.addEventListener('click',()=>{const preset=inkPresets[Number(button.dataset.inkPreset)];set({...defaultInk,enabled:true,target:'night',...preset.value});});
 document.querySelector('#ink-reset').addEventListener('click',()=>set(defaultInk));
 document.querySelector('#ink-export').addEventListener('click',()=>{const record=document.querySelector('#ink-record');record.hidden=false;record.value=JSON.stringify({version:2,...getContext(),ink:{...state}},null,2);record.focus();record.select()});
 sync();return {set};
}
function patternImage(kind,color,opacity){
 let lines='';if(kind==='contour')for(let y=5;y<100;y+=8)lines+=`<path d="M-10 ${y}Q50 ${y+15} 110 ${y}"/>`;else if(kind==='hatch')for(let x=-100;x<200;x+=7)lines+=`<path d="M${x} 0l100 100"/>`;else if(kind==='blueprint'){lines='<path d="M50 0v100M20 0v100M80 0v100M0 25h100M0 50h100M0 75h100"/>';for(let y=5;y<100;y+=5)lines+=`<path d="M47 ${y}h6"/>`}
 return 'data:image/svg+xml,'+encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><g fill="none" stroke="${color}" stroke-width=".3" opacity="${opacity/100}">${lines}</g></svg>`);
}
export function applyInk(stage,settings){
 const s={...defaultInk,...settings};let svg=stage.querySelector('.sprite-filters');
 if(!svg){svg=document.createElementNS('http://www.w3.org/2000/svg','svg');svg.classList.add('sprite-filters');svg.setAttribute('aria-hidden','true');stage.append(svg)}
 const id=`${stage.dataset.svgPrefix}-ink`,scale=stage.clientWidth/1600,keep=s.detail/100,radius=s.width*scale,threshold=s.threshold/100;
 const color=(value,result,mask,opacity=1)=>`<feFlood flood-color="${value}" flood-opacity="${opacity}" result="${result}Color"/><feComposite in="${result}Color" in2="${mask}" operator="in" result="${result}"/>`;
 const edge=s.side==='inner'?`<feMorphology in="SourceAlpha" operator="erode" radius="${radius}" result="contracted"/><feComposite in="SourceAlpha" in2="contracted" operator="out" result="edge"/>`:s.side==='all'?`<feMorphology in="SourceAlpha" operator="dilate" radius="${radius}" result="expanded"/><feComposite in="expanded" in2="SourceAlpha" operator="out" result="edge"/>`:`<feOffset in="SourceAlpha" dx="${(s.side==='left'?-1:1)*radius}" result="expanded"/><feComposite in="expanded" in2="SourceAlpha" operator="out" result="edge"/>`;
 const channelTable=channel=>{const lo=parseInt(s.fill.slice(1+channel*2,3+channel*2),16)/255,hi=parseInt(s.line.slice(1+channel*2,3+channel*2),16)/255;return Array.from({length:s.levels},(_,i)=>lo+(hi-lo)*i/(s.levels-1)).join(' ')};
 const quant=s.levels>=2?`<feColorMatrix in="body" type="saturate" values="0" result="body"/><feComponentTransfer in="body" result="body">${['R','G','B'].map((channel,i)=>`<feFunc${channel} type="discrete" tableValues="${channelTable(i)}"/>`).join('')}</feComponentTransfer>`:'';
 const internal=s.internal?`<feColorMatrix in="SourceGraphic" type="matrix" values="0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 .2126 .7152 .0722 0 0" result="luma"/><feComponentTransfer in="luma" result="luma"><feFuncA type="linear" slope="${1/(1-threshold)}" intercept="${-threshold/(1-threshold)}"/></feComponentTransfer><feComposite in="luma" in2="SourceAlpha" operator="in" result="luma"/>${color(s.line,'internal','luma',s.internal/100)}`:'';
 const pattern=s.pattern!=='none'?`<feImage href="${patternImage(s.pattern,s.line,s.patternOpacity)}" x="0" y="0" width="100%" height="100%" preserveAspectRatio="none" result="patternSource"/><feComposite in="patternSource" in2="SourceAlpha" operator="in" result="pattern"/>`:'';
 const accent=s.accent?`<feColorMatrix in="SourceGraphic" type="matrix" values="0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 3 0 -3 0 -.08" result="warm"/><feComposite in="warm" in2="SourceAlpha" operator="in" result="warm"/>${color(s.accentColor,'accent','warm',s.accent/100)}`:'';
 const grain=s.grain?`<feTurbulence type="fractalNoise" baseFrequency=".7" numOctaves="2" seed="17" result="noise"/><feColorMatrix in="noise" type="saturate" values="0" result="noise"/><feComponentTransfer in="noise" result="noise"><feFuncA type="linear" slope="${s.grain/100}"/></feComponentTransfer><feComposite in="noise" in2="SourceAlpha" operator="in" result="noise"/>`:'';
 const shift=s.shift?`<feOffset in="edge" dx="${s.shift*scale}" result="redEdge"/>${color('#b96565','red','redEdge',.35)}<feOffset in="edge" dx="${-s.shift*scale}" result="blueEdge"/>${color('#719bc8','blue','blueEdge',.35)}`:'';
 svg.innerHTML=`<defs><filter id="${id}" x="-20%" y="-20%" width="140%" height="140%" color-interpolation-filters="sRGB">${color(s.fill,'solid','SourceAlpha')}<feComposite in="SourceGraphic" in2="solid" operator="arithmetic" k1="0" k2="${keep}" k3="${1-keep}" k4="0" result="body"/>${quant}${edge}${color(s.line,'outline','edge',s.lineOpacity/100)}${internal}${pattern}${accent}${grain}${shift}<feMerge result="lines"><feMergeNode in="outline"/>${s.internal?'<feMergeNode in="internal"/>':''}${pattern?'<feMergeNode in="pattern"/>':''}</feMerge>${s.glow?`<feGaussianBlur in="lines" stdDeviation="${s.glow*scale}" result="glow"/>`:''}<feMerge>${s.glow?'<feMergeNode in="glow"/>':''}${shift?'<feMergeNode in="red"/><feMergeNode in="blue"/>':''}<feMergeNode in="body"/><feMergeNode in="lines"/>${accent?'<feMergeNode in="accent"/>':''}${grain?'<feMergeNode in="noise"/>':''}</feMerge></filter></defs>`;
 const grade=stage.dataset.person==='tone'?' saturate(.32) brightness(.85) contrast(1.12)':stage.dataset.person==='light'?' saturate(.3) brightness(.8) contrast(1.12)':'';
 for(const actor of ['neil','night']){const img=stage.querySelector('.'+actor+' img'),active=s.enabled&&(actor===s.target||s.target==='both');img.style.filter=active?`url(#${id})${grade}`:'';img.style.maskImage=active&&s.fade?`linear-gradient(to bottom,#000 ${100-s.fade}%,transparent 100%)`:''}
}
