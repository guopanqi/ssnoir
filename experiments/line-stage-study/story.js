// Timing and dialogue transcribed from Claude's lamp.run, including its waits.
const say=(label,speaker,text,acting={})=>({label,speaker,text,acting,duration:450+text.length*105});
const wait=(label,duration,acting={},flicker=false)=>({label,speaker:'',text:'',acting,duration,flicker});
const lampStory=[
 wait('灯还没有亮',600,{neilX:-9,nightX:109,lamp:0}),
 wait('灯管骤闪',660,{lamp:1},true),
 wait('灯光稳定',900,{lamp:1}),
 wait('尼尔走入灯下',1400,{neilX:29.375,neilMove:2800}),
 wait('夜莺走入灯下',3000,{nightX:70.625,nightMove:2800}),
 wait('尼尔停住',800),
 say('开口质问','尼尔','现在可以说了？'),
 say('承认关系','夜莺','莱恩是我……之前的男朋友。'),
 say('等她继续','尼尔','继续。'),
 say('无法相信','夜莺','他不会做出这样的事情来，他，他怎么会这样？'),
 say('尼尔走近','尼尔','第一次的时候我就问过你，你为什么没有告诉我？',{neilX:33.75,neilMove:700}),
 say('害怕被放弃','夜莺','我不能确定，而且我觉得你会不愿意管，一个没钱的女孩，一个以前的男人，如果我真的告诉你，你还会帮我吗？',{nightX:68.75,nightMove:700}),
 wait('无人接话',700,{quiet:true}),
 wait('沉默中的骤闪',660,{},true),
 wait('光线暗下来',1900,{lamp:.6}),
 say('尼尔没有安慰她','尼尔','也许会，也许不会，我不知道。',{quiet:false,lamp:1}),
 say('补上遗漏的事实','夜莺','分手之后，他找过我，我总是甩掉他。后来我离开老街之后，他到剧院找我，但都被保安拦住了，我们没有再见过面了。'),
 say('再次追问','尼尔','你还有什么是瞒着我的？'),
 say('最后的保证','夜莺','没有了，尼尔，我知道的就这些了，真的。',{nightX:66.875,nightMove:700}),
 wait('沉默留下距离',1400,{quiet:true,lamp:.7}),
 wait('又一次骤闪',660,{},true),
 wait('灯光变弱',1300,{lamp:.5}),
 say('她决定带路','夜莺','我知道他住的地方，我们去看看吧。',{quiet:false,lamp:1,nightFacing:'right'}),
 wait('夜莺先走',2200,{nightX:115.625,nightMove:5600}),
 wait('尼尔迟了一拍',1600),
 wait('尼尔跟上',1900,{neilX:115.625,neilMove:5800}),
 wait('灯光回响',1000,{halo:.95}),
 wait('两人走远',3000,{halo:.4}),
 wait('只剩下街道',800),
 wait('最后的骤闪',660,{},true),
 wait('只剩下灯光',2200,{lamp:1}),
 wait('灯渐暗',1000,{lamp:.6})
];
export let story=lampStory;
export let starts=story.map((_,i)=>story.slice(0,i).reduce((n,c)=>n+c.duration,0));
export function actingAt(index){const state={neilX:-9,nightX:109,neilPose:'neutral',nightPose:'neutral',nightFacing:'left',lamp:1,quiet:false,halo:.4,moveMs:0,neilVisible:true,rain:0,door:0};for(let i=0;i<=index;i++)Object.assign(state,story[i].acting);return state}
export function performanceAt(index,elapsed){
 const state=actingAt(index),position=starts[index]+elapsed;
 for(const actor of ['neil','night']){let from=actor==='neil'?-9:109,to=from,start=0,duration=0;
  for(let i=0;i<=index;i++)if(actor+'X' in story[i].acting){const a=story[i].acting;const fraction=duration?Math.min(1,(starts[i]-start)/duration):1;from=from+(to-from)*fraction;to=a[actor+'X'];start=starts[i];duration=a[actor+'Move']??0}
  const t=duration?Math.max(0,Math.min(1,(position-start)/duration)):1;state[actor+'X']=from+(to-from)*t;state[actor+'Walking']=duration>0&&t<1;
 }
 let fade=-1;for(let i=0;i<=index;i++)if(story[i].acting.nightFade)fade=i;state.nightOpacity=fade<0?1:Math.max(0,1-(position-starts[fade])/story[fade].acting.nightFade);
 if(story[index].flicker)state.lamp=[.15,1,.3,.9,.5,1][Math.min(5,Math.floor(elapsed/110))];
 return state;
}
export let soundEvents=story.flatMap((cue,i)=>[
 ...(cue.flicker?Array.from({length:6},(_,j)=>({time:starts[i]+j*110,type:'zap'})):[]),
 ...['neil','night'].flatMap(actor=>cue.acting[actor+'Move']>=1400?Array.from({length:Math.ceil(cue.acting[actor+'Move']/430)},(_,j)=>({time:starts[i]+j*430,type:'step'})):[])
]).sort((a,b)=>a.time-b.time);

const rainStory=[
 wait('门前雨声',1500,{neilX:-20,nightX:-15,neilVisible:false,rain:1,lamp:1,door:0}),
 wait('夜莺走到对讲机前',3600,{nightX:58,nightMove:3400}),
 wait('她伸手按铃',900),
 {...wait('门铃响起',1300),sound:'bell'},
 wait('等候回应',700),
 say('夜莺试探着开口','夜莺','尼尔先生？'),
 wait('对讲机的停顿',400),
 say('室内传来回答','尼尔','是，你是？'),
 say('说明来意','夜莺','我听朋友说起过你，我遇到了一些麻烦，我想请你帮忙。'),
 say('尼尔犹豫','尼尔','现在很晚了，而且……'),
 say('请求几分钟','夜莺','就几分钟，先生，而且这事有点急，我怕明天就太晚了。'),
 wait('雨声填满沉默',3000,{quiet:true,rain:1.45}),
 say('最后一次请求','夜莺','可以吗？',{quiet:false,rain:1}),
 wait('等待门锁',900),
 {...wait('门锁蜂鸣',900),sound:'buzz'},
 wait('门内暖光亮起',1400,{door:1}),
 wait('她转向门口',700,{nightFacing:'right'}),
 wait('夜莺走进门内',2800,{nightX:50,nightMove:2600,nightFade:2600}),
 wait('门重新合上',1600,{door:0}),
 wait('雨夜归于安静',800,{rain:.3})
];
export const stories={lamp:{title:'路灯下，未说出的名字',scene:'lamp',cues:lampStory},rain:{title:'雨夜求助',scene:'hotel',cues:rainStory}};
export function selectStory(id){
 const selected=stories[id];if(!selected)throw Error('Unknown story: '+id);
 story=selected.cues;starts=story.map((_,i)=>story.slice(0,i).reduce((n,c)=>n+c.duration,0));
 soundEvents=story.flatMap((c,i)=>[...(c.sound?[{time:starts[i],type:c.sound}]:[]),...(c.flicker?Array.from({length:6},(_,j)=>({time:starts[i]+j*110,type:'zap'})):[]),...['neil','night'].flatMap(a=>c.acting[a+'Move']>=1400?Array.from({length:Math.ceil(c.acting[a+'Move']/430)},(_,j)=>({time:starts[i]+j*430,type:'step'})):[])]).sort((a,b)=>a.time-b.time);
}
