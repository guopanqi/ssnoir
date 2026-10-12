// Theatre direction is data, independent of geometry and input handling.
// All positions use the same fixed 16:9 orthographic stage.
const C=(v,a=0,b=1)=>Math.max(a,Math.min(b,v));
const S=(a,b,t)=>{const u=C((t-a)/(b-a));return u*u*(3-2*u)};
const lerp=(a,b,t)=>a+(b-a)*t;
const D=(x=0,y=-2.50,opts={})=>({x,y,scale:1,tilt:0,source:'尼尔',pose:{},visible:true,flip:false,...opts});
const sin=Math.sin;
export const SCENES={
  drunk:{title:'酒馆外 · 醉步',duration:21,
    detail:'同一套尼尔描线；比较静态姿势与自动权重关节驱动。'},
  chase:{title:'街角 · 追逐',duration:26,
    detail:'尼尔与码头工人的正式描线；奔跑、冲撞、刹车和纵深转角。'}
};
function getDrunk(time) {
  const t=C(time,0,21);
  const exit=S(.6,3,t);
  const walk=S(3,8,t),walk2=S(12.5,19.8,t);
  const x=lerp(-2.65,-.48,walk)+3.9*walk2;
  const stumble=Math.sin((t-2.5)*5.8);
  let source='尼尔_醉站',pose={},tilt=0;
  if(t>=3.0&&t<8.0 || t>=12.5) {
    source='尼尔_醉行';
    pose={torso:.10+.12*Math.sin(t*2.6),head:-.16,armL:-.26-.28*stumble,armR:.16+.27*stumble,
      legL:.23*stumble,legR:-.23*stumble,kneeL:Math.max(0,stumble)*.18,kneeR:Math.max(0,-stumble)*.18};
    tilt=.052*Math.sin(t*2.15)+(t>14&&t<16?.14*Math.sin(t*5):0);
  } else if(t>=8&&t<12.5){
    source='尼尔_扶墙';
    pose={torso:.12,head:.15,armL:-.20,legR:.11};
    tilt=.09;
  } else {
    pose={torso:.11*Math.sin(t*1.3),head:.13,armL:.12,armR:-.09};
    tilt=.045*Math.sin(t*1.7);
  }
  const visible=t>=.65&&t<=20.5;
  const beat=t<.65?'酒馆门口的灯亮起来'
    :t<3?'他醉醺醺地走出门'
    :t<8?'脚步不稳，身体不断纠正重心'
    :t<12.5?'扶住墙，勉强站稳'
    :t<16?'继续走；重心险些失控'
    :t<20.5?'终于消失在街道另一头':'空荡的酒馆门口';
  return {
    beat,door:exit,scroll:0,alley:0,paper:0,mail:0,crowd:0,
    neil:D(x,-2.50,{visible,source,pose,tilt,
      bob:(t>=3&&t<8||t>=12.5)?Math.abs(Math.sin(t*5.7))*.055:0}),
    worker:D(15,-2.50,{source:'码头工人',visible:false}),
    focus:x*.12,gateLight:1-exit*.55,streak:0
  };
}
function getChase(time){
  const t=C(time,0,26);
  const running=S(4.5,7,t);
  const scroll=S(6.2,9.5,t)*(1-S(17,20,t));
  const escape=S(18,20.3,t);
  const neilTurn=S(22.1,24.5,t);
  const gap=lerp(3.45,2.55,S(9,15,t));
  const stride=sin((t-5.2)*11.8);
  const workerX=lerp(-2.05,-1.35,S(5,8,t));
  let neil=D(4.8,-2.5,{flip:true,source:'尼尔',pose:{}});
  let worker=D(-2.0,-2.5,{flip:true,source:'码头工人',pose:{},visible:t>=1});
  if(t<3.5){
    neil=D(4.2,-2.5,{source:'尼尔',flip:true,pose:{torso:-.07,head:.07}});
    worker=D(lerp(-3.1,-2.15,S(1,3,t)),-2.5,{
      source:'码头工人',flip:true,visible:t>=1,pose:{armL:-.35,torso:.04}});
  }else if(t<5.2){
    neil=D(4.2,-2.5,{source:'尼尔',flip:true,pose:{torso:.24,armR:-.7,head:-.16}});
    worker=D(-2.05,-2.5,{source:'码头工人',flip:true,pose:{head:-.35,torso:.10}});
  }else{
    neil=D(lerp(4.2,workerX+gap,S(5.3,9,t)),-2.50,{source:t<6.3?'尼尔':'尼尔_逼近',
      flip:true,tilt:.16+.05*sin(t*3.0),pose:{torso:.27,head:-.11,
       armL:-.85*stride,armR:.85*stride,elbowL:.13,elbowR:-.13,
       legL:.66*stride,legR:-.66*stride,kneeL:.44*Math.max(0,-stride),kneeR:.44*Math.max(0,stride)},
       bob:Math.abs(stride)*.07});
    worker=D(workerX,-2.50,{source:t<5.8?'码头工人':'码头工人_奔跑',
      flip:true,tilt:.12+.05*sin(t*3.0),pose:{torso:.18,head:-.1,
        armL:.67*stride,armR:-.67*stride,
        legL:-.42*stride,legR:.42*stride,kneeL:.3*Math.max(0,stride),kneeR:.3*Math.max(0,-stride)},
        bob:Math.abs(stride)*.08});
    // Shoulder against the crowd, then brake before the alley.
    if(t>=11&&t<12.2)neil.pose={...neil.pose,torso:.33,armL:-1.15,head:-.20};
    if(t>=17.8){
      worker.x=lerp(worker.x,-.2,escape);
      worker.y=lerp(-2.5,-.7,escape);
      worker.scale=lerp(1,.35,escape);
      worker.pose={torso:.07,armL:-.3,legL:.14,legR:-.12};
      worker.visible=t<20.65;
    }
    if(t>=18.5) {
      neil.x=lerp(neil.x,2.2,S(18.5,20,t));
      neil.source='尼尔';
      neil.tilt=lerp(.18,0,S(18.5,20.5,t));
      neil.pose={torso:lerp(.24,-.13,S(19,20,t)),head:.15,armL:.17,armR:-.16,legL:-.17,legR:.21};
      neil.bob=0;
    }
    if(t>=22){
      neil.x=lerp(2.2,-.2,neilTurn);
      neil.y=lerp(-2.50,-.7,neilTurn);
      neil.scale=lerp(1,.35,neilTurn);
      neil.pose={torso:.08,armL:-.22,armR:.22,legL:.28*sin(t*8),legR:-.28*sin(t*8)};
      neil.visible=t<24.8;
    }
  }
  const beat=t<1?'邮筒旁，一个人影走近'
    :t<3.5?'那个人拿走了信封'
    :t<5.2?'尼尔放下报纸，察觉异样'
    :t<8?'两个人猛地冲入街道'
    :t<12.3?'拥挤的人群中，尼尔撞开一条路'
    :t<17.8?'追逐越来越快；背景化成掠过的黑影'
    :t<20.7?'对方拐入巷子，消失在深处'
    :t<22.2?'尼尔刹住脚步，喘了口气'
    :t<24.8?'他也追进那条巷子':'街道恢复平静';
  return {beat,neil,worker,door:0,mail:1,
    paper:1-S(3.45,4.05,t),scroll,gateLight:0,
    crowd:scroll*(.65+.35*Math.abs(Math.sin(t*1.25))),alley:S(16.2,18.5,t),
    focus:0,streak:scroll,
    impact:t>11.3&&t<11.55||t>14.1&&t<14.35
  };
}
export function sampleScene(name,time) {
  if(!SCENES[name])throw new Error('Unknown scene '+name);
  return name==='drunk'?getDrunk(time):getChase(time);
}
