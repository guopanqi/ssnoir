{n:'第二天',sub:'尼尔的房间 · 独自探索失败后',sh:'次日',pos:[1330,700],bg:'#0b0907',set:'#set3',i:['34%','62%'],mk:()=>SETS.room(),
ini(){P('nl',{d:0,show:1,x:1040,f:1,s:.56,face:'hard',h:6});P('ny',{d:0,x:-160,s:.56,face:'n'})},
async run(k,{w,go,fl,n,y,nar}){
await w(500);await fl();lamp(1);await w(500);
P('ny',{show:1,d:0});go('ny',560,2600);await w(2900);
await y('他们没告诉你怎么走？');P('nl',{h:0});await n('差不多');
await y('打听到什么消息了？');await n('毫无进展，他们好像早就知道我要问什么一样');
P('ny',{face:'smile'});await y('你穿成这样，一眼就看出你是外面的，没把你裤子扒了已经算客气的了。');
P('nl',{face:'wry',h:4});await w(1800);
P('ny',{face:'n'});await y('明天我带你进去');P('nl',{h:0,face:'n'});await n('你不是不想回去吗');
cc();P('ny',{face:'sad',h:8});await w(2600);P('ny',{h:0});
await y('我不想回去，特别不想。可是，什么时候也不能都只让你去。');
await w(2200)}},
