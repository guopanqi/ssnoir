const clamp=(x,a=0,b=1)=>Math.max(a,Math.min(b,x));
const smooth=(a,b,t)=>{const u=clamp((t-a)/(b-a));return u*u*(3-2*u)};
const mix=(a,b,t)=>a+(b-a)*t;
const wave=(v)=>Math.sin(v);
const easeEnd=t=>1-Math.pow(1-clamp(t),3);
export const CLIPS={
  drunk:{label:'01 · 醉步',duration:14,subtitle:'不稳定的重心 · 肢体调整与滞后',title:'THE TAVERN'},
  brace:{label:'02 · 扶墙',duration:11,subtitle:'手伸出去以后，身体才把重量交给墙',title:'THE BRICK WALL'},
  run:{label:'03 · 追逐',duration:12,subtitle:'两腿交替 · 步幅、摆臂、衣摆独立跟随',title:'THE CHASE'}
};
const zeros=()=>({head:0,torso:0,pelvis:0,armUpperL:0,armLowerL:0,armUpperR:0,armLowerR:0,
 thighL:0,shinL:0,thighR:0,shinR:0,coatL:0,coatR:0});
const O=()=>({});
export function sampleScene(scene,time){
  if(!CLIPS[scene])throw Error('Unknown clip '+scene);
  const t=clamp(time,0,CLIPS[scene].duration),angles=zeros(),offsets=O();
  let x=.35,y=0, tilt=0,bob=0,shadow=1,wall=.8,wind=0,beat='',speed=0;
  if(scene==='drunk'){
    const enter=smooth(.4,2,t),travel=smooth(2,6.3,t),travel2=smooth(10.4,13.3,t);
    x=mix(.25,.45,travel)+travel2*.27;
    speed=t<6.3?smooth(.9,2.4,t)*(1-smooth(6,7.2,t)):smooth(10,11,t)*(1-smooth(13.2,14,t));
    const sway=wave(t*2.18),step=wave(t*5.0),big=smooth(4.5,5.2,t)*(1-smooth(5.8,6.9,t));
    tilt=.048*sway*speed+.19*big*wave(t*3.5);
    angles.torso=-.11*sway*speed+.16*big;
    angles.head=-.12*sway*speed-.11*big;
    angles.armUpperL=-.24*speed-.30*step*speed-.26*big;
    angles.armUpperR=+.20*speed+.29*step*speed+.34*big;
    angles.armLowerL=+.16*speed;
    angles.armLowerR=-.10*speed;
    angles.thighL=.27*step*speed;
    angles.thighR=-.27*step*speed;
    angles.shinL=-.12*Math.max(0,step)*speed;
    angles.shinR=-.12*Math.max(0,-step)*speed;
    angles.coatL=.05*wave(t*2.18-.6)*speed;
    angles.coatR=-.07*wave(t*2.18-1)*speed;
    bob=(28-3*Math.abs(step))*speed;
    const br=smooth(6.7,7.6,t)*(1-smooth(9.7,10.5,t));
    angles.torso+=.20*br;angles.head-=.14*br;angles.armUpperR-=1.17*br;angles.armLowerR-=.25*br;
    angles.thighR+=.22*br;angles.shinR-=.18*br;
    x+=.035*br;
    wall=br;
    beat=t<2?'酒馆门灯亮起，尼尔走出来':t<4.6?'身体在脚步后面摇晃':t<6.7?'他几乎失去平衡':t<10.4?'伸手扶墙，停住':t<13.4?'离开墙壁，继续前行':'一个踉跄的背影渐远';
    shadow=enter;
  } else if(scene==='brace'){
    const reach=smooth(2.0,4.6,t),load=smooth(4,6.2,t),rel=smooth(8.3,10.2,t);
    const amount=reach*(1-rel);
    x=.40+.04*load-.015*rel;
    angles.torso=.20*load*(1-rel);
    angles.head=-.18*load*(1-rel)+.09*wave(t*1.3)*amount;
    angles.armUpperR=-1.19*amount;
    angles.armLowerR=-.25*amount;
    angles.armUpperL=.08*amount;
    angles.thighL=-.22*load*(1-rel);
    angles.shinL=.16*load*(1-rel);
    angles.thighR=.17*load*(1-rel);
    angles.coatL=-.10*load*(1-rel);
    angles.coatR=.06*load*(1-rel);
    bob=8*load*(1-rel);
    wall=1;
    shadow=.75;
    beat=t<2?'他勉强站稳':t<4.6?'右手伸向墙壁':t<6.2?'身体把重量压向支撑点':t<8.4?'停住，喘息':'撑开身体，放下手臂';
  } else {
    const go=smooth(.7,1.4,t)*(1-smooth(9.7,11.4,t));
    const stride=wave((t-1.1)*10.6),push=wave((t-1.1)*10.6-.3);
    x=.23+Math.min(1,smooth(1,5,t))*.32+smooth(8.4,10,t)*.14;
    tilt=.10*go+(.02*wave(t*8))*go;
    angles.torso=.11*go;
    angles.head=-.1*go;
    angles.armUpperL=(.05+.46*stride)*go;
    angles.armUpperR=(-.05+.46*stride)*go;
    angles.armLowerL=-.12*go+.12*stride*go;
    angles.armLowerR=-.10*go-.12*stride*go;
    angles.thighL=(.18+.19*stride)*go;
    angles.thighR=(-.18+.19*stride)*go;
    angles.shinL=-.18*Math.max(0,stride)*go;
    angles.shinR=.18*Math.max(0,-stride)*go;
    angles.coatL=-.16*go+.13*wave(t*10.6-.85)*go;
    angles.coatR=+.18*go-.10*wave(t*10.6-.5)*go;
    bob=-13*Math.abs(push)*go;
    wind=go;
    speed=go;
    wall=0;
    shadow=1;
    const stop=smooth(9.7,10.5,t);
    tilt-=.23*stop;
    angles.torso-=.26*stop;
    angles.armUpperL-=.27*stop;
    angles.armUpperR+=.34*stop;
    beat=t<1.4?'他发现拿走信封的背影':t<5.2?'冲出街角——腿、手臂和衣摆各自运动':t<9.7?'掠过人群，试图追上对方':t<11.1?'骤然刹停，鞋底擦地':'喘息后，他又朝巷子望去';
  }
  return {scene,t,x,y,tilt,bob,angles,offsets,shadow,wall,wind,beat,speed};
}
