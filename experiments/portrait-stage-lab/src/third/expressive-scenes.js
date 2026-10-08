// 把情绪转折落在明确拍点上，不按每句对白自动触发。
const wait=duration=>({type:'wait',duration});
const pool=(id,strength,duration)=>({type:'pool',id,strength,duration});
export function enrichScenes(stories){
 const [rain,pack,truth,fee,phone]=stories;
 const atmosphere=story=>story.beats[0].commands.find(c=>c.type==='atmosphere');
 atmosphere(rain).lights.push({id:'门缝',x:1010,y:395,width:430,height:550,color:'#e0ba7f',strength:0});
 const unlock=rain.beats[3].commands.findIndex(c=>c.type==='effect'&&c.effect==='unlock');
 rain.beats[3].commands.splice(unlock+1,0,pool('门缝',.23,950),wait(450));
 rain.note='门内迟迟不回答，她靠近一点又低下头。开锁后，门边的暖光才出现；她先抬头，停一拍再走进去。';

 const recognition=pack.beats[1].commands.findIndex(c=>c.type==='pose');
 pack.beats[1].commands.splice(recognition+1,0,pool('冷光',.055,650),wait(500));
 pack.beats[2].commands.splice(2,0,pool('冷光',.10,600));
 pack.note='烟盒放大时，她停住、低头，周围的冷光略微收弱。她主动转回话题，烟盒收回，灯光恢复；听到老街后才退开。';

 const opening=truth.beats[0].commands;
 opening.splice(opening.findIndex(c=>c.type==='prop'),1,{type:'prop',id:'路灯',kind:'streetlamp',x:630,y:347});
 atmosphere(truth).lights.push({id:'路灯',x:630,y:305,width:660,height:580,color:'#d2b17a',strength:.16});
 for(const [beat,duration,levels]of [[truth.beats[1],650,[1,.3,.85,.45,1]],[truth.beats[2],900,[1,.75,.12,.5,1]]]){
  const silent=beat.commands.findIndex(c=>c.type==='silence');
  beat.commands.splice(silent+1,0,{type:'flicker',id:'路灯',duration,levels});
 }
 truth.beats[3].commands.find(c=>c.type==='wait').duration=1500;
 truth.note='路灯立在两人之间。两次关键沉默里，灯短暂不稳，节奏各不相同。她先离开，他在灯下停一会儿才跟上。';

 // 叫住她时不立刻把气氛变好，直到尼尔明确接下委托才逐渐变暖。
 const answer=fee.beats[3].commands.findIndex(c=>c.type==='silence');
 fee.beats[3].commands.splice(answer+1,0,pool('室内暖光',.34,1500));
 fee.note='她背向尼尔准备离开；被叫住后才回身。直到他明确答应，室内暖光才缓慢变亮，她仍低头留在门口。';

 atmosphere(phone).lights.push({id:'电话',x:855,y:410,width:390,height:340,color:'#d6b17d',strength:.035});
 phone.beats[2].commands.find(c=>c.type==='move'&&c.actor==='经理').x=1070;
 phone.beats[2].commands.pop();
 phone.beats.push({label:'留下的电话',commands:[
  {type:'move',actor:'经理',x:1360,duration:1200},{type:'exit',actor:'经理',duration:180},
  {type:'propMove',id:'电话',x:855,y:386,duration:750},pool('电话',.18,850),wait(650),
  {type:'facing',actor:'尼尔',direction:'right'},wait(1700)
 ]});
 phone.note='经理贴近、搭肩，替尼尔把事情安排好。经理离开后，电话从桌边移向尼尔并留下暖光；尼尔没有跟着走。';
}
