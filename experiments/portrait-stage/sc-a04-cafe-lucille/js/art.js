/* 线绘舞台：清晨的咖啡馆。
   构造保持简单：一面墙、一扇窗、一扇门、一张柜台、两张桌子。
   线条由 CSS 逐笔画出；填色在 .built 后淡入。 */
(function () {
  const LINE = '#c9d6ea';
  const LINE_DIM = '#7587a4';
  const LINE_DEEP = '#3b4a63';
  const WARM = '#e6c48c';

  const ink = (d, o = {}) => {
    const {
      delay = 0,
      dur = 1.05,
      width = 1.7,
      stroke = LINE,
      cls = '',
      cap = 'round',
    } = o;
    return `<path class="ink ${cls}" pathLength="1" d="${d}" style="--delay:${delay}s;--dur:${dur}s;stroke:${stroke};stroke-width:${width};stroke-linecap:${cap}"/>`;
  };

  const line = (x1, y1, x2, y2, o) => ink(`M${x1} ${y1}L${x2} ${y2}`, o);
  const box = (x, y, w, h, o) => ink(`M${x} ${y}H${x + w}V${y + h}H${x}Z`, o);
  const poly = (pts, close = false, o = {}) =>
    ink(`M${pts.map(p => p.join(' ')).join('L')}${close ? 'Z' : ''}`, o);

  const solid = (d, fill, o = {}) => {
    const { cls = '', opacity = 1 } = o;
    return `<path class="solid ${cls}" d="${d}" fill="${fill}" opacity="${opacity}"/>`;
  };
  const solidRect = (x, y, w, h, fill, o = {}) => {
    const { cls = '', opacity = 1 } = o;
    return `<rect class="solid ${cls}" x="${x}" y="${y}" width="${w}" height="${h}" fill="${fill}" opacity="${opacity}"/>`;
  };

  const cup = (x, y, s = 1, o = {}) => {
    const d = o.delay || 0;
    return `<g transform="translate(${x} ${y}) scale(${s})">
      <ellipse cx="0" cy="0" rx="13" ry="3.6" fill="none" stroke="#aab9d2" stroke-width="1.2" opacity=".85"/>
      <path d="M-10 -1L-7 20H7L10 -1" fill="#0b0f18" fill-opacity=".5" stroke="#c3d0e4" stroke-width="1.3"/>
      <path d="M-7 7H7L6 19H-6Z" fill="#6b4527" opacity=".55"/>
      <path d="M10 2Q16 4 15 9Q14 14 9 14" fill="none" stroke="#c3d0e4" stroke-width="1.1"/>
      ${o.steam ? `<path class="steam" d="M-3 -8Q-7 -15 0 -20Q7 -25 3 -33" fill="none" stroke="#b9c8e0" stroke-width="1" opacity=".35"/>` : ''}
      ${ink(`M-10 -1L-7 20H7L10 -1`, { delay: d, dur: .45, width: 1.2, stroke: '#c3d0e4' })}
    </g>`;
  };

  function backWall() {
    let s = '';
    s += solidRect(0, 0, 1600, 900, '#05070c');
    s += solidRect(0, 62, 1600, 578, '#090c13');
    s += solidRect(0, 640, 1600, 260, '#07090f');

    // 墙线、墙裙、踢脚
    s += line(0, 62, 1600, 62, { delay: .02, dur: 1.1, width: 2 });
    s += line(0, 118, 1600, 118, { delay: .06, dur: 1.1, width: 1, stroke: LINE_DIM });
    s += line(0, 548, 1600, 548, { delay: .22, dur: 1, width: 1.2, stroke: LINE_DIM });
    s += line(0, 640, 1600, 640, { delay: .26, dur: 1.1, width: 1.6 });

    // 墙面分格：只留竖向，不铺满
    for (let x = 70; x < 1600; x += 148) {
      s += line(x, 120, x, 546, { delay: .16 + x / 3200, dur: .7, width: .9, stroke: LINE_DEEP });
    }

    // 地板透视线
    for (let x = -240; x <= 1840; x += 130) {
      s += line(800, 642, x, 900, { delay: .38 + Math.abs(x - 800) / 6000, dur: .7, width: .7, stroke: LINE_DEEP });
    }
    for (const y of [690, 752, 826]) {
      s += line(0, y, 1600, y, { delay: .46, dur: .8, width: .7, stroke: LINE_DEEP });
    }

    return s;
  }

  function windowSet() {
    let s = '';
    const x = 112, y = 152, w = 420, h = 362;

    // 玻璃外的冷色街景
    s += solid(`M${x + 8} ${y + 8}H${x + w - 8}V${y + h - 8}H${x + 8}Z`, '#0b1724', { opacity: .96 });
    s += `<circle cx="208" cy="386" r="120" fill="url(#streetGlow)" opacity=".55"/>`;
    // 对街楼影
    s += `<path class="solid" d="M130 320 V240 H196 V282 H252 V210 H318 V300 H374 V246 H440 V334 H512 V508 H130Z"
      fill="#0a111b" opacity=".9"/>`;
    for (const [wx, wy] of [[154, 270], [214, 248], [280, 306], [344, 260], [402, 292], [462, 340], [474, 394]]) {
      s += `<rect class="solid" x="${wx}" y="${wy}" width="13" height="19" fill="#caa05f" opacity=".35"/>`;
    }

    // 窗框
    const d = `M${x} ${y + h}V${y}H${x + w}V${y + h}Z`;
    s += ink(d, { delay: .1, dur: 1, width: 2.1 });
    s += box(x + 10, y + 10, w - 20, h - 20, { delay: .16, dur: .8, width: 1, stroke: LINE_DIM });
    s += line(x + w / 2, y, x + w / 2, y + h, { delay: .2, dur: .7, width: 1.6 });
    s += line(x, y + h / 2, x + w, y + h / 2, { delay: .24, dur: .7, width: 1.4 });

    // 雨痕
    for (let i = 0; i < 9; i++) {
      const rx = x + 26 + i * 46;
      s += line(rx, y + 24 + (i % 3) * 30, rx - 14, y + 170 + (i % 4) * 28,
        { delay: .34, dur: .75, width: .8, stroke: '#40597a' });
    }

    // 窗台
    s += solidRect(x - 16, y + h, w + 32, 14, '#141820', { opacity: 1 });
    s += line(x - 16, y + h, x + w + 16, y + h, { delay: .28, dur: .8, width: 1.8 });
    s += line(x - 16, y + h + 14, x + w + 16, y + h + 14, { delay: .3, dur: .8, width: 1.1, stroke: LINE_DIM });

    return s;
  }

  function doorSet() {
    let s = '';
    const x = 596, y = 286, w = 156, h = 354;

    s += solidRect(x, y, w, h, '#11141b', { opacity: 1 });
    s += box(x, y, w, h, { delay: .3, dur: .9, width: 1.9 });
    s += box(x + 12, y + 12, w - 24, h - 24, { delay: .36, dur: .8, width: 1, stroke: LINE_DIM });

    // 上半玻璃
    s += solidRect(x + 24, y + 26, w - 48, 150, '#0b1521', { opacity: .95 });
    s += box(x + 24, y + 26, w - 48, 150, { delay: .42, dur: .7, width: 1.2 });
    s += line(x + w / 2, y + 26, x + w / 2, y + 176, { delay: .46, dur: .5, width: .9, stroke: LINE_DIM });
    // 玻璃反光
    s += line(x + 40, y + 154, x + 88, y + 44, { delay: .5, dur: .7, width: .9, stroke: '#4d6688' });

    // 下半门板
    s += box(x + 24, y + 202, w - 48, 126, { delay: .48, dur: .7, width: 1, stroke: LINE_DIM });
    s += line(x + 24, y + 232, x + w - 24, y + 232, { delay: .52, dur: .5, width: .8, stroke: LINE_DEEP });

    // 门把与门铃
    s += `<circle cx="${x + w - 30}" cy="${y + 248}" r="4" fill="#c8a45f" class="soft"/>`;
    s += line(x + w - 30, y + 248, x + w - 30, y + 260, { delay: .56, dur: .4, width: 1.1, stroke: '#a8844d' });

    // OPEN 挂牌
    s += line(x + w / 2, y - 16, x + w / 2, y + 4, { delay: .58, dur: .4, width: 1, stroke: LINE_DIM });
    s += solidRect(x + 38, y + 4, w - 76, 30, '#17120c', { opacity: 1 });
    s += box(x + 38, y + 4, w - 76, 30, { delay: .62, dur: .5, width: 1.1, stroke: WARM });
    s += `<text class="ink" x="${x + w / 2}" y="${y + 25}" text-anchor="middle" font-family="Arial Narrow, Helvetica, sans-serif"
      font-size="16" letter-spacing="3" fill="none" stroke="${WARM}" stroke-width="1"
      style="--delay:.7s;--dur:.45s">OPEN</text>`;

    return s;
  }

  function wallClock() {
    let s = '';
    const x = 834, y = 218, r = 34;
    s += `<circle cx="${x}" cy="${y}" r="${r}" fill="#0d1118" stroke="${LINE}" stroke-width="1.8" class="soft"/>`;
    s += `<circle cx="${x}" cy="${y}" r="${r - 6}" fill="none" stroke="${LINE_DIM}" stroke-width="1"/>`;
    for (let i = 0; i < 12; i++) {
      const a = i * Math.PI / 6;
      s += line(x + Math.sin(a) * (r - 9), y - Math.cos(a) * (r - 9),
        x + Math.sin(a) * (r - 5), y - Math.cos(a) * (r - 5),
        { delay: .7, dur: .3, width: .9, stroke: LINE_DIM });
    }
    s += line(x, y, x - 13, y - 10, { delay: .74, dur: .4, width: 1.6 });
    s += line(x, y, x + 4, y - 23, { delay: .76, dur: .4, width: 1.2 });
    s += `<circle cx="${x}" cy="${y}" r="2.6" fill="${WARM}" class="soft"/>`;
    return s;
  }

  function backBar() {
    let s = '';
    const x = 982, y = 168, w = 578, h = 400;

    s += solid(`M${x} ${y}H${x + w}V${y + h}H${x}Z`, '#0c0f16', { opacity: .95 });
    s += ink(`M${x} ${y}H${x + w}V${y + h}H${x}Z`, { delay: .34, dur: .95, width: 1.6 });

    // 镜面
    for (const [mx, mw] of [[1008, 238], [1272, 264]]) {
      const md = `M${mx} ${y + 262}V${y + 54}Q${mx + mw / 2} ${y + 20} ${mx + mw} ${y + 54}V${y + 262}Z`;
      s += solid(md, '#101a28', { opacity: .96 });
      s += ink(md, { delay: .44, dur: .85, width: 1.4 });
      s += ink(`M${mx + 10} ${y + 254}V${y + 62}Q${mx + mw / 2} ${y + 34} ${mx + mw - 10} ${y + 62}V${y + 254}Z`,
        { delay: .52, dur: .75, width: .9, stroke: LINE_DIM });
      s += line(mx + 22, y + 232, mx + 22, y + 80, { delay: .58, dur: .7, width: .9, stroke: '#415a7a' });
      s += line(mx + mw - 24, y + 244, mx + mw - 24, y + 96, { delay: .6, dur: .7, width: .8, stroke: '#415a7a' });
    }

    // 中央镜面里的 CAFE 反字
    s += `<text class="ink" x="1204" y="${y + 140}" text-anchor="middle" font-family="Georgia, serif"
      font-size="42" letter-spacing="9" fill="none" stroke="${WARM}" stroke-width="1.2" opacity=".8"
      style="--delay:1.05s;--dur:.7s">CAFE</text>`;

    // 层板
    for (const [sy, d] of [[y + 288, .58], [y + 352, .62]]) {
      s += line(x + 14, sy, x + w - 14, sy, { delay: d, dur: .75, width: 1.4 });
      s += line(x + 14, sy + 5, x + w - 14, sy + 5, { delay: d + .04, dur: .7, width: .8, stroke: LINE_DIM });
    }

    // 杯碟与少量瓶子，不铺满
    for (let i = 0; i < 7; i++) {
      s += cup(1024 + i * 76, y + 288, .72, { delay: .66 + i * .03 });
    }
    for (const [bx, bh, bw] of [[1038, 46, 12], [1092, 58, 14], [1302, 40, 11], [1356, 54, 13], [1416, 44, 12], [1480, 60, 14], [1528, 42, 11]]) {
      const d = `M${bx - bw / 2} ${y + 352}V${y + 352 - bh + 12}Q${bx - bw / 2} ${y + 352 - bh + 4} ${bx - bw / 4} ${y + 352 - bh + 2}V${y + 352 - bh}H${bx + bw / 4}V${y + 352 - bh + 2}Q${bx + bw / 2} ${y + 352 - bh + 4} ${bx + bw / 2} ${y + 352 - bh + 12}V${y + 352}Z`;
      s += solid(d, '#121722', { opacity: .9 });
      s += ink(d, { delay: .72, dur: .55, width: 1, stroke: LINE_DIM });
    }

    // 柜台后下方的柜门
    for (const cx of [1004, 1200, 1396]) {
      s += box(cx, y + 302, 160, 76, { delay: .8, dur: .65, width: 1, stroke: LINE_DIM });
      s += `<circle cx="${cx + 80}" cy="${y + 342}" r="2.4" fill="#b08a4c" class="soft"/>`;
    }

    return s;
  }

  function counter() {
    let s = '';
    const x0 = 900, x1 = 1600, top = 586;

    // 柜台立面
    const front = `M${x0} ${top}H${x1}V790H${x0 + 14}Z`;
    s += solid(front, '#100e0c', { opacity: .99 });
    s += ink(front, { delay: .72, dur: .95, width: 1.7 });
    // 台面
    s += solid(`M${x0 - 16} ${top - 22}H${x1}V${top + 6}H${x0}Z`, '#191410', { opacity: 1 });
    s += ink(`M${x0 - 16} ${top - 22}H${x1}V${top + 6}H${x0}Z`, { delay: .66, dur: .85, width: 1.6 });
    s += line(x0 - 4, top - 18, x1 - 6, top - 18, { delay: .78, dur: .8, width: 1.2, stroke: WARM });

    // 立面竖纹与黄铜脚踏
    for (let x = x0 + 34; x < x1; x += 42) {
      s += line(x, top + 18, x - 3, 780, { delay: .84 + (x - x0) / 3800, dur: .65, width: .75, stroke: LINE_DEEP });
    }
    s += line(x0 + 8, 744, x1, 744, { delay: .96, dur: .8, width: 2, stroke: '#a8844d' });
    s += line(x0 + 14, 790, x1, 790, { delay: .98, dur: .8, width: 1.6 });

    // 派柜
    s += solidRect(x0 + 62, top - 88, 122, 88, '#0b1018', { opacity: .9 });
    s += ink(`M${x0 + 62} ${top - 88}H${x0 + 184}V${top}H${x0 + 62}Z`, { delay: .88, dur: .8, width: 1.4 });
    s += `<path class="solid" d="M${x0 + 74} ${top - 96}Q${x0 + 122} ${top - 140} ${x0 + 170} ${top - 96}Z" fill="#16202e" opacity=".85"/>`;
    s += ink(`M${x0 + 74} ${top - 96}Q${x0 + 122} ${top - 140} ${x0 + 170} ${top - 96}`, { delay: .94, dur: .7, width: 1.2, stroke: LINE_DIM });
    s += line(x0 + 82, top - 78, x0 + 162, top - 78, { delay: .96, dur: .5, width: .8, stroke: LINE_DIM });
    // 派
    s += `<ellipse cx="${x0 + 122}" cy="${top - 26}" rx="46" ry="11" fill="#3a2416" stroke="#c9a45c" stroke-width="1.1" class="soft"/>`;
    for (let i = -2; i <= 2; i++) {
      s += line(x0 + 122 + i * 16, top - 33, x0 + 122 + i * 16, top - 18, { delay: 1, dur: .3, width: .8, stroke: '#c9a45c' });
    }

    // 收银机
    s += poly([[1316, top - 4], [1334, top - 58], [1420, top - 58], [1438, top - 4]], true,
      { delay: .94, dur: .75, width: 1.5, stroke: WARM });
    s += solidRect(1334, top - 58, 86, 20, '#17120d', { opacity: 1 });
    s += box(1352, top - 42, 50, 16, { delay: 1, dur: .5, width: .9, stroke: LINE_DIM });
    for (let x = 1344; x <= 1414; x += 10) {
      s += line(x, top - 20, x, top - 12, { delay: 1.02, dur: .25, width: .7, stroke: LINE_DIM });
    }
    s += line(1320, top - 6, 1434, top - 6, { delay: 1.04, dur: .4, width: 1.1, stroke: WARM });

    // 咖啡壶
    s += `<g transform="translate(1510 ${top})">
      <ellipse cx="0" cy="0" rx="34" ry="6" fill="#0a0d13" stroke="#7f90ad" stroke-width="1.1" class="soft"/>
      <path d="M-22 -2V-58Q-22 -70 0 -70Q22 -70 22 -58V-2Z" fill="#141a24" stroke="#c3d0e4" stroke-width="1.4"/>
      <path d="M-18 -48H18M-18 -34H18" stroke="#8f6a3d" stroke-width="1.2"/>
      <path d="M22 -50Q38 -50 34 -30" fill="none" stroke="#c3d0e4" stroke-width="1.2"/>
      <path d="M-8 -70V-80H8V-70" fill="none" stroke="#c3d0e4" stroke-width="1.2"/>
      <circle cx="0" cy="-84" r="2.4" fill="#d8b56a"/>
    </g>`;

    // 一摞碟子与零钱
    s += cup(x0 + 296, top - 6, .85, { delay: 1 });
    s += cup(x0 + 322, top - 12, .85, { delay: 1.04 });
    s += `<g id="coins">
      <ellipse cx="${x0 + 424}" cy="${top - 6}" rx="15" ry="4" fill="#c9a45c" opacity=".8"/>
      <ellipse cx="${x0 + 424}" cy="${top - 10}" rx="15" ry="4" fill="#e0bd78" opacity=".85"/>
      <ellipse cx="${x0 + 440}" cy="${top - 4}" rx="9" ry="3" fill="#c9a45c" opacity=".8"/>
      <ellipse cx="${x0 + 408}" cy="${top - 3}" rx="8" ry="2.6" fill="#c9a45c" opacity=".7"/>
    </g>`;

    return s;
  }

  function tableSet() {
    let s = '';
    // 中间圆桌
    s += `<ellipse cx="638" cy="732" rx="128" ry="30" fill="#0a0c12" stroke="#5c6d8c" stroke-width="1.4" class="soft"/>`;
    s += ink('M510 732Q638 690 766 732', { delay: .9, dur: .8, width: 1.5 });
    s += line(540, 748, 522, 848, { delay: .96, dur: .6, width: 1.3, stroke: LINE_DIM });
    s += line(736, 748, 754, 848, { delay: .98, dur: .6, width: 1.3, stroke: LINE_DIM });
    s += line(638, 760, 638, 858, { delay: 1, dur: .55, width: 1.1, stroke: LINE_DEEP });
    s += line(576, 716, 700, 716, { delay: 1.02, dur: .5, width: .9, stroke: LINE_DEEP });
    s += cup(596, 706, .9, { delay: 1.04, steam: true });
    s += cup(684, 708, .85, { delay: 1.06 });

    // 椅子：只画一把，别把画面塞满
    s += `<g transform="translate(800 856) scale(-1 1)">
      ${line(-18, -96, -18, 0, { delay: 1.08, dur: .6, width: 1.2, stroke: LINE_DIM })}
      ${line(-18, -96, 16, -96, { delay: 1.1, dur: .45, width: 1.2, stroke: LINE_DIM })}
      ${line(16, -96, 16, -48, { delay: 1.12, dur: .5, width: 1.2, stroke: LINE_DIM })}
      ${line(16, -48, -26, -48, { delay: 1.14, dur: .45, width: 1.1, stroke: LINE_DIM })}
      ${line(-22, -48, -22, 0, { delay: 1.16, dur: .45, width: 1, stroke: LINE_DIM })}
      ${line(12, -48, 12, 0, { delay: 1.18, dur: .45, width: 1, stroke: LINE_DIM })}
    </g>`;

    // 前景桌角：压住画面下缘，给柜台留出纵深
    s += `<ellipse cx="204" cy="888" rx="196" ry="44" fill="#080a0f" stroke="#465874" stroke-width="1.3" class="soft"/>`;
    s += ink('M8 888Q204 826 400 888', { delay: 1.02, dur: .9, width: 1.4 });
    s += cup(146, 858, 1.15, { delay: 1.12, steam: true });
    s += cup(256, 864, 1.1, { delay: 1.14 });
    s += line(84, 900, 84, 862, { delay: 1.16, dur: .4, width: 1.1, stroke: LINE_DIM });

    return s;
  }

  function lamps() {
    let s = '';
    for (const [x, y, d] of [[426, 152, .58], [1102, 128, .64]]) {
      const len = y + 34;
      s += line(x, 62, x, len, { delay: d, dur: .6, width: 1.2, stroke: LINE_DIM });
      s += poly([[x - 4, len], [x - 38, len + 36], [x + 38, len + 36], [x + 4, len]], true,
        { delay: d + .06, dur: .7, width: 1.4, stroke: WARM });
      s += line(x - 38, len + 36, x + 38, len + 36, { delay: d + .1, dur: .4, width: 1.2, stroke: WARM });
      s += `<ellipse cx="${x}" cy="${len + 42}" rx="76" ry="18" fill="url(#lampGlow)" opacity=".5" class="soft"/>`;
      s += `<circle cx="${x}" cy="${len + 40}" r="4" fill="#ffd88e" opacity=".9" class="soft"/>`;
    }
    return s;
  }

  function coatRack() {
    let s = '';
    const x = 56;
    s += line(x, 640, x, 220, { delay: .86, dur: .7, width: 1.6, stroke: LINE_DIM });
    s += line(x - 38, 640, x + 38, 640, { delay: .9, dur: .5, width: 1.5, stroke: LINE_DIM });
    s += line(x, 242, x - 30, 286, { delay: .94, dur: .45, width: 1.2, stroke: LINE_DIM });
    s += line(x, 242, x + 30, 286, { delay: .96, dur: .45, width: 1.2, stroke: LINE_DIM });
    // 挂着的围裙和帽子
    s += `<path class="solid" d="M${x + 30} 286 L${x + 64} 306 L${x + 58} 430 L${x + 36} 430 L${x + 30} 340Z"
      fill="#3a2020" opacity=".9"/>`;
    s += `<path d="M${x + 30} 286 L${x + 64} 306 L${x + 58} 430 L${x + 36} 430 L${x + 30} 340Z"
      fill="none" stroke="#7e4c4c" stroke-width="1.1" class="soft"/>`;
    s += `<ellipse cx="${x - 30}" cy="292" rx="28" ry="7" fill="#1e1a17" stroke="#5c5041" stroke-width="1" class="soft"/>`;
    s += `<path d="M${x - 50} 290Q${x - 48} 270 ${x - 30} 270Q${x - 12} 270 ${x - 10} 290" fill="#1e1a17" stroke="#5c5041" stroke-width="1" class="soft"/>`;
    return s;
  }

  function gradients() {
    return `
      <radialGradient id="lampGlow" cx="50%" cy="45%" r="50%">
        <stop offset="0" stop-color="#f4c97c" stop-opacity=".62"/>
        <stop offset=".55" stop-color="#f4c97c" stop-opacity=".18"/>
        <stop offset="1" stop-color="#f4c97c" stop-opacity="0"/>
      </radialGradient>
      <radialGradient id="streetGlow" cx="50%" cy="50%" r="50%">
        <stop offset="0" stop-color="#8fb3d8" stop-opacity=".42"/>
        <stop offset="1" stop-color="#8fb3d8" stop-opacity="0"/>
      </radialGradient>
      <radialGradient id="floorGlow" cx="50%" cy="50%" r="50%">
        <stop offset="0" stop-color="#f4c97c" stop-opacity=".18"/>
        <stop offset="1" stop-color="#f4c97c" stop-opacity="0"/>
      </radialGradient>
      <radialGradient id="dimGrad" cx="66%" cy="46%" r="70%">
        <stop offset="0" stop-color="#020409" stop-opacity="0"/>
        <stop offset=".46" stop-color="#020409" stop-opacity="0"/>
        <stop offset=".72" stop-color="#020409" stop-opacity=".38"/>
        <stop offset="1" stop-color="#020409" stop-opacity=".76"/>
      </radialGradient>`;
  }

  function defs() {
    return `<defs>
      <filter id="neonGlow" x="-60%" y="-60%" width="220%" height="220%">
        <feGaussianBlur stdDeviation="3" result="b"/>
        <feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge>
      </filter>
      ${gradients()}
    </defs>`;
  }

  function lightPools() {
    return `<g id="lightPools">
      <ellipse cx="1102" cy="560" rx="260" ry="120" fill="url(#floorGlow)"/>
      <ellipse cx="426" cy="700" rx="220" ry="110" fill="url(#floorGlow)"/>
      <ellipse cx="638" cy="700" rx="150" ry="90" fill="url(#lampGlow)" opacity=".2"/>
    </g>`;
  }

  function cupFly() {
    return `<g id="cupFly" opacity="0">
      <ellipse cx="0" cy="0" rx="13" ry="3.6" fill="none" stroke="#cfdcee" stroke-width="1.2"/>
      <path d="M-10 -1L-7 20H7L10 -1" fill="#0b0f18" fill-opacity=".55" stroke="#d4e0f0" stroke-width="1.3"/>
      <ellipse cx="0" cy="6" rx="7" ry="2" fill="#7a4d24" opacity=".8"/>
      <path d="M10 2Q16 4 15 9Q14 14 9 14" fill="none" stroke="#cfdcee" stroke-width="1.1"/>
    </g>`;
  }

  function build() {
    return `${defs()}
      <g id="setBack">${backWall()}</g>
      ${lightPools()}
      <g id="setFront">
        ${windowSet()}
        ${doorSet()}
        ${wallClock()}
        ${coatRack()}
        ${backBar()}
        ${counter()}
        ${tableSet()}
        ${lamps()}
      </g>
      <g id="cupFly-wrap">${cupFly()}</g>
      <g id="castLu"></g>
      <g id="castNl"></g>
      <g id="houseDim-wrap"><rect id="houseDim" x="0" y="0" width="1600" height="900" fill="url(#dimGrad)"/></g>
      <g id="fg"></g>`;
  }

  window.Art = { build };
})();
