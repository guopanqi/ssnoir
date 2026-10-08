// 六幕拍点数据：对白、舞台指示的唯一来源，编排（姿势、走位、道具、光、声）也在这里。
// text 必须与 tools/baseline.txt 逐字一致，由 tools/check-script.mjs 校验。
// cue 字段都是“绝对状态”，因此可以从头重建任意拍点：
//   poses {角色: 姿势id}      move {角色: x}       show {角色: 0|1}
//   facing {角色: 0|1}        props {道具id: 状态}  fx {rain|tears|...}
//   light {focus: 角色|null}  sfx 名称              hold 秒数
//   seq [{at: 毫秒, ...cue}]  从本拍开始按时间追加的舞台动作
PT.SCENES = [
  {
    id: 's1',
    title: '片段一：雨夜求助',
    setting: '夜晚，公寓楼下，外面下着雨。',
    cast: '夜莺；尼尔通过入户电铃回应，不在楼下。',
    note: '夜莺第一次来找尼尔。她确实遇到麻烦，也担心被拒绝，仍努力保持体面。尼尔不知道她是谁，没有预先答应接下委托。',
    colors: { bg: '#0d1626', floor: '#070d1a', line: '#3d5f92' },
    initial: {
      fx: { rain: 1, tears: 0 },
      light: { focus: null },
      props: { intercom: { show: 1, lit: 0 }, aptDoor: { show: 1, open: 0 }, umbrellaLean: { show: 0 } },
      actors: {
        夜莺: { pose: 'n-umbrella', x: 240, show: 1, facing: 1, dim: 0 },
        尼尔: { pose: 'e-stand', x: 700, show: 0, facing: 1, dim: 0 },
      },
    },
    beats: [
      {
        dir: '（夜莺来到公寓楼下，按下301的电铃。）', hold: 1.2,
        cue: {
          move: { 夜莺: 840 }, poses: { 夜莺: 'n-ring' }, sfx: 'buzz',
          seq: [{ at: 0, props: { intercom: { show: 1, lit: 1 } } }],
        },
      },
      { say: '夜莺', text: '尼尔先生？', cue: { light: { focus: '夜莺' } } },
      { say: '尼尔', via: '电铃', text: '是，你是？', cue: { light: { focus: null }, sfx: 'click', fx: { intercomGlow: 1 } } },
      {
        say: '夜莺', text: '我听朋友说起过你，我遇到了一些麻烦，我想请你帮忙。',
        cue: { light: { focus: '夜莺' }, poses: { 夜莺: 'n-lean' }, fx: { intercomGlow: 0 } },
      },
      { say: '尼尔', via: '电铃', text: '现在很晚了，而且……', cue: { light: { focus: null }, poses: { 夜莺: 'n-bow' }, fx: { intercomGlow: 1 } } },
      {
        say: '夜莺', text: '就几分钟，先生，而且这事有点急，我怕明天就太晚了。',
        cue: { light: { focus: '夜莺' }, poses: { 夜莺: 'n-fist' }, move: { 夜莺: 822 }, fx: { intercomGlow: 0 } },
      },
      {
        dir: '（尼尔没有立即回答。）', hold: 1.8,
        cue: { light: { focus: null }, poses: { 夜莺: 'n-arms' }, fx: { intercomGlow: 0 },
          seq: [{ at: 300, props: { umbrellaLean: { show: 1 } } }] },
      },
      { say: '夜莺', text: '可以吗？', cue: { light: { focus: '夜莺' }, poses: { 夜莺: 'n-plead' }, move: { 夜莺: 834 } } },
      {
        dir: '（电铃传来开锁声，公寓楼下的门开了。夜莺获准进入。）', hold: 3.6,
        cue: { light: { focus: null }, sfx: 'unlock',
          seq: [
            { at: 500, props: { aptDoor: { show: 1, open: 1 } }, sfx: 'creak', light: { focus: null } },
            { at: 1400, poses: { 夜莺: 'n-back' }, props: { umbrellaLean: { show: 0 } } },
            { at: 2100, move: { 夜莺: 1020 }, sfx: 'step' },
            { at: 2900, show: { 夜莺: 0 }, sfx: 'step' },
          ] },
      },
    ],
  },

  {
    id: 's2',
    title: '片段二：看见烟盒',
    setting: '尼尔与夜莺见面。尼尔刚追丢了收取勒索款的人。',
    cast: '尼尔、夜莺。',
    propNote: '关键道具：一包红色外壳的“金牌”香烟，烟盒已经被挤变形。',
    note: '尼尔不知道勒索者的身份。本实验统一采用：夜莺看到烟盒后，联想到前男友莱恩，但尚不能确定，也没有向尼尔说出这个怀疑。她对回到老街有真实的抗拒。',
    colors: { bg: '#1c1913', floor: '#120f0b', line: '#6b5730' },
    initial: {
      fx: { rain: 0, tears: 0, intercomGlow: 0 },
      light: { focus: null },
      props: { pack: { show: 0, crush: 0 } },
      actors: {
        尼尔: { pose: 'e-fist', x: 400, show: 1, facing: 1, dim: 0 },
        夜莺: { pose: 'n-arms', x: 880, show: 1, facing: -1, dim: 0 },
      },
    },
    beats: [
      { say: '尼尔', text: '我没追到他，该死，在路口的时候突然窜出一辆摩托，我把它跟丢了。', cue: { light: { focus: '尼尔' }, poses: { 尼尔: 'e-fist' } } },
      { say: '夜莺', text: '你人没事儿就好。', cue: { light: { focus: '夜莺' }, poses: { 夜莺: 'n-lean' }, move: { 夜莺: 846 } } },
      {
        say: '尼尔', text: '我拿到了这个。',
        cue: { light: { focus: '尼尔' }, poses: { 尼尔: 'e-reach' }, props: { pack: { show: 1 } } },
      },
      {
        dir: '（尼尔拿出烟盒，夜莺看了一会儿。）', hold: 2.0, sfx: 'rustle',
        cue: {
          light: { focus: null }, poses: { 夜莺: 'n-bow' }, move: { 夜莺: 812 },
        },
      },
      { say: '夜莺', text: '这是……他的？', cue: { light: { focus: '夜莺' }, poses: { 夜莺: 'n-chin' } } },
      { say: '尼尔', text: '追的时候从他身上掉下来的，“金牌”香烟。', cue: { light: { focus: '尼尔' }, poses: { 尼尔: 'e-reach' } } },
      {
        say: '尼尔', text: '烟盒经常塞在口袋里，都被挤变形了，一定是经常进行大幅度的活动。',
        cue: { light: { focus: '尼尔' }, poses: { 尼尔: 'e-accuse' }, props: { pack: { crush: 1 } } },
      },
      { say: '夜莺', text: '然后呢？大侦探？还有什么？', cue: { light: { focus: '夜莺' }, poses: { 夜莺: 'n-arms' }, move: { 夜莺: 838 } } },
      { say: '尼尔', text: '没了，我没什么能想到的。他从街道的桥上跳下去，应该对这一带很熟，我准备去问问。', cue: { light: { focus: '尼尔' }, poses: { 尼尔: 'e-stand' }, props: { pack: { show: 0 } } } },
      { say: '夜莺', text: '老街？', cue: { light: { focus: '夜莺' }, poses: { 夜莺: 'n-chin' } } },
      { say: '尼尔', text: '对。', cue: { light: { focus: '尼尔' } } },
      {
        say: '夜莺', text: '我可不回去。',
        cue: { light: { focus: '夜莺' }, poses: { 夜莺: 'n-back' }, move: { 夜莺: 916 } },
      },
      { say: '尼尔', text: '我没说要带上你……你不喜欢那里？', cue: { light: { focus: '尼尔' }, poses: { 尼尔: 'e-arms' } } },
      {
        say: '夜莺', text: '我花了很久才从里面走出来，我不回去。',
        cue: { light: { focus: '夜莺' }, move: { 夜莺: 956 }, hold: 1.2 },
      },
    ],
  },

  {
    id: 's3',
    title: '片段三：隐瞒莱恩后的争执',
    setting: '离开老街后，两人终于能够单独谈话。',
    cast: '尼尔、夜莺。',
    note: '夜莺已经陪尼尔进入老街，调查中有人提到了莱恩。她此前隐瞒了这个前男友，以及自己对他的怀疑。她害怕尼尔把这件事看成私人纠葛，不愿接下这个没钱的委托。尼尔刚意识到自己被隐瞒。他仍愿意继续调查，但这次解释没有立即消除他的不信任。',
    colors: { bg: '#171428', floor: '#0f0d1c', line: '#5a4f8c' },
    initial: {
      fx: { rain: 0, tears: 0, intercomGlow: 0 },
      light: { focus: null },
      props: { streetLamp: { show: 1, on: 1 } },
      actors: {
        尼尔: { pose: 'e-stand', x: 430, show: 1, facing: 1, dim: 0 },
        夜莺: { pose: 'n-arms', x: 866, show: 1, facing: -1, dim: 0 },
      },
    },
    beats: [
      { say: '尼尔', text: '现在可以说了？', cue: { light: { focus: '尼尔' }, poses: { 尼尔: 'e-stand' }, move: { 尼尔: 452 } } },
      {
        say: '夜莺', text: '莱恩是我……之前的男朋友。',
        cue: { light: { focus: '夜莺' }, poses: { 夜莺: 'n-bow' }, move: { 夜莺: 838 } },
      },
      { say: '尼尔', text: '继续。', cue: { light: { focus: '尼尔' }, poses: { 尼尔: 'e-light' } } },
      {
        say: '夜莺', text: '他不会做出这样的事情来，他，他怎么会这样？',
        cue: { light: { focus: '夜莺' }, poses: { 夜莺: 'n-chin' }, move: { 夜莺: 800 } },
      },
      {
        say: '尼尔', text: '第一次的时候我就问过你，你为什么没有告诉我？',
        cue: { light: { focus: '尼尔' }, poses: { 尼尔: 'e-accuse' }, move: { 尼尔: 502 } },
      },
      {
        say: '夜莺', text: '我不能确定，而且我觉得你会不愿意管，一个没钱的女孩，一个以前的男人，如果我真的告诉你，你还会帮我吗？',
        cue: { light: { focus: '夜莺' }, poses: { 夜莺: 'n-plead' }, move: { 夜莺: 792 } },
      },
      { dir: '（尼尔没有立即回答。）', hold: 1.8, cue: { light: { focus: null }, poses: { 尼尔: 'e-listen' } } },
      { say: '尼尔', text: '也许会，也许不会，我不知道。', cue: { light: { focus: '尼尔' }, poses: { 尼尔: 'e-arms' }, move: { 尼尔: 470 } } },
      {
        say: '夜莺', text: '分手之后，他找过我，我总是甩掉他。后来我离开老街之后，他到剧院找我，但都被保安拦住了，我们没有再见过面了。',
        cue: { light: { focus: '夜莺' }, poses: { 夜莺: 'n-bow' } },
      },
      {
        say: '尼尔', text: '你还有什么是瞒着我的？',
        cue: { light: { focus: '尼尔' }, poses: { 尼尔: 'e-fist' }, move: { 尼尔: 546 } },
      },
      {
        say: '夜莺', text: '没有了，尼尔，我知道的就这些了，真的。',
        cue: { light: { focus: '夜莺' }, poses: { 夜莺: 'n-plead' }, move: { 夜莺: 846 } },
      },
      { dir: '（尼尔沉默。）', hold: 1.8, cue: { light: { focus: null }, poses: { 尼尔: 'e-listen' } } },
      {
        say: '夜莺', text: '我知道他住的地方，我们去看看吧。',
        cue: { light: { focus: '夜莺' }, poses: { 夜莺: 'n-lean' }, move: { 夜莺: 892 } },
      },
      {
        dir: '（夜莺准备继续带路。尼尔停顿了一会儿，随后跟上。）', hold: 3.4,
        cue: {
          seq: [
            { at: 0, poses: { 夜莺: 'n-back' }, move: { 夜莺: 960 }, sfx: 'step' },
            { at: 1200, move: { 夜莺: 1060 }, sfx: 'step' },
            { at: 1900, poses: { 尼尔: 'e-back' }, sfx: 'step' },
            { at: 2600, move: { 尼尔: 700 }, sfx: 'step' },
          ] },
      },
    ],
  },

  {
    id: 's4',
    title: '片段四：准备离开，被叫住',
    setting: '尼尔的房间。第一次求助的谈话接近结束。',
    cast: '尼尔、夜莺。',
    propNote: '有关物品：夜莺的随身东西、帽子和雨伞。',
    note: '夜莺凑不够勒索款，也没有钱立即支付尼尔的报酬。尼尔还没有答应帮她。夜莺认为求助失败，确实准备离开，不是在故意试探他。尼尔最后开口，决定接下这个对自己并不划算的委托。',
    colors: { bg: '#241a12', floor: '#160f0a', line: '#7a5326' },
    initial: {
      fx: { rain: 0, tears: 0, intercomGlow: 0 },
      light: { focus: null },
      props: {
        sideTable: { show: 1 },
        hat: { show: 1 }, bag: { show: 1 }, umbrellaFold: { show: 1 },
      },
      actors: {
        尼尔: { pose: 'e-arms', x: 430, show: 1, facing: 1, dim: 0 },
        夜莺: { pose: 'n-lean', x: 806, show: 1, facing: -1, dim: 0 },
      },
    },
    beats: [
      { say: '尼尔', text: '那我的报酬呢？你准备怎么付我的报酬？', cue: { light: { focus: '尼尔' }, poses: { 尼尔: 'e-arms' } } },
      {
        say: '夜莺', text: '演出之后可以吗？经理说过些天会有一场大型的演出，应该会有些钱。虽然之前很多次小一些的演出，他也没给过我什么钱。',
        cue: { light: { focus: '夜莺' }, poses: { 夜莺: 'n-lean' }, move: { 夜莺: 780 } },
      },
      { say: '夜莺', text: '当时他说会有些钱的，但是也没给。', cue: { light: { focus: '夜莺' }, poses: { 夜莺: 'n-bow' } } },
      { say: '夜莺', text: '这次机会很重要，我不愿意错过。', cue: { light: { focus: '夜莺' }, poses: { 夜莺: 'n-chin' } } },
      {
        dir: '（尼尔没有回答。夜莺不愿让他看见自己的眼泪。）', hold: 2.2,
        cue: { light: { focus: null }, poses: { 尼尔: 'e-listen', 夜莺: 'n-cover' }, move: { 夜莺: 836 } },
      },
      { say: '夜莺', text: '我明白，确实太麻烦你了。', cue: { light: { focus: '夜莺' }, poses: { 夜莺: 'n-bow' }, move: { 夜莺: 866 } } },
      {
        dir: '（她收起东西，戴好帽子，准备拿伞离开。）', hold: 2.4, sfx: 'rustle',
        cue: {
          seq: [
            { at: 200, props: { hat: { show: 0 }, bag: { show: 0 }, umbrellaFold: { show: 0 } } },
            { at: 900, poses: { 夜莺: 'n-leave' } },
          ] },
      },
      { say: '夜莺', text: '谢谢你，尼尔先生。', cue: { light: { focus: '夜莺' }, move: { 夜莺: 916 } } },
      { say: '尼尔', text: '演出还有多久？', cue: { light: { focus: '尼尔' }, poses: { 尼尔: 'e-stand' }, move: { 尼尔: 470 } } },
      { say: '夜莺', text: '就这两周了。', cue: { light: { focus: '夜莺' }, poses: { 夜莺: 'n-glance' } } },
      {
        say: '尼尔', text: '我们得先想办法凑够赎金，只要他来拿钱，我有办法弄明白他是谁。',
        cue: { light: { focus: '尼尔' }, poses: { 尼尔: 'e-arms' }, move: { 尼尔: 508 } },
      },
      {
        dir: '（夜莺意识到，他答应了。眼泪流了下来。）', hold: 3.0,
        cue: { light: { focus: '夜莺' }, fx: { tears: 2 },
          seq: [{ at: 400, poses: { 夜莺: 'n-cover' } }] },
      },
    ],
  },

  {
    id: 's5',
    title: '片段五：经理安装电话',
    setting: '尼尔回到公寓，发现经理和安装电话的工人在等他。',
    cast: '尼尔、经理；工人可作为次要人物。',
    propNote: '关键道具：正在安装的电话。',
    note: '经理此时认为尼尔对自己有价值，对他热情而熟络。他给尼尔提供实际方便，也把这件事张扬地表现成两人的交情。他不觉得需要先征求尼尔同意。尼尔意外，还没有来得及接受或拒绝。经理搭肩、拍肩的身体接触是本段需要表达的情节。',
    colors: { bg: '#1a2024', floor: '#111519', line: '#4d6b73' },
    initial: {
      fx: { rain: 0, tears: 0, intercomGlow: 0 },
      light: { focus: null },
      props: { aptDoor: { show: 1, open: 0, x: 150 }, wallPhone: { show: 1 } },
      actors: {
        尼尔: { pose: 'e-back', x: 240, show: 1, facing: 1, dim: 0 },
        经理: { pose: 'm-stand', x: 560, show: 1, facing: 1, dim: 0 },
        工人: { pose: 'w-kneel', x: 1112, show: 1, facing: -1, dim: 0 },
      },
    },
    beats: [
      {
        dir: '（尼尔回家，发现有人在等着。）', hold: 3.0, sfx: 'creak',
        cue: {
          seq: [
            { at: 0, props: { aptDoor: { show: 1, open: 1 } } },
            { at: 700, move: { 尼尔: 792 } },
            { at: 2000, poses: { 尼尔: 'e-stand' }, facing: { 尼尔: -1 }, props: { aptDoor: { show: 1, open: 0 } } },
            { at: 2300, light: { focus: '经理' } },
          ] },
      },
      { say: '经理', text: '尼尔！', cue: { light: { focus: '经理' }, poses: { 经理: 'm-stand' } } },
      {
        dir: '（经理迎上来，拍尼尔的肩膀，熟络地搭住他的肩。）', hold: 2.6, sfx: 'pat',
        cue: {
          seq: [
            { at: 0, move: { 经理: 690 }, poses: { 经理: 'm-shoulder' } },
            { at: 900, move: { 尼尔: 852 }, poses: { 尼尔: 'e-stand' } },
            { at: 1400, sfx: 'pat' },
          ] },
      },
      { say: '尼尔', text: '这是干嘛？', cue: { light: { focus: '尼尔' }, move: { 尼尔: 866 } } },
      { say: '经理', text: '电话！以后出了事情，还让人通过门房留言吗？', cue: { light: { focus: '经理' }, poses: { 经理: 'm-shoulder' } } },
      { say: '经理', text: '大侦探得有个电话。', cue: { light: { focus: '经理' } } },
      { say: '尼尔', text: '我还……', cue: { light: { focus: '尼尔' }, poses: { 尼尔: 'e-hatoff' } } },
      { say: '经理', text: '早晚的事。', cue: { light: { focus: '经理' } } },
      {
        dir: '（经理转向工人。）', hold: 2.2,
        cue: {
          seq: [
            { at: 0, move: { 经理: 626 }, poses: { 经理: 'm-point' } },
            { at: 700, move: { 尼尔: 900 }, poses: { 尼尔: 'e-arms' }, facing: { 尼尔: -1 } },
            { at: 1400, light: { focus: '经理' } },
          ] },
      },
      {
        say: '经理', text: '给他装好的！最好的线！', hold: 1.4,
        cue: { light: { focus: '经理' }, poses: { 经理: 'm-point' },
          seq: [{ at: 900, sfx: 'ring', props: { wallPhone: { show: 1, lit: 1 } } }] },
      },
    ],
  },

  {
    id: 's6',
    title: '片段六：经理解雇尼尔',
    setting: '剧院。经理找尼尔谈调查进展。',
    cast: '尼尔、经理。',
    note: '尼尔未能在经理期待的期限内解决问题。经理决定换人，仍认为自己客气、合理，没有必要因此伤了交情。尼尔感到被否定和羞辱，但没有与经理争吵。不要擅自增加反击台词。这段与“安装电话”是同一个经理、同一段关系的另一面。',
    colors: { bg: '#2b1418', floor: '#1b0d10', line: '#8c3f47' },
    initial: {
      fx: { rain: 0, tears: 0, intercomGlow: 0 },
      light: { focus: null },
      props: { theatreDoor: { show: 1, open: 0, shut: 0 }, papers: { show: 0 } },
      actors: {
        经理: { pose: 'm-stand', x: 452, show: 1, facing: 1, dim: 0 },
        尼尔: { pose: 'e-stand', x: 862, show: 1, facing: -1, dim: 0 },
      },
    },
    beats: [
      { say: '经理', text: '尼尔，我们必须得好好谈谈了。', cue: { light: { focus: '经理' }, move: { 经理: 486 }, poses: { 经理: 'm-stand' } } },
      { dir: '（尼尔沉默。）', hold: 1.6, cue: { light: { focus: null }, poses: { 尼尔: 'e-listen' } } },
      { say: '经理', text: '到目前为止已经有几天了，事情有什么重要的进展吗？', cue: { light: { focus: '经理' }, poses: { 经理: 'm-stand' } } },
      { dir: '（尼尔没有回答。）', hold: 1.5, cue: { light: { focus: null } } },
      {
        say: '经理', text: '我只针对事情，不针对人。这是一份工作，我们每个人都在自己的岗位上，为这件事出一份力。',
        cue: { light: { focus: '经理' }, move: { 经理: 520 } },
      },
      {
        say: '经理', text: '就拿我来说，我每天都在为赞助和演出奔走，如果我没能做得很好，我自己就不配呆在这个岗位上，我就要开除我自己。',
        cue: { light: { focus: '经理' }, poses: { 经理: 'm-shoulder' } },
      },
      { dir: '（尼尔沉默。）', hold: 1.6, cue: { light: { focus: null }, poses: { 尼尔: 'e-listen' } } },
      { say: '经理', text: '如果我们不能为这件事情的解决作出贡献，那我们在这儿不是浪费时间吗？', cue: { light: { focus: '经理' }, poses: { 经理: 'm-stand' } } },
      { dir: '（尼尔仍没有回应。）', hold: 1.6, cue: { light: { focus: null }, poses: { 尼尔: 'e-hatoff' } } },
      {
        say: '经理', text: '尼尔，我其实很不情愿，但是这件事情你可以先放一放了，我可能会找一个更老练的侦探。但也别那么绝情，别断了联系。',
        cue: { light: { focus: '经理' }, poses: { 经理: 'm-shoulder' }, move: { 经理: 566 } },
      },
      {
        dir: '（尼尔交接已知信息，具体内容略过。他离开剧院，身后的大门砰地关上。）', hold: 5.2,
        cue: {
          seq: [
            { at: 0, poses: { 尼尔: 'e-reach' }, light: { focus: '尼尔' }, props: { papers: { show: 1 } } },
            { at: 1100, props: { papers: { show: 0 } }, sfx: 'rustle' },
            { at: 1700, poses: { 尼尔: 'e-back' }, light: { focus: null }, move: { 尼尔: 1010 } },
            { at: 2700, move: { 尼尔: 1108 }, sfx: 'step' },
            { at: 3400, props: { theatreDoor: { show: 1, open: 1, shut: 0 } }, sfx: 'creak' },
            { at: 4200, props: { theatreDoor: { show: 1, open: 0, shut: 1 } }, sfx: 'slam', shake: 1, show: { 尼尔: 0 } },
            { at: 4700, poses: { 经理: 'm-stand' } },
          ] },
      },
    ],
  },
];
