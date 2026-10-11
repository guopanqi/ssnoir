/* chapters */
const C1=Object.assign({t:'<h1>片段一 · 雨夜求助</h1><p>格兰德酒店门前</p>',o:'920px 610px',bg:'#070a12',set:'#hotel',i:['20%','52%'],
ini(){P('ny',{d:0,x:-200,s:.54,hat:1,umo:1})},
async run(k,{w,say,go}){
rainI=1;rainS(.55,3400);$('#fx').classList.add('rn');sp(1,860,.9,.16,'#8fb4e8');await w(1500);
P('ny',{show:1,d:0});go('ny',810,3400);await w(3600);P('ny',{ar:-90,h:-3});
await w(900);bell();op('#bt',1);rx('ul',false);await w(1300);P('ny',{ar:0});op('#bt',0);await w(700);
await say('ny','尼尔先生？');await w(400);
await say('nl','是，你是？',1);
P('ny',{face:'smile',h:0});await say('ny','我听朋友说起过你，我遇到了一些麻烦，我想请你帮忙。');
P('ny',{face:'sad'});await say('nl','现在很晚了，而且……',1);
P('ny',{face:'n',h:-2});await say('ny','就几分钟，先生，而且这事有点急，我怕明天就太晚了。');
cc();P('ny',{face:'sad',h:7});rainS(.8,3600);rx('q',true);$('#nw').classList.remove('lit');await w(3000);
P('ny',{h:0,face:'n'});rx('q',false);$('#nw').classList.add('lit');await say('ny','可以吗？');cc();await w(900);
buzz();await w(900);rx('ul',true);op('#dl',.95);$('#leaf').style.transform='scaleX(.12)';sp(2,800,.9,.26,'#f4cf80');P('ny',{face:'smile',h:-2});await w(1400);
P('ny',{f:1,h:0,face:'n'});await w(700);P('ny',{x:790,y:786,s:.46,show:0,d:2600,cl:{wk:1}});steps(2400);await w(2800);
$('#leaf').style.transform='scaleX(1)';op('#dl',0);rx('ul',false);sp(2,800,.9,0);sp(1,810,.7,0);await w(1600);
rainI=.3;rainS(.2);$('#fx').classList.remove('rn');await w(800)}},{n:'雨夜求助',sub:'夜晚 · 公寓楼下 · 下着雨',sh:'雨夜',pos:[900,600]});
const C7=Object.assign({t:'<h1>片段三 · 隐瞒莱恩后的争执</h1><p>老街之外 · 一盏路灯</p>',o:'430px 710px',bg:'#08090f',set:'#lamp',i:['34%','62%'],
ini(){P('nl',{d:0,x:-140,s:.56,face:'hard'});P('ny',{d:0,x:1740,s:.56,f:1,face:'sad',h:8})},
async run(k,{w,say,go,fl}){
await w(600);await fl();lamp(1);await w(900);
P('nl',{show:1,d:0});go('nl',470,2800);await w(1400);P('ny',{show:1,d:0});go('ny',1130,2800);await w(3000);
P('nl',{ar:55,al:-55});await w(800);
await say('nl','现在可以说了？');
P('ny',{h:10});await say('ny','莱恩是我……之前的男朋友。');
light();await say('nl','继续。');
P('ny',{face:'sad',h:5,ar:100});await say('ny','他不会做出这样的事情来，他，他怎么会这样？');P('ny',{ar:0});
P('nl',{ar:0,al:0,x:540,b:5,h:-2,face:'hard'});await say('nl','第一次的时候我就问过你，你为什么没有告诉我？');
P('ny',{h:-3,x:1100,ar:30,al:-30});await say('ny','我不能确定，而且我觉得你会不愿意管，一个没钱的女孩，一个以前的男人，如果我真的告诉你，你还会帮我吗？');
cc();P('ny',{h:3});rx('q',true);await w(700);await fl();lamp(.6);await w(1900);
P('nl',{face:'n',b:0});rx('q',false);lamp(1);await say('nl','也许会，也许不会，我不知道。');
P('ny',{face:'sad',h:8,look:6});await say('ny','分手之后，他找过我，我总是甩掉他。后来我离开老街之后，他到剧院找我，但都被保安拦住了，我们没有再见过面了。');
P('nl',{face:'hard',b:3,h:0});P('ny',{look:0,h:0});await say('nl','你还有什么是瞒着我的？');
P('ny',{x:1070,ar:-20,al:20,face:'sad'});await say('ny','没有了，尼尔，我知道的就这些了，真的。');
cc();P('nl',{ar:55,al:-55,b:-2});P('ny',{ar:0,al:0});rx('q',true);lamp(.7);await w(1400);await fl();lamp(.5);await w(1300);
rx('q',false);lamp(1);P('ny',{face:'n',h:0,f:0,d:500});await say('ny','我知道他住的地方，我们去看看吧。');
cc();go('ny',1850,5600);await w(2200);P('nl',{face:'n',h:5,b:2});await w(1600);
P('nl',{ar:0,al:0,b:0});go('nl',1850,5800);await w(1900);os('.lh',.95);sp(2,800,1.3,.34,'#f3d08a');await w(1000);os('.lh',.4);sp(2,800,1.1,.22,'#f3d08a');await w(3000);
await w(800);await fl();lamp(1);await w(2200);lamp(.6)}},{n:'隐瞒莱恩后的争执',sub:'离开老街之后 · 一盏路灯',sh:'路灯',pos:[430,720]});
