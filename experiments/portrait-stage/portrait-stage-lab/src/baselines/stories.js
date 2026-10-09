const scene = name => ({type:'scene',scene:name});
const enter = (actor,x) => ({type:'enter',actor,x});
const say = (actor,text,remote=false) => ({type:'say',actor,text,remote});
const move = (actor,x,duration=700) => ({type:'move',actor,x,duration});
const wait = (duration=950) => ({type:'wait',duration});
const silence = {type:'silence'};
const pose = (actor,pose) => ({type:'pose',actor,pose});
const facing = (actor,direction) => ({type:'facing',actor,direction});
const prop = (id,kind,x,y) => ({type:'prop',id,kind,x,y});
const remove = id => ({type:'propRemove',id});
const exit = actor => ({type:'exit',actor,duration:450});
const effect = effect => ({type:'effect',effect});
const beat = (label,...commands) => ({label,commands});
export const stories = [
 {id:'rain',title:'雨夜的求助',note:'只有一只电铃。门内的人始终不出现；她第二次开口时，才走近一点。',beats:[
 beat('来访',scene('rain'),prop('电铃','bell',945,320),enter('夜莺',410),wait(500),effect('bell'),say('夜莺','尼尔先生？'),say('尼尔','是，你是？',true)),
 beat('请求',say('夜莺','我听朋友说起过你，我遇到了一些麻烦，我想请你帮忙。'),say('尼尔','现在很晚了，而且……',true)),
 beat('再争取一次',silence,move('夜莺',465,550),say('夜莺','就几分钟，先生，而且这事有点急，我怕明天就太晚了。'),silence,wait(1400),pose('夜莺','低头'),say('夜莺','可以吗？')),
 beat('允许进入',silence,wait(900),effect('unlock'),pose('夜莺','基础'),wait(450),move('夜莺',865,1600),exit('夜莺'))]},
 {id:'pack',title:'那包金牌香烟',note:'同一件道具，两个人看到不同的东西。她不解释那一眼，随后拒绝回到老街。',beats:[
 beat('回来',scene('quiet'),enter('尼尔',440),enter('夜莺',845),facing('夜莺','left'),say('尼尔','我没追到他，该死，在路口的时候突然窜出一辆摩托，我把它跟丢了。'),say('夜莺','你人没事儿就好。')),
 beat('拿出烟盒',say('尼尔','我拿到了这个。'),prop('烟盒','pack',515,392),silence,move('夜莺',735,650),wait(850),pose('夜莺','低头'),wait(650),say('夜莺','这是……他的？'),say('尼尔','追的时候从他身上掉下来的，“金牌”香烟。')),
 beat('把话题交还',say('尼尔','烟盒经常塞在口袋里，都被挤变形了，一定是经常进行大幅度的活动。'),pose('夜莺','基础'),say('夜莺','然后呢？大侦探？还有什么？'),say('尼尔','没了，我没什么能想到的。他从街道的桥上跳下去，应该对这一带很熟，我准备去问问。')),
 beat('老街',say('夜莺','老街？'),say('尼尔','对。'),silence,move('夜莺',855,700),say('夜莺','我可不回去。'),say('尼尔','我没说要带上你……你不喜欢那里？'),say('夜莺','我花了很久才从里面走出来，我不回去。'),silence,wait(1300))]},
 {id:'truth',title:'你为什么没有告诉我',note:'眼泪没有消除质问。她先承担带路的行动，他隔了一拍才跟上。',beats:[
 beat('质问',scene('quiet'),prop('椅子','chair',600,484),enter('尼尔',410),enter('夜莺',835),facing('夜莺','left'),say('尼尔','现在可以说了？'),pose('夜莺','低头'),say('夜莺','莱恩是我……之前的男朋友。'),say('尼尔','继续。')),
 beat('没有被安慰',say('夜莺','他不会做出这样的事情来，他，他怎么会这样？'),say('尼尔','第一次的时候我就问过你，你为什么没有告诉我？'),say('夜莺','我不能确定，而且我觉得你会不愿意管，一个没钱的女孩，一个以前的男人，如果我真的告诉你，你还会帮我吗？'),silence,wait(1600),say('尼尔','也许会，也许不会，我不知道。')),
 beat('补上事实',say('夜莺','分手之后，他找过我，我总是甩掉他。后来我离开老街之后，他到剧院找我，但都被保安拦住了，我们没有再见过面了。'),say('尼尔','你还有什么是瞒着我的？'),say('夜莺','没有了，尼尔，我知道的就这些了，真的。'),silence,wait(1400)),
 beat('先去带路',pose('夜莺','基础'),say('夜莺','我知道他住的地方，我们去看看吧。'),silence,facing('夜莺','right'),move('夜莺',1080,1000),wait(950),move('尼尔',705,1100),exit('夜莺'),move('尼尔',1080,1000),exit('尼尔'))]},
 {id:'fee',title:'门前的一句话',note:'她以为谈话结束了。答应帮忙发生在她已经开始离开之后。',beats:[
 beat('报酬',scene('quiet'),prop('桌子','table',360,491),prop('伞','umbrella',975,465),enter('尼尔',430),enter('夜莺',800),facing('夜莺','left'),say('尼尔','那我的报酬呢？你准备怎么付我的报酬？'),say('夜莺','演出之后可以吗？经理说过些天会有一场大型的演出，应该会有些钱。虽然之前很多次小一些的演出，他也没给过我什么钱。')),
 beat('没有把握的许诺',say('夜莺','当时他说会有些钱的，但是也没给。'),say('夜莺','这次机会很重要，我不愿意错过。'),silence,wait(1600),pose('夜莺','低头'),say('夜莺','我明白，确实太麻烦你了。')),
 beat('准备离开',silence,facing('夜莺','right'),move('夜莺',955,850),remove('伞'),say('夜莺','谢谢你，尼尔先生。'),silence,wait(1000),say('尼尔','演出还有多久？')),
 beat('门口留下',facing('夜莺','left'),pose('夜莺','基础'),say('夜莺','就这两周了。'),say('尼尔','我们得先想办法凑够赎金，只要他来拿钱，我有办法弄明白他是谁。'),silence,wait(700),pose('夜莺','低头'),wait(1700))]},
 {id:'phone',title:'大侦探得有个电话',note:'他送来便利，也越过了主人的边界。搭肩是一种单方面的亲近。',beats:[
 beat('不请自来的安排',scene('quiet'),prop('桌子','table',920,491),prop('电话','phone',925,386),enter('尼尔',700),enter('经理',265),say('经理','尼尔！'),silence,move('经理',575,900)),
 beat('搭上肩膀',{type:'light',actor:'经理',value:0},{type:'light',actor:'尼尔',value:0},enter('搭肩',640),say('尼尔','这是干嘛？'),say('经理','电话！以后出了事情，还让人通过门房留言吗？'),say('经理','大侦探得有个电话。'),say('尼尔','我还……'),say('经理','早晚的事。')),
 beat('替他做主',silence,exit('搭肩'),{type:'light',actor:'经理',value:1},{type:'light',actor:'尼尔',value:1},move('经理',790,750),say('经理','给他装好的！最好的线！'),silence,wait(1500))]},
 {id:'fired',title:'你可以先放一放了',note:'礼貌的措辞逐步夺走位置。尼尔没有反驳，最后才让门响起来。',beats:[
 beat('谈工作',scene('quiet'),prop('椅子','chair',850,484),enter('经理',810),enter('尼尔',425),facing('经理','left'),say('经理','尼尔，我们必须得好好谈谈了。'),silence,wait(950),say('经理','到目前为止已经有几天了，事情有什么重要的进展吗？'),silence,wait(1400)),
 beat('说得很合理',say('经理','我只针对事情，不针对人。这是一份工作，我们每个人都在自己的岗位上，为这件事出一份力。'),say('经理','就拿我来说，我每天都在为赞助和演出奔走，如果我没能做得很好，我自己就不配呆在这个岗位上，我就要开除我自己。'),silence,wait(1000)),
 beat('留一个台阶',say('经理','如果我们不能为这件事情的解决作出贡献，那我们在这儿不是浪费时间吗？'),silence,wait(1200),say('经理','尼尔，我其实很不情愿，但是这件事情你可以先放一放了，我可能会找一个更老练的侦探。但也别那么绝情，别断了联系。')),
 beat('交出线索',silence,prop('材料','letter',480,397),{type:'propMove',id:'材料',x:720,y:400,duration:900},wait(550),remove('材料'),facing('尼尔','left'),move('尼尔',130,1250),exit('尼尔'),{type:'sound',sound:'impact'},wait(1600))]}
];
