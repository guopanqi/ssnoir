import { studies } from './studies.js';
export const cast = {
  守门人: { color: '#899499', height: 430, silhouette: true, poses: { 基础: '/assets/neil-listen.png' } },
  经理: { color: '#d0aa72', height: 420, poses: { 基础: '/assets/manager.png' } },
  夜莺: { color: '#c68c70', height: 405, poses: { 基础: '/assets/nightingale-wait.png', 低头: '/assets/nightingale-down.png' } },
  尼尔: { color: '#b8c2c7', height: 430, poses: { 基础: '/assets/neil-listen.png', 摘帽: '/assets/neil-hat.png' } },
};
const say = (actor, text, extra = {}) => ({ type: 'say', actor, text, ...extra });
export const stories = [
  { id: 'rain', title: '雨夜来客', subtitle: '第一场 · 公寓楼下', note: '一次靠近，一次低头。其余的情绪交给声音与停顿。', beats: [
    { label: '走到门前', commands: [{ type: 'scene', scene: 'rain' }, { type: 'enter', actor: '夜莺', x: -130 }, { type: 'move', actor: '夜莺', x: 705, duration: 1800 }] },
    { label: '按响电铃', commands: [{ type: 'effect', effect: 'bell' }, { type: 'wait', duration: 650 }, say('尼尔', '是，哪位？', { remote: true })] },
    { label: '第一次开口', commands: [say('夜莺', '尼尔先生？朋友介绍我来的。')] },
    { label: '说明来意', commands: [say('夜莺', '我遇到些麻烦，想请你帮忙。')] },
    { label: '门内的迟疑', commands: [say('尼尔', '现在很晚了，而且……', { remote: true })] },
    { label: '争取几分钟', commands: [say('夜莺', '就几分钟，先生。事情有些急。')] },
    { label: '不能等到明天', commands: [say('夜莺', '我怕明天就太晚了。')] },
    { label: '没有回答', commands: [{ type: 'silence' }, { type: 'wait', duration: 1100 }, { type: 'pose', actor: '夜莺', pose: '低头' }, { type: 'wait', duration: 550 }, say('夜莺', '可以吗？')] },
    { label: '门开了', commands: [{ type: 'effect', effect: 'unlock' }, say('尼尔', '进来吧。', { remote: true })] },
    { label: '终于获准', commands: [{ type: 'pose', actor: '夜莺', pose: '基础' }, say('夜莺', '谢谢。')] },
    { label: '走入暖光', commands: [{ type: 'silence' }, { type: 'move', actor: '夜莺', x: 960, duration: 850 }, { type: 'exit', actor: '夜莺', duration: 350 }, { type: 'effect', effect: 'fade' }, { type: 'wait', duration: 400 }] },
  ] },
  { id: 'room', title: '一份赔钱的委托', subtitle: '第二场 · 尼尔的房间', note: '离开不是欲擒故纵。叫住她的那句话，才改变两人的距离。', beats: [
    { label: '雨声留在窗外', commands: [{ type: 'scene', scene: 'room' }, { type: 'enter', actor: '尼尔', x: 420 }, { type: 'enter', actor: '夜莺', x: 850 }, say('尼尔', '照片有些出格。但你为什么这么紧张？')] },
    { label: '她的机会', commands: [say('夜莺', '我过些天有一场演出。他可能不止有这一张。')] },
    { label: '想起过去', commands: [say('尼尔', '你能想到，谁会干这种事吗？')] },
    { label: '隐瞒', commands: [{ type: 'pose', actor: '夜莺', pose: '低头' }, { type: 'wait', duration: 400 }, say('夜莺', '……没有。我和他们很久都不联系了。')] },
    { label: '报酬', commands: [say('尼尔', '那我的报酬呢？你准备怎么付？')] },
    { label: '尚未到手的钱', commands: [say('夜莺', '演出之后可以吗？经理说，这次会有些钱。')] },
    { label: '不愿再求', commands: [{ type: 'wait', duration: 500 }, say('夜莺', '我明白。确实太麻烦你了。')] },
    { label: '准备告辞', commands: [{ type: 'pose', actor: '夜莺', pose: '基础' }, say('夜莺', '谢谢你，尼尔先生。'), { type: 'silence' }, { type: 'move', actor: '夜莺', x: 1000, duration: 850 }] },
    { label: '叫住她', commands: [{ type: 'pose', actor: '尼尔', pose: '摘帽' }, say('尼尔', '演出还有多久？')] },
    { label: '回过身来', commands: [{ type: 'move', actor: '夜莺', x: 890, duration: 550 }, say('夜莺', '就这两周了。')] },
    { label: '接下委托', commands: [say('尼尔', '先想办法凑够钱。他来拿钱，我就能查出是谁。')] },
    { label: '这次不用说谢谢', commands: [{ type: 'silence' }, { type: 'wait', duration: 700 }, { type: 'move', actor: '夜莺', x: 805, duration: 650 }, say('世界', '我还没想明白，为什么接下这个赔钱的生意。', { inner: true })] },
  ] },
  { id: 'effects', title: '舞台动作试台', subtitle: '附场 · 表现手段', note: '这里单独检查移动、换姿势、灯光、震动与闪光，不把它们全部塞进剧情。', beats: [
    { label: '两位演员', commands: [{ type: 'scene', scene: 'room' }, { type: 'enter', actor: '尼尔', x: 410 }, { type: 'enter', actor: '夜莺', x: 880 }, say('世界', '先看距离，再看动作。', { inner: true })] },
    { label: '同时靠近', commands: [{ type: 'parallel', commands: [{ type: 'move', actor: '尼尔', x: 500, duration: 700 }, { type: 'move', actor: '夜莺', x: 790, duration: 700 }] }, say('尼尔', '靠近，只在关系改变时发生。')] },
    { label: '换姿势', commands: [{ type: 'pose', actor: '夜莺', pose: '低头' }, say('夜莺', '换图保持脚底位置，不让人物跳起来。')] },
    { label: '压低灯光', commands: [{ type: 'light', actor: '夜莺', value: .55 }, say('尼尔', '灯光可以变弱，不必每句话都闪烁。')] },
    { label: '局部震动', commands: [{ type: 'effect', effect: 'shake', actor: '尼尔' }, say('尼尔', '短促的震动，留给真正受到冲击的一刻。')] },
    { label: '全场闪光', commands: [{ type: 'effect', effect: 'flash' }, { type: 'pose', actor: '夜莺', pose: '基础' }, { type: 'light', actor: '夜莺', value: 1 }, say('世界', '试台结束。', { inner: true })] },
  ] },
  ...studies,
];
