const say = (actor, text) => ({ type: 'say', actor, text });
const setup = () => [{ type: 'scene', scene: 'room' }, { type: 'enter', actor: '尼尔', x: 420 }, { type: 'enter', actor: '夜莺', x: 850 }];
function cigarette(acted) { return [
  { label: '从口袋拿出的东西', commands: [...setup(), say('尼尔', '我没追上。但拿到了这个。')] },
  { label: '烟盒', commands: [{ type: 'prop', id: '烟盒', kind: 'cigarettes', x: 555, y: 375 }, say('尼尔', '追的时候，从他身上掉下来的。')] },
  { label: '认出了吗', commands: [...(acted ? [{ type: 'silence' }, { type: 'wait', duration: 850 }, { type: 'pose', actor: '夜莺', pose: '低头' }, { type: 'wait', duration: 350 }] : []), say('夜莺', '……他的？')] },
  { label: '一个问题', commands: [say('尼尔', '你见过这种烟？')] },
  { label: '回答', commands: [say('夜莺', '很多人都抽。')] },
  { label: '话题转开', commands: [...(acted ? [{ type: 'pose', actor: '夜莺', pose: '基础' }] : []), say('夜莺', '你人没事就好。')] },
  { label: '留下的问题', commands: [{ type: 'silence' }, { type: 'wait', duration: 900 }] },
]; }
function distance(retreat) { return [
  { label: '追问', commands: [...setup(), say('尼尔', '收到第一封信的时候，你就想过是莱恩？')] },
  { label: '承认', commands: [say('夜莺', '我不能确定。')] },
  { label: '他问过的事', commands: [say('尼尔', '我没问你确定了谁。')] },
  { label: '距离', commands: [{ type: 'silence' }, ...(retreat ? [{ type: 'move', actor: '夜莺', x: 955, duration: 600 }] : [{ type: 'wait', duration: 600 }]), say('夜莺', '我怕你知道以后，就不肯管了。')] },
  { label: '回答', commands: [say('尼尔', '也许不会。我不知道。')] },
  { label: '责任', commands: [say('尼尔', '可昨天去问那些人的是我。')] },
  { label: '没有追近', commands: [say('夜莺', '……对不起。')] },
  { label: '继续工作', commands: [say('尼尔', '把地址写下来。')] },
  { label: '停住', commands: [{ type: 'silence' }, { type: 'wait', duration: 1000 }] },
]; }
function manager(entrance) { return [
  { label: '冷场', commands: [...setup(), ...(!entrance ? [{ type: 'enter', actor: '经理', x: 650, layer: 'front' }] : []), say('尼尔', '你还有什么没跟我说？')] },
  { label: '话没说完', commands: [say('夜莺', '我……')] },
  { label: '经理来了', commands: [{ type: 'silence' }, ...(entrance ? [{ type: 'enter', actor: '经理', x: 1350, layer: 'front' }, { type: 'move', actor: '经理', x: 650, duration: 950 }] : [{ type: 'wait', duration: 950 }]), say('经理', '尼尔！可算找到你了。')] },
  { label: '直接安排', commands: [say('经理', '吃了吗？走，我正好要去酒店。')] },
  { label: '试图回应', commands: [say('尼尔', '我们还有点事。')] },
  { label: '他的逻辑', commands: [say('经理', '好极了，路上说。车就在楼下。')] },
  { label: '被改变的节奏', commands: [...(entrance ? [{ type: 'parallel', commands: [{ type: 'move', actor: '经理', x: 480, duration: 650 }, { type: 'move', actor: '尼尔', x: 350, duration: 650 }] }] : [{ type: 'wait', duration: 650 }]), say('经理', '夜莺，你先排练。我把他借走一会儿。')] },
  { label: '来不及答应', commands: [say('尼尔', '我还没答应。')] },
  { label: '笑点', commands: [say('经理', '上车再答应也来得及。')] },
  { label: '她留在原地', commands: [{ type: 'silence' }, { type: 'wait', duration: 850 }] },
]; }
export const studies = [
 {id:'cigarette',title:'那包烟',subtitle:'实验 01 · 没说出口的事',note:'对照：台词与其他条件相同，B 只增加一次停顿与低头。',questions:['她看见烟盒时，发生了什么变化？','哪一拍让你产生这个判断？','有没有动作让你分心？'],variants:[{label:'A',description:'只有对白',beats:cigarette(false)},{label:'B',description:'停顿与低头',beats:cigarette(true)}]},
 {id:'distance',title:'把地址写下来',subtitle:'实验 02 · 人物之间的距离',note:'对照：同样的对白，B 只让夜莺退开半步；A 保留等长停顿。',questions:['两人的关系有没有变化？','你从哪里读到这种变化？','退开像情绪，还是像机械滑动？'],variants:[{label:'A',description:'保持距离',beats:distance(false)},{label:'B',description:'退开半步',beats:distance(true)}]},
 {id:'manager',title:'上车再答应',subtitle:'实验 03 · 冷场被打断',note:'对照：A 三人从头在场；B 经理中途入场、占据两人之间，再带动尼尔移动。没有伪造拍肩动作。',questions:['你觉得经理是怎样的人？','他有没有改变整场戏的节奏？','三个人的站位是否清楚？'],variants:[{label:'A',description:'静态三人对白',beats:manager(false)},{label:'B',description:'入场与带动',beats:manager(true)}]},
];

function farewell(delayed) { return [
 {label:'准备离开',commands:[...setup(),say('夜莺','谢谢你，尼尔先生。')]},
 {label:'走向门口',commands:[{type:'silence'},{type:'facing',actor:'夜莺',direction:'right'},{type:'move',actor:'夜莺',x:1000,duration:900},say('尼尔','演出还有多久？')]},
 {label:'停住',commands:[{type:'silence'},{type:'wait',duration:delayed?1000:150},{type:'facing',actor:'夜莺',direction:'left'},{type:'move',actor:'夜莺',x:900,duration:550},say('夜莺','就这两周了。')]},
 {label:'他的决定',commands:[say('尼尔','先想办法凑够钱。我们查清楚是谁。')]},
 {label:'没有走出去',commands:[{type:'silence'},{type:'wait',duration:850}]},
]; }
studies.push(
 {id:'farewell',title:'门前的一句话',subtitle:'实验 04 · 动作被语言打断',note:'两个版本只改变叫住后转回来的等待时长；返回的距离与对白相同。',questions:['她停下，是意外、犹豫还是松了口气？','哪一个时刻让你读到了变化？','等待是否过长？'],variants:[{label:'A',description:'立即转回',beats:farewell(false)},{label:'B',description:'停住后转回',beats:farewell(true)}]},
 {id:'blocked',title:'门还开着',subtitle:'实验 05 · 多人与权力',note:'守门人只用剪影。没有拳头或震动，用堵住出口检验威胁是否成立。',questions:['谁掌握了这个房间？','你什么时候意识到尼尔不能离开？','守门人的位置能否读清？'],beats:[
  {label:'出口',commands:[...setup(),{type:'enter',actor:'守门人',x:1260,layer:'front'},say('尼尔','那我改天再来。')]},
  {label:'有人挡住了门',commands:[{type:'silence'},{type:'move',actor:'守门人',x:1100,duration:800},{type:'move',actor:'尼尔',x:630,duration:650}]},
  {label:'房间深处',commands:[{type:'say',actor:'弗兰克',text:'你刚来，急什么？',remote:true,source:'房间深处'}]},
  {label:'没有人碰他',commands:[say('尼尔','我还有事。')]},
  {label:'门并没有关',commands:[{type:'say',actor:'弗兰克',text:'我们也是。',remote:true,source:'房间深处'}]},
  {label:'留在原地',commands:[{type:'silence'},{type:'wait',duration:1300}]},
 ]},
 {id:'letter',title:'那封信先留下',subtitle:'实验 06 · 道具与注意力',note:'故意不配伸手图，检验信件移动能否讲清事情，以及哪里显得像悬浮。',questions:['谁拿走了信，谁不愿让他拿走？','道具与人物动作的先后是否清楚？','缺少伸手姿势是否破坏可信度？'],beats:[
  {label:'桌上的信',commands:[...setup(),{type:'prop',id:'信',kind:'letter',x:600,y:375},say('尼尔','这封信我要带走。')]},
  {label:'取走',commands:[{type:'silence'},{type:'propMove',id:'信',x:490,y:365,duration:500}]},
  {label:'慢了一拍',commands:[{type:'wait',duration:450},{type:'move',actor:'夜莺',x:755,duration:450},say('夜莺','等等。')]},
  {label:'停在手边',commands:[say('尼尔','怎么了？')]},
  {label:'她改口',commands:[{type:'pose',actor:'夜莺',pose:'低头'},say('夜莺','……别弄丢了。')]},
  {label:'收进外套',commands:[{type:'silence'},{type:'propRemove',id:'信'},{type:'wait',duration:900}]},
 ]},
 {id:'blackout',title:'灯灭的那一刻',subtitle:'实验 07 · 画外事件',note:'建议开启声音。只用黑场、脚步、撞击与重新亮起后的变化；不把站姿旋转伪装成倒地。',questions:['黑暗里你觉得发生了什么？','声音与恢复灯光后的画面是否一致？','没有倒地画面，是留白还是信息不足？'],beats:[
  {label:'门外有人',commands:[...setup(),{type:'enter',actor:'守门人',x:1100,layer:'front'},say('尼尔','谁在门外？')]},
  {label:'突然断电',commands:[{type:'silence'},{type:'effect',effect:'blackout'},{type:'sound',sound:'steps'},{type:'wait',duration:950}]},
  {label:'一声撞击',commands:[{type:'sound',sound:'impact'},{type:'wait',duration:600},{type:'exit',actor:'守门人',duration:1},{type:'move',actor:'尼尔',x:720,duration:1},{type:'wait',duration:700}]},
  {label:'重新亮起',commands:[{type:'effect',effect:'restore'},say('夜莺','他倒在门外了。')]},
  {label:'仍然看不见的人',commands:[say('尼尔','别过来。')]},
  {label:'余波',commands:[{type:'silence'},{type:'wait',duration:1000}]},
 ]},
);
