// 幕布与光池是独立层；人物和道具不随幕布透明度一起变淡。
export const atmospheres={
 rain:{color:'#101c2c',lights:[{id:'门前',x:720,y:340,width:680,height:620,color:'#7f9cbd',strength:.12}]},
 street:{color:'#151b24',lights:[{id:'冷光',x:640,y:330,width:840,height:570,color:'#8698ad',strength:.10}]},
 room:{color:'#261d19',lights:[{id:'室内暖光',x:500,y:330,width:850,height:660,color:'#d2a477',strength:.23},{id:'门边',x:950,y:370,width:380,height:530,color:'#a2a9b5',strength:.07}]},
 theatre:{color:'#24151e',lights:[{id:'经理',x:810,y:310,width:600,height:610,color:'#e3b383',strength:.23},{id:'尼尔',x:425,y:330,width:420,height:570,color:'#a2b0c8',strength:.10}]}
};
