export const defaultInk={silhouette:false,silhouetteTarget:'night',silhouetteColor:'#070b12',silhouetteOpacity:100,silhouetteSoftness:.6,brightnessEnabled:false,colorEnabled:false,edgesEnabled:false,textureEnabled:false,brightness:100,target:'night',fill:'#32424d',detail:100,paletteLight:'#d3d7cd',patternColor:'#d3d7cd',line:'#d3d7cd',width:0,lineOpacity:0,internal:0,threshold:55,glow:0,side:'all',fade:0,grain:0,shift:0,levels:0,pattern:'none',patternOpacity:25,accent:0,accentColor:'#d1ac6b'};
export const flatTreatments=[
 {id:'raw',name:'原图',value:{...defaultInk}},
 {id:'palette',name:'限色＋三档色阶',value:{...defaultInk,detail:100,fill:'#17232e',paletteLight:'#b9beb1',levels:3,width:0,lineOpacity:0}},
 {id:'edge',name:'限色＋三档色阶＋细描边',value:{...defaultInk,detail:100,fill:'#17232e',line:'#b9beb1',levels:3,width:1.2,lineOpacity:65}}
];
export const inkPresets=[
 {name:'只提亮原图',value:{...defaultInk,brightness:140}},
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

export const silhouetteKeys=['silhouette','silhouetteTarget','silhouetteColor','silhouetteOpacity','silhouetteSoftness'];

export const adjustmentModules={
 brightness:{flag:'brightnessEnabled',keys:['brightness']},
 color:{flag:'colorEnabled',keys:['fill','detail','paletteLight','levels','accent','accentColor']},
 edges:{flag:'edgesEnabled',keys:['line','width','lineOpacity','internal','threshold','glow','side','shift']},
 texture:{flag:'textureEnabled',keys:['patternColor','pattern','patternOpacity','grain','fade']}
};
// A preset explicitly enables only the modules it uses; raw presets enable none.
for(const preset of [...flatTreatments,...inkPresets]){
 const v=preset.value;
 if(v.levels>=2&&v.paletteLight===defaultInk.paletteLight)v.paletteLight=v.line;
 if(v.pattern&&v.pattern!=='none')v.patternColor=v.line||defaultInk.patternColor;
 for(const {flag,keys} of Object.values(adjustmentModules))v[flag]=keys.some(k=>k in v&&v[k]!==defaultInk[k]);
}
export function effectiveAdjustments(settings){
 const s={...defaultInk,...settings};
 for(const {flag,keys} of Object.values(adjustmentModules))if(!s[flag])for(const k of keys)s[k]=defaultInk[k];
 return s;
}
