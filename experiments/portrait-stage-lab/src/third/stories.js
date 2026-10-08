import {enrichScenes} from './expressive-scenes.js';
import {atmospheres} from './atmospheres.js';
import {stories as baselines} from '../baselines/stories.js';
import {stories as originals} from '../stories.js';
export {cast} from './cast.js';
export const stories=structuredClone(baselines);
for(const [i,s]of stories.entries())s.subtitle=`第三版 / 第 ${i+1} 幕`;
stories[0].beats[0].commands=stories[0].beats[0].commands.filter(c=>c.kind!=='bell');
const rain=stories[0];
rain.beats[0].commands.find(c=>c.type==='enter').x=-120;
rain.beats[0].commands.splice(2,0,{type:'move',actor:'夜莺',x:640,duration:1400});
rain.beats[2].commands.find(c=>c.type==='move').x=700;
rain.beats[3].commands.find(c=>c.type==='move').x=1020;
rain.note='只留下门、门铃和雨。第二次开口时，她才向门再靠近一点。';
const pack=stories[1];
pack.beats[1].commands=pack.beats[1].commands.filter(c=>c.type!=='move');
pack.beats[3].commands.find(c=>c.type==='move').x=950;
pack.beats[1].commands.splice(pack.beats[1].commands.findIndex(c=>c.type==='wait'),0,{type:'inspect',id:'烟盒',show:true,duration:900});
pack.beats[2].commands.splice(1,0,{type:'inspect',id:'烟盒',show:false,duration:750});
pack.note='烟盒从手边移到中央，再回到手边。放大只为认出证物，停顿留给人物。';
const phone=stories[4];
phone.beats[0].commands.find(c=>c.type==='move').x=540;
phone.beats[1].commands.unshift({type:'move',actor:'经理',x:590,duration:450},{type:'tilt',actor:'尼尔',angle:5,duration:350},{type:'wait',duration:220});
phone.beats[2].commands.splice(2,0,{type:'tilt',actor:'尼尔',angle:0,duration:350});
phone.note='先靠近，再进一步；尼尔脚下不动，身体微微后倾。搭肩保持为一张完整的关系姿势。';
const fired=stories[5];
fired.beats[0].commands.find(c=>c.type==='scene').scene='theatre';
fired.beats.splice(3,1,
 {label:'交出线索',commands:[{type:'silence'},{type:'wait',duration:1400},{type:'prop',id:'材料',kind:'letter',x:480,y:397},{type:'propMove',id:'材料',x:720,y:400,duration:900},{type:'wait',duration:650},{type:'propRemove',id:'材料'},{type:'wait',duration:900}]},
 {label:'缓慢离场',commands:[{type:'facing',actor:'尼尔',direction:'left'},{type:'move',actor:'尼尔',x:265,duration:2200},{type:'door',action:'open',duration:650},{type:'move',actor:'尼尔',x:120,duration:1700},{type:'exit',actor:'尼尔',duration:450}]},
 {label:'门合上后',commands:[{type:'wait',duration:400},{type:'door',action:'slam',duration:420},{type:'wait',duration:1800}]}
);
fired.note='交出材料后停一拍，缓慢走出去。人已经离场，门才猛地合上；震动过后，房间再静一会儿。';
const trial=structuredClone(originals.find(s=>s.id==='effects'));
trial.title='舞台动作试台';trial.subtitle='第三版 / 单独试演';
trial.beats.splice(4,0,{label:'身体退让',commands:[{type:'tilt',actor:'尼尔',angle:5,duration:500},{type:'wait',duration:500},{type:'tilt',actor:'尼尔',angle:0,duration:500},{type:'say',actor:'尼尔',text:'脚下不动，身体退开一点。'}]}, {label:'证物移到中央',commands:[{type:'prop',id:'试台烟盒',kind:'pack',x:555,y:375},{type:'inspect',id:'试台烟盒',show:true,duration:900},{type:'say',actor:'尼尔',text:'看清道具，再把注意力还给人物。'},{type:'inspect',id:'试台烟盒',show:false,duration:750},{type:'propRemove',id:'试台烟盒'}]});
trial.note='移动、换姿势、提亮、后倾、道具查看、震动和闪光分别试演；不把所有动作塞进每句对白。';stories.push(trial);

const introductions=[
 ['雨夜求助','夜晚，公寓楼下，一位陌生来客'],
 ['看见烟盒','追丢了收款人，尼尔带回一件东西'],
 ['未说出的名字','离开老街后，两人终于单独谈话'],
 ['一份赔钱的委托','尼尔的房间，求助的谈话接近结束'],
 ['经理安装电话','尼尔回家，有人在替他安排事情'],
 ['经理解雇尼尔','剧院，经理找尼尔谈调查进展'],
 ['舞台动作试台','逐项观察动作、道具与光线']
];
const settings=['rain','street','street','room','room','theatre','room'];
for(const [i,story]of stories.entries()){
 const commands=story.beats[0].commands;
 commands.find(c=>c.type==='scene').scene=settings[i];
 commands.splice(1,0,{type:'atmosphere',...structuredClone(atmospheres[settings[i]])});
 const index=commands.findIndex(c=>['say','wait','move'].includes(c.type));
 commands.splice(index<0?commands.length:index,0,{type:'title',kicker:i===6?'附场':`片段${'一二三四五六'[i]}`,title:introductions[i][0],description:introductions[i][1],fadeIn:650,hold:1400,fadeOut:950,settle:350});
}
// 经理话语仍然温和，尼尔所在的光池逐步收弱。
stories[5].beats[1].commands.unshift({type:'pool',id:'尼尔',strength:.065,duration:1400});
stories[5].beats[2].commands.unshift({type:'pool',id:'尼尔',strength:.035,duration:1300});
stories[5].beats[5].commands.push({type:'pool',id:'经理',strength:.13,duration:1000},{type:'wait',duration:900});

enrichScenes(stories);
