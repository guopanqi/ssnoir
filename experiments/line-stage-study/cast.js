export const castStyles=[
 {id:'neonEdge',name:'N2 断线暗面',note:'来自原 Neon：深色实体与局部浅色长弧，卷发与肩颈形成辨识；夜莺保留礼服，尼尔提炼原 Neon 的低圆帽与侧脸。人物统一中性姿势，自带描线无需额外加线。',folder:'05-neon-selective-edge',neilFolder:'08-neil-n2-neon'},
 {id:'curlCoat',name:'C1 卷发外套',note:'在 C 上恢复额前发浪、卷发和细长侧脸；保留户外外套与手包，尼尔提炼原 Neon 轮廓。人物统一中性姿势，可点击推荐描线补浅色外缘。',folder:'06-c-curl-coat',neilFolder:'09-neil-c1-neon'},
 {id:'storySoft',name:'G1 柔软手绘',note:'借鉴 Florence 的手绘叙事表达：柔软墨线、石灰肤色与少量平涂，保留夜莺的卷发和谨慎神情。',folder:'20-story-soft-ink',neilFolder:'09-neil-c1-neon'},
 {id:'storyRetro',name:'G2 复古叙事',note:'借鉴 Overboard! 的角色辨识：强调侧脸、额前发浪与小块暗红唇色，采用较明确的深色轮廓。',folder:'21-story-retro',neilFolder:'09-neil-c1-neon'},
 {id:'storyOrganic',name:'G3 有机平涂',note:'借鉴 Mutazione 的有机造型：长曲线、大块哑光填充，减少发丝与衣褶，用侧脸传递情绪。',folder:'22-story-organic',neilFolder:'09-neil-c1-neon'},
 {id:'storyDuo',name:'G4 双色长弧',note:'原创双色叙事造型：深色实心礼服与浅色皮肤，少量粗细变化的长弧，检查进一步简化后的辨识度。',folder:'23-story-two-tone',neilFolder:'09-neil-c1-neon'},
 {id:'storyBrush',name:'G5 松笔漫画',note:'不以 N2 图像锁定造型，按夜莺身份重新设计：更蓬松的卷发、瘦长侧脸和不均匀墨线，姿势仍是一手靠近锁骨。',folder:'24-story-brush',neilFolder:'09-neil-c1-neon'},
 {id:"noirPaint1",name:"P1 墨洗体积",note:"灰蓝墨洗、柔软明暗与填色体积；卷发仍保留清晰发浪。",folder:"25-noir-wash",neilFolder:'09-neil-c1-neon'},
 {id:"noirPaint2",name:"P2 半面阴影",note:"侧光将脸、肩和手连成亮面，深色阴影保持实体；表情克制。",folder:"26-noir-half-shadow",neilFolder:'09-neil-c1-neon'},
 {id:"noirPaint3",name:"P3 哑光厚涂",note:"哑光大色面与少量干笔，面部偏写实，强调成年人的疲惫感。",folder:"27-noir-dry-gouache",neilFolder:'09-neil-c1-neon'},
 {id:"noirPaint4",name:"P4 舞台暗面",note:"大片深色体块，脸和手从暗处显现；用舞台光塑造人物体积。",folder:"28-noir-stage-volume",neilFolder:'09-neil-c1-neon'},
];
export function sprite(styleId,actor,pose){
 const style=castStyles.find(s=>s.id===styleId);if(!style)throw Error(`Unknown cast style: ${styleId}`);
 return `assets/${actor==='neil'?style.neilFolder:style.folder}/${actor}-neutral.png`;
}
