/* 舞台道具助手：gl 移动/淡入任意 SVG 道具；hum 在 #props 放一群轻轻摇晃的剪影；酒保擦杯/上架两个共用动作 */
const gl=(id,x,y,o=1,ms=500,r=0,ease='ease')=>{const e=$('#'+id);if(!e)return;e.style.transition=`transform ${ms}ms ${ease},opacity .25s`;e.style.transform=`translate(${x}px,${y}px) rotate(${r}deg)`;e.style.opacity=o};
const GL=id=>`<g id="${id}" style="opacity:0"><path d="M-11 0L-9 -26H9L11 0Z" fill="#0b0f1c" stroke="#e6eeff" stroke-width="2.5"/><path d="M-10 -9H10" stroke="#e0b060" stroke-width="3"/></g>`;
const hum=list=>{const g=$('#props');list.forEach(([x,y,s,f],i)=>{g.insertAdjacentHTML('beforeend',`<g transform="translate(${x} ${y})"><g style="transform-box:fill-box;transform-origin:50% 100%">${SIL(0,0,s,f)}</g></g>`);g.lastElementChild.firstElementChild.animate([{transform:'rotate(-2.5deg)'},{transform:'rotate(2.5deg)'}],{duration:1500+((i*377)%900),direction:'alternate',iterations:Infinity,easing:'ease-in-out',delay:-i*300})})};
/* 酒保：擦杯口（杯在手边轻晃）、转身把杯子放回架子（整张立绘切换） */
const wipeGlass=async(w,n=3)=>{pose('bm','wipe',{show:1,f:1,face:'n',back:0});gl('gl',923,592,1,0);for(let i=0;i<n;i++){gl('gl',929,590,1,320,8);await w(380);gl('gl',917,594,1,320,-8);await w(380)}};
const shelveGlass=async w=>{pose('bm','turnUp',{f:1,back:1});gl('gl',998,476,1,650);await w(800);pose('bm','idleDown',{f:1,back:0,face:'n'});await w(300)};
const CRATE=id=>`<g id="${id}" style="opacity:0"><rect x="-42" y="-34" width="84" height="68" fill="#07080c" stroke="#cdbf9e" stroke-width="2.5"/><path d="M-42 -34L42 34M42 -34L-42 34" stroke="#8a7a5c" stroke-width="2"/></g>`;
