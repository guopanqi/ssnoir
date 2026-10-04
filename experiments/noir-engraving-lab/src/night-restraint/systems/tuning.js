export function mountTuning(P,apply){
 const initial={...P},inputs=new Map();
 const panel=document.createElement('details');panel.className='tuning';
 const summary=document.createElement('summary');summary.textContent='画面调节';panel.append(summary);
 const contents=document.createElement('div');contents.className='tuning-body';panel.append(contents);
 const intro=document.createElement('p');intro.textContent='边看边调；刷新恢复默认。';contents.append(intro);
 const fields=[
 ['城市活动',[['cars','车辆数量',0,80,1],['carSpeed','车流速度',0,2,.05],['trailLength','车流尾迹长度',0,30,1],['trailGain','尾迹亮度',0,1.5,.05],['tailRed','尾灯红度',0,1,.01],['boats','船只数量',0,6,1],['boatSpeed','船只速度',0,2,.05],['wakeGain','船尾波光',0,1,.05]]],
 ['灯光与空气',[['lampGlow','路灯光晕',0,1.5,.05],['lampPool','地面光池',0,1.5,.05],['sodiumPoints','暖光照亮建筑',0,1.5,.05],['bloom','地标辉光',0,.8,.01],['windowGain','窗光亮度',0,1.8,.05],['mist','流动雾',0,1,.05]]],
 ['胶片与色彩',[['grain','胶片颗粒',0,.08,.002],['saturation','色彩饱和度',.3,1.2,.02],['exposure','曝光',.7,1.6,.01]]]
 ];
 for(const [title,items]of fields){const section=document.createElement('fieldset');const legend=document.createElement('legend');legend.textContent=title;section.append(legend);contents.append(section);
 for(const [key,text,min,max,step]of items){
 const label=document.createElement('label');const span=document.createElement('span');span.textContent=text;
 const output=document.createElement('output');const input=document.createElement('input');input.type='range';input.min=min;input.max=max;input.step=step;input.setAttribute('aria-label',text);
 const refresh=()=>{input.value=P[key];output.textContent=Number(P[key]).toFixed(step>=1?0:String(step).split('.')[1].length);};refresh();inputs.set(key,refresh);
 input.addEventListener('input',()=>{P[key]=Number(input.value);refresh();apply();});label.append(span,output,input);section.append(label);
 }}
 const buttons=document.createElement('div');buttons.className='tuning-presets';contents.append(buttons);
 for(const [name,values]of [['推荐',initial],['更热闹',{...initial,cars:65,boats:6,trailGain:.9,trailLength:18}],['原版颗粒',{...initial,grain:.05}],['安静对照',{...initial,cars:0,boats:0,grain:0}]]){
 const button=document.createElement('button');button.textContent=name;button.addEventListener('click',()=>{Object.assign(P,values);for(const refresh of inputs.values())refresh();apply();});buttons.append(button);
 }
 document.body.append(panel);
}
