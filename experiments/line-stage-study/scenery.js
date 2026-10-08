const path=(d,stroke='#8397ad',w=1.5,extra='')=>`<path d="${d}" fill="none" stroke="${stroke}" stroke-width="${w}" ${extra}/>`;
// A stage is deliberately drawn from sparse structural lines, as requested.
const defs=`<defs><linearGradient id="cone" x1="0" y1="0" x2="0" y2="1"><stop stop-color="#edcb8b" stop-opacity=".13"/><stop offset="1" stop-color="#edcb8b" stop-opacity=".015"/></linearGradient><radialGradient id="pool"><stop stop-color="#d0b680" stop-opacity=".12"/><stop offset="1" stop-color="#d0b680" stop-opacity="0"/></radialGradient><filter id="halo"><feGaussianBlur stdDeviation="8"/></filter><linearGradient id="edge"><stop stop-color="#8094b1" stop-opacity="0"/><stop offset=".22" stop-color="#8094b1" stop-opacity=".7"/><stop offset=".78" stop-color="#8094b1" stop-opacity=".7"/><stop offset="1" stop-color="#8094b1" stop-opacity="0"/></linearGradient></defs>`;
export function scenery(scene){
 let architecture='',light='';
 if(scene==='warehouse'){
  architecture+=path('M155 737V186L565 96L1345 195V738','#70849c',2)+path('M155 186L915 277L1345 195 M915 277V737','#a4b4c7',2);
  architecture+=`<g transform="translate(0 -115)">${path('M116 335L915 410L1400 327 M116 335L127 362L915 438L1400 354L1400 327 M915 410V438','#c1c9ca',2.2)}</g>`;
  architecture+=path('M179 250V737 M1330 250V737','#7a8fa3',3);
  architecture+=path('M235 397L558 427V731H235Z M252 418L540 446V713H252Z','#63788e');
  for(let x=285;x<530;x+=48)architecture+=path(`M${x} ${422+(x-252)*.097}V711`,'#34475d',1);
  architecture+=path('M1012 444L1197 422V733H1012Z M1025 457L1183 439V718H1025Z M1137 594H1150','#788fa5',1.8);
  architecture+=path('M608 219L778 241V307L608 291Z M623 235L764 253 M623 271L764 288','#576b81');
  architecture+=path('M0 739H1600 M0 760H1600','url(#edge)',1.6)+path('M180 845L530 772 M1420 850L1170 772','#344458');
  light=`<path d="M782 318L813 320L1490 741H100Z" fill="url(#cone)"/><ellipse cx="794" cy="741" rx="420" ry="46" fill="url(#pool)"/>${path('M795 282V308 M769 315L825 320L815 301L784 298Z','#dac49e',2)}<ellipse cx="799" cy="317" rx="15" ry="4" fill="#e5c892"/><ellipse cx="799" cy="317" rx="31" ry="14" fill="#e5c892" opacity=".25" filter="url(#halo)"/>`;
 }else if(scene==='lamp'){
  architecture=path('M0 739H1600 M0 757H1600','url(#edge)',1.6);
  for(let x=100;x<1600;x+=230)architecture+=path(`M${x} 834h86`,'#33475e',2);
  architecture+=path('M795 739V222 M805 739V222 M779 739H823V724H779Z M772 225H828L815 190H785Z M800 190V177','#9daeb9',2);
  light=`<path d="M794 226H806L1137 741H463Z" fill="url(#cone)"/><ellipse cx="800" cy="740" rx="340" ry="35" fill="url(#pool)"/><circle cx="800" cy="223" r="9" fill="#edd4a0"/><circle cx="800" cy="223" r="30" fill="#edd4a0" opacity=".17" filter="url(#halo)"/>${path('M481 742Q800 702 1119 742 M574 745Q800 720 1026 745','#a89164',1,'opacity=".3"')}`;
 }else throw Error('Unknown scene');
 return defs+`<g class="architecture">${architecture}</g><g class="light">${light}</g>`;
}
