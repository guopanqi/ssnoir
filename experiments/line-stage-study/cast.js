export const castStyles=[
 {id:'neonEdge',name:'N2 断线暗面',note:'来自原 Neon：深色实体与局部浅色长弧，卷发与肩颈形成辨识；夜莺保留礼服，尼尔提炼原 Neon 的低圆帽与侧脸。人物统一中性姿势，自带描线无需额外加线。',folder:'05-neon-selective-edge',neilFolder:'08-neil-n2-neon'},
 {id:'curlCoat',name:'C1 卷发外套',note:'在 C 上恢复额前发浪、卷发和细长侧脸；保留户外外套与手包，尼尔提炼原 Neon 轮廓。人物统一中性姿势，可点击推荐描线补浅色外缘。',folder:'06-c-curl-coat',neilFolder:'09-neil-c1-neon'},
 {id:'n2Fine',name:'S1 细亮线',note:'N2 深色实体与淡蓝细线，卷发用更长的弧线重画。',folder:'14-n2-fine-contour',neilFolder:'09-neil-c1-neon'},
 {id:'n2Rim',name:'S2 暖光剪影',note:'N2 剪影退入暗面，暖色边光强调额前发浪与身段，内部细节最少。',folder:'15-n2-gold-rim',neilFolder:'09-neil-c1-neon'},
 {id:'n2Topo',name:'S3 等高线',note:'用密集等高线重画卷发和人体体积；这是生成画稿中的线条，不是后处理覆盖。',folder:'16-n2-topography',neilFolder:'09-neil-c1-neon'},
 {id:'n2Deco',name:'S4 Deco 金线',note:'深青灰礼服与金色长弧，把额前卷发整理为装饰艺术的波浪。',folder:'17-n2-deco',neilFolder:'09-neil-c1-neon'},
 {id:'n2Wood',name:'S5 黑白版画',note:'黑白大面与少量刻线，浅色侧脸更突出，检查在暗布景中的对比。',folder:'18-n2-woodcut',neilFolder:'09-neil-c1-neon'},
 {id:'n2Loop',name:'S6 连笔曲线',note:'用疏朗的连笔弧线概括侧脸、肩袖与裙摆；保留实心填充，并非真实 SVG 单一路径。',folder:'19-n2-loop-line',neilFolder:'09-neil-c1-neon'},
];
export function sprite(styleId,actor,pose){
 const style=castStyles.find(s=>s.id===styleId);if(!style)throw Error(`Unknown cast style: ${styleId}`);
 return `assets/${actor==='neil'?style.neilFolder:style.folder}/${actor}-neutral.png`;
}
