/* 线绘舞台：酒馆、吧台、小舞台。
   所有线条带 pathLength=1，由 CSS 逐笔绘制；填色在 .built 后淡入。
   这里只描述空间和道具，不描述表演。 */
(function () {
  const LINE = '#c6d6f2';
  const LINE_DIM = '#7187ac';
  const LINE_DEEP = '#3c4b67';
  const WARM = '#e3c58f';

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

  const box = (x, y, w, h, o) =>
    ink(`M${x} ${y}H${x + w}V${y + h}H${x}Z`, o);

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

  const bottle = (x, base, h, w, o = {}) => {
    const neck = Math.max(4, w * 0.34);
    const shoulder = h * 0.3;
    const d = `M${x - w / 2} ${base}
      L${x - w / 2} ${base - h + shoulder}
      Q${x - w / 2} ${base - h + shoulder * .38} ${x - neck / 2} ${base - h + shoulder * .18}
      L${x - neck / 2} ${base - h}
      L${x + neck / 2} ${base - h}
      L${x + neck / 2} ${base - h + shoulder * .18}
      Q${x + w / 2} ${base - h + shoulder * .38} ${x + w / 2} ${base - h + shoulder}
      L${x + w / 2} ${base}Z`;
    const delay = o.delay || 0;
    const fill = o.fill || '#101522';
    return solid(d, fill, { opacity: .9 }) +
      ink(d, { delay, dur: .7, width: o.width || 1.25, stroke: o.stroke || LINE_DIM }) +
      line(x - w / 2 + 3, base - h * .45, x + w / 2 - 3, base - h * .45,
        { delay: delay + .08, dur: .6, width: 1, stroke: '#8b6f42' }) +
      (o.label === false ? '' : line(x - w / 2 + 3, base - h * .52, x + w / 2 - 3, base - h * .52,
        { delay: delay + .1, dur: .6, width: .9, stroke: '#b08a4c' }));
  };

  const stemGlass = (x, y, o = {}) => {
    const delay = o.delay || 0;
    return ink(`M${x - 7} ${y} Q${x} ${y + 15} ${x + 7} ${y} M${x} ${y + 8}V${y + 24} M${x - 8} ${y + 25}H${x + 8}`,
      { delay, dur: .55, width: 1.1, stroke: LINE_DIM });
  };

  const sconce = (x, y, delay) => {
    return `
      <g>
        <ellipse cx="${x}" cy="${y + 8}" rx="58" ry="74" fill="url(#warmGlow)" opacity=".5"/>
        ${poly([[x, y - 22], [x - 26, y + 18], [x - 8, y + 18], [x, y + 4], [x + 8, y + 18], [x + 26, y + 18]], true,
          { delay, dur: .8, width: 1.3, stroke: WARM })}
        ${poly([[x - 32, y + 23], [x + 32, y + 23]], false, { delay: delay + .1, dur: .5, width: 1.2, stroke: WARM })}
        ${line(x, y - 30, x, y + 28, { delay: delay + .12, dur: .5, width: 1.1, stroke: WARM })}
        <circle class="solid" cx="${x}" cy="${y + 26}" r="3.2" fill="#ffd88e" opacity=".9"/>
      </g>`;
  };

  const chair = (x, y, flip, delay) => {
    const f = flip ? -1 : 1;
    return `
      <g transform="translate(${x} ${y}) scale(${f} 1)">
        ${line(-18, -92, -18, 0, { delay, dur: .7, width: 1.3, stroke: LINE_DIM })}
        ${line(-18, -92, 16, -92, { delay: delay + .06, dur: .5, width: 1.3, stroke: LINE_DIM })}
        ${line(16, -92, 16, -46, { delay: delay + .1, dur: .6, width: 1.3, stroke: LINE_DIM })}
        ${line(16, -46, -26, -46, { delay: delay + .14, dur: .5, width: 1.2, stroke: LINE_DIM })}
        ${line(-22, -46, -22, 0, { delay: delay + .18, dur: .5, width: 1.1, stroke: LINE_DIM })}
        ${line(12, -46, 12, 0, { delay: delay + .2, dur: .5, width: 1.1, stroke: LINE_DIM })}
        ${poly([[-26, -106], [-18, -92], [-18, -66], [-30, -70]], true, { delay: delay + .22, dur: .5, width: 1, stroke: LINE_DIM })}
      </g>`;
  };

  function backWall() {
    let s = '';
    // 墙与地面
    s += solidRect(0, 0, 1600, 900, '#05070c');
    s += solidRect(0, 80, 1600, 620, '#080b12');
    s += solidRect(0, 700, 1600, 200, '#07090f');

    // 顶线、墙裙
    s += line(0, 80, 1600, 80, { delay: .02, dur: 1.1, width: 2.1 });
    s += line(0, 96, 1600, 96, { delay: .06, dur: 1.1, width: 1.1, stroke: LINE_DIM });
    s += line(0, 610, 1600, 610, { delay: .2, dur: 1.1, width: 1.5 });
    s += line(0, 646, 1600, 646, { delay: .24, dur: 1.1, width: 1.1, stroke: LINE_DIM });
    s += line(0, 700, 1600, 700, { delay: .28, dur: 1.1, width: 1.6 });

    // 墙面竖向分格
    for (let x = 40; x < 1600; x += 122) {
      s += line(x, 98, x, 610, { delay: .18 + (x / 1600) * .3, dur: .8, width: 1, stroke: LINE_DEEP });
      s += line(x + 18, 654, x + 18, 700, { delay: .22 + (x / 1600) * .2, dur: .5, width: 1, stroke: LINE_DEEP });
    }
    // 墙裙压线
    for (let x = 30; x < 1600; x += 122) {
      s += line(x, 666, x + 56, 666, { delay: .3, dur: .6, width: 1, stroke: LINE_DEEP });
    }

    // 左侧高窗（雨夜街区只剩蓝灰反光）
    for (const [x, w] of [[36, 128], [184, 128]]) {
      const y = 150;
      const h = 300;
      const d = `M${x} ${y + h}V${y + 36}Q${x + w / 2} ${y - 22} ${x + w} ${y + 36}V${y + h}Z`;
      s += solid(d, '#07111c', { opacity: .95 });
      s += ink(d, { delay: .12 + x / 1500, dur: 1, width: 1.6 });
      s += line(x + w / 2, y + 8, x + w / 2, y + h, { delay: .28, dur: .8, width: 1.1, stroke: LINE_DIM });
      s += line(x, y + 120, x + w, y + 120, { delay: .32, dur: .7, width: 1, stroke: LINE_DIM });
      s += line(x, y + 222, x + w, y + 222, { delay: .34, dur: .7, width: 1, stroke: LINE_DIM });
      // 雨痕
      for (let i = 1; i < 7; i++) {
        const rx = x + 14 + i * 16;
        s += line(rx, y + 30 + (i % 3) * 22, rx - 10, y + 150 + (i % 4) * 26,
          { delay: .42, dur: .7, width: .8, stroke: '#405a7d' });
      }
    }

    // 霓虹 BAR
    s += `<g class="neon-flicker">`;
    s += `<text class="ink" x="520" y="176" text-anchor="middle" font-family="Arial Narrow, Helvetica, sans-serif"
      font-size="54" font-weight="700" letter-spacing="12" fill="none" stroke="#e9a963" stroke-width="2.2"
      style="--delay:1.15s;--dur:.8s;filter:url(#neonGlow)">BAR</text>`;
    s += line(452, 196, 588, 196, { delay: 1.28, dur: .8, width: 1, stroke: '#a86b3e' });
    s += `</g>`;

    // 壁灯
    s += sconce(300, 300, .5);
    s += sconce(752, 300, .56);

    // 右墙：后台侧门
    s += box(1548, 300, 40, 360, { delay: .44, dur: .8, width: 1.3, stroke: LINE_DIM });
    s += line(1548, 350, 1588, 350, { delay: .5, dur: .4, width: 1, stroke: LINE_DEEP });
    s += line(1548, 610, 1588, 610, { delay: .52, dur: .4, width: 1, stroke: LINE_DEEP });
    s += `<circle cx="1578" cy="486" r="2.6" fill="#b08a4c" class="soft"/>`;

    return s;
  }

  function barBack() {
    let s = '';
    // 吧台后柜体
    const d = `M320 240H720V590H320Z`;
    s += solid(d, '#0b0e16', { opacity: .92 });
    s += ink(d, { delay: .42, dur: .95, width: 1.5 });

    // 镜面两联
    const mirror = (x, w) => {
      const dd = `M${x} 500V292Q${x + w / 2} 242 ${x + w} 292V500Z`;
      s += solid(dd, '#0d1624', { opacity: .95 });
      s += ink(dd, { delay: .52, dur: .9, width: 1.5 });
      s += ink(`M${x + 10} 492V298Q${x + w / 2} 258 ${x + w - 10} 298V492Z`,
        { delay: .62, dur: .8, width: 1, stroke: LINE_DIM });
      // 镜面反光
      s += line(x + 22, 468, x + 22, 310, { delay: .7, dur: .8, width: .9, stroke: '#46648b' });
      s += line(x + w - 26, 480, x + w - 26, 322, { delay: .74, dur: .8, width: .9, stroke: '#46648b' });
    };
    mirror(344, 158);
    mirror(520, 178);

    // 层板
    for (const [y, delay] of [[324, .58], [406, .62], [488, .66]]) {
      s += line(330, y, 710, y, { delay, dur: .8, width: 1.4 });
      s += line(330, y + 5, 710, y + 5, { delay: delay + .04, dur: .8, width: .8, stroke: LINE_DIM });
      for (const x of [352, 430, 516, 604, 682]) {
        s += line(x, y + 6, x, y + 16, { delay: delay + .1, dur: .35, width: .9, stroke: LINE_DEEP });
      }
    }

    // 倒挂酒杯架
    s += line(360, 278, 690, 278, { delay: .7, dur: .7, width: 1.1, stroke: WARM });
    for (let x = 376; x <= 676; x += 25) {
      s += stemGlass(x, 282, { delay: .74 + (x - 376) / 3000 });
    }

    // 酒瓶
    const shelfA = [
      [352, 324, 48, 12], [374, 324, 37, 10], [396, 324, 54, 13], [420, 324, 43, 11],
      [444, 324, 60, 14], [470, 324, 39, 10], [493, 324, 50, 12], [518, 324, 44, 11],
      [544, 324, 57, 13], [570, 324, 36, 10], [593, 324, 49, 12], [618, 324, 42, 11],
      [644, 324, 55, 13], [670, 324, 38, 10], [692, 324, 47, 12],
    ];
    const shelfB = [
      [352, 406, 35, 11], [375, 406, 42, 12], [399, 406, 30, 10], [422, 406, 50, 14],
      [448, 406, 37, 11], [472, 406, 45, 12], [497, 406, 32, 10], [520, 406, 52, 14],
      [547, 406, 39, 11], [571, 406, 46, 12], [596, 406, 34, 10], [620, 406, 49, 13],
      [646, 406, 36, 10], [670, 406, 44, 12], [695, 406, 31, 9],
    ];
    for (const b of shelfA) s += bottle(b[0], b[1], b[2], b[3], { delay: .68 + (b[0] - 350) / 1800 });
    for (const b of shelfB) s += bottle(b[0], b[1], b[2], b[3], { delay: .74 + (b[0] - 350) / 1800 });

    // 柜门与抽屉
    for (const x of [344, 536]) {
      s += box(x, 512, 164, 64, { delay: .78, dur: .75, width: 1.1, stroke: LINE_DIM });
      s += line(x + 20, 528, x + 144, 528, { delay: .84, dur: .6, width: .9, stroke: LINE_DEEP });
      s += `<circle cx="${x + 82}" cy="560" r="2.5" fill="#b08a4c" class="soft"/>`;
    }

    return s;
  }

  function barCounter() {
    let s = '';
    // 柜台立面
    const front = `M120 610H780L762 790H138Z`;
    s += solid(front, '#0f0d0d', { opacity: .98 });
    s += ink(front, { delay: .72, dur: .95, width: 1.6 });
    s += ink(`M102 586H792L780 610H114Z`, { delay: .68, dur: .8, width: 1.6 });
    s += solid(`M102 586H792L780 610H114Z`, '#17120f', { opacity: 1 });
    s += line(108, 604, 786, 604, { delay: .8, dur: .8, width: 1.1, stroke: WARM });

    // 竖纹与踢脚
    for (let x = 168; x <= 740; x += 43) {
      s += line(x, 618, x - 4, 778, { delay: .84 + (x - 168) / 2400, dur: .7, width: .8, stroke: LINE_DEEP });
    }
    s += line(146, 758, 756, 758, { delay: .94, dur: .8, width: 1.1, stroke: LINE_DIM });
    s += line(138, 790, 762, 790, { delay: .96, dur: .8, width: 1.6 });

    // 黄铜脚踏
    s += line(126, 744, 772, 744, { delay: 1.02, dur: .8, width: 2, stroke: '#a8844d' });
    for (const x of [210, 440, 690]) {
      s += line(x, 744, x, 776, { delay: 1.06, dur: .4, width: 1.2, stroke: '#8a6a3b' });
    }

    // 酒龙头
    s += box(292, 542, 18, 44, { delay: .92, dur: .6, width: 1.2, stroke: WARM });
    s += line(301, 542, 301, 520, { delay: .96, dur: .4, width: 1.4, stroke: WARM });
    s += line(295, 526, 307, 526, { delay: .98, dur: .3, width: 1.2, stroke: WARM });
    s += line(283, 548, 292, 558, { delay: 1, dur: .3, width: 1.1, stroke: WARM });
    s += line(319, 548, 310, 558, { delay: 1.02, dur: .3, width: 1.1, stroke: WARM });

    // 收银机
    s += poly([[196, 584], [220, 552], [268, 552], [282, 584]], true,
      { delay: .98, dur: .7, width: 1.3, stroke: WARM });
    s += line(216, 566, 270, 566, { delay: 1.02, dur: .4, width: .9, stroke: LINE_DIM });
    for (let x = 222; x <= 262; x += 10) {
      s += line(x, 574, x, 580, { delay: 1.04, dur: .3, width: .8, stroke: LINE_DIM });
    }

    // 抹布
    s += poly([[466, 588], [524, 583], [531, 601], [474, 606]], true,
      { delay: 1.04, dur: .5, width: 1.4, stroke: '#b9c5d9' });
    s += line(480, 593, 506, 590, { delay: 1.08, dur: .3, width: 1, stroke: '#cfd9e8' });
    s += line(486, 597, 516, 595, { delay: 1.1, dur: .3, width: .9, stroke: '#8d98ae' });

    // 空杯（尼尔面前）
    s += `<g id="glass" transform="translate(650 552) scale(1.18)">
      <ellipse cx="0" cy="0" rx="14" ry="4" fill="none" stroke="#c3d1e8" stroke-width="1.4" opacity=".95"/>
      <path d="M-11 -2 L-8 26 H8 L11 -2" fill="#0c111a" fill-opacity=".55" stroke="#d3dff1" stroke-width="1.6"/>
      <path d="M-9 12 H9 L8 25 H-8Z" fill="#8a5626" opacity=".85"/>
      <ellipse cx="0" cy="12" rx="9" ry="2.4" fill="#c98c45" opacity=".9"/>
      <path d="M-8 -2 Q0 3 8 -2" fill="none" stroke="#f2f7ff" stroke-width="1.1" opacity=".8"/>
    </g>`;

    // 烟灰缸
    s += `<g transform="translate(748 582)">
      <ellipse cx="0" cy="0" rx="15" ry="4.5" fill="#0a0c12" stroke="#7f90ad" stroke-width="1.1" class="soft"/>
      <ellipse cx="0" cy="-1" rx="9" ry="2.4" fill="#11151f" stroke="#61718a" stroke-width=".9" class="soft"/>
    </g>`;

    return s;
  }

  function stage() {
    let s = '';
    // 台口立柱
    for (const x of [812, 1512]) {
      s += box(x, 122, 34, 578, { delay: .48, dur: 1, width: 1.5 });
      s += box(x - 7, 122, 48, 24, { delay: .5, dur: .8, width: 1.4, stroke: WARM });
      s += box(x - 3, 146, 40, 12, { delay: .54, dur: .7, width: 1.1, stroke: LINE_DIM });
      for (let y = 180; y <= 660; y += 58) {
        s += line(x + 8, y, x + 8, y + 34, { delay: .6 + (y - 180) / 1800, dur: .45, width: .9, stroke: LINE_DEEP });
        s += line(x + 25, y, x + 25, y + 34, { delay: .62 + (y - 180) / 1800, dur: .45, width: .9, stroke: LINE_DEEP });
      }
    }
    // 台口横梁
    s += box(812, 100, 734, 34, { delay: .42, dur: 1, width: 1.6 });
    s += line(836, 114, 1522, 114, { delay: .5, dur: .9, width: 1, stroke: LINE_DIM });
    for (let x = 860; x < 1520; x += 64) {
      s += line(x, 100, x, 134, { delay: .55 + (x - 860) / 2200, dur: .4, width: .9, stroke: LINE_DEEP });
    }

    // 幕布
    const cd = `M850 700V276Q850 166 946 166H1412Q1508 166 1508 276V700Z`;
    s += solid(cd, '#240d14', { opacity: .98 });
    s += ink(cd, { delay: .56, dur: 1.15, width: 1.8 });
    // 幕布褶
    for (let i = 0; i < 17; i++) {
      const x = 870 + i * 38;
      const bend = (i % 3) * 6;
      s += ink(`M${x} 190 Q${x - 10 + bend} 360 ${x + 2} 530 Q${x + 8} 620 ${x - 4} 700`,
        { delay: .68 + i * .018, dur: 1, width: 1, stroke: i % 2 ? '#4a2029' : '#5a2a32' });
    }
    // 帷幕檐
    let val = `M846 190H1512V236`;
    for (let x = 1512; x > 846; x -= 48) {
      val += `Q${x - 24} 258 ${x - 48} 236`;
    }
    val += 'Z';
    s += solid(val, '#310f18', { opacity: .98 });
    s += ink(val, { delay: .6, dur: 1, width: 1.6, stroke: WARM });
    for (let x = 870; x < 1510; x += 48) {
      s += line(x, 196, x, 238, { delay: .72 + (x - 870) / 2600, dur: .45, width: .9, stroke: '#6a343c' });
    }

    // 台面
    const pf = `M820 700H1534V770H820Z`;
    s += solid(pf, '#08090d', { opacity: .98 });
    s += line(820, 700, 1534, 700, { delay: .78, dur: 1, width: 2.1 });
    s += line(820, 770, 1534, 770, { delay: .82, dur: 1, width: 1.7 });
    s += line(820, 700, 820, 770, { delay: .8, dur: .5, width: 1.2 });
    s += line(1534, 700, 1534, 770, { delay: .8, dur: .5, width: 1.2 });
    // 台面装饰
    for (let x = 890; x < 1520; x += 72) {
      s += poly([[x, 734], [x + 24, 714], [x + 48, 734], [x + 24, 754]], false,
        { delay: .88 + (x - 890) / 3000, dur: .55, width: .9, stroke: LINE_DEEP });
    }
    // 台口脚灯
    for (let x = 900; x <= 1450; x += 110) {
      s += `<ellipse cx="${x}" cy="694" rx="10" ry="3.2" fill="#f2c36e" opacity=".6" class="soft"/>`;
    }

    // 钢琴（左后）
    const p = `M884 700V542H1018V700Z`;
    s += solid(p, '#07080c', { opacity: .98 });
    s += ink(p, { delay: .74, dur: .9, width: 1.5 });
    s += line(884, 618, 1018, 618, { delay: .82, dur: .6, width: 1, stroke: LINE_DIM });
    s += box(896, 632, 110, 20, { delay: .86, dur: .6, width: 1, stroke: LINE_DIM });
    for (let x = 902; x <= 1000; x += 7) {
      s += line(x, 638, x, 650, { delay: .9, dur: .25, width: .7, stroke: LINE_DEEP });
    }
    s += line(908, 542, 908, 508, { delay: .84, dur: .4, width: 1.1, stroke: LINE_DIM });
    s += line(994, 542, 994, 508, { delay: .86, dur: .4, width: 1.1, stroke: LINE_DIM });
    s += poly([[900, 508], [1002, 508], [988, 482], [914, 482]], true,
      { delay: .88, dur: .55, width: 1, stroke: LINE_DIM });
    s += line(916, 490, 984, 490, { delay: .92, dur: .4, width: .8, stroke: LINE_DEEP });

    // 钢琴手剪影
    s += `<g opacity=".9">
      <ellipse cx="930" cy="700" rx="34" ry="5" fill="#000" opacity=".55"/>
      <path d="M914 700V646Q914 620 934 618Q956 616 958 642V700Z" fill="#0a0b10" stroke="#3e4c66" stroke-width="1.1"/>
      <circle cx="938" cy="596" r="15" fill="#0a0b10" stroke="#4b5b78" stroke-width="1.1"/>
      <path d="M924 596Q936 578 952 592" fill="none" stroke="#4b5b78" stroke-width="1"/>
      <path d="M922 650 Q900 646 898 640M954 650 Q980 646 986 638" fill="none" stroke="#4b5b78" stroke-width="1.2"/>
    </g>`;
    // 琴凳
    s += line(902, 700, 902, 682, { delay: .94, dur: .3, width: 1.1, stroke: LINE_DIM });
    s += line(966, 700, 966, 682, { delay: .96, dur: .3, width: 1.1, stroke: LINE_DIM });
    s += line(894, 682, 974, 682, { delay: .98, dur: .3, width: 1.1, stroke: LINE_DIM });

    // 低音提琴与乐手（右后）
    s += `<g>
      <ellipse cx="1418" cy="700" rx="44" ry="6" fill="#000" opacity=".55"/>
      <path d="M1392 700V648Q1392 622 1416 620Q1442 622 1442 650V700Z" fill="#090a0e" stroke="#3e4c66" stroke-width="1.1"/>
      <circle cx="1418" cy="596" r="15" fill="#090a0e" stroke="#4b5b78" stroke-width="1.1"/>
      <path d="M1404 596Q1418 578 1434 592" fill="none" stroke="#4b5b78" stroke-width="1"/>
      <path d="M1388 540Q1366 520 1358 510" fill="none" stroke="#4b5b78" stroke-width="1.2"/>
      <path d="M1356 510 Q1332 506 1322 540 Q1312 580 1340 600 Q1362 614 1376 590 Q1390 562 1372 546 Q1360 534 1352 548 Q1344 564 1358 576" fill="#0a0b10" stroke="#4b5b78" stroke-width="1.2"/>
      <path d="M1432 620 Q1456 600 1450 570" fill="none" stroke="#4b5b78" stroke-width="1.1"/>
    </g>`;

    return s;
  }

  /* 麦克风单独一层，画在人物之后：杆与话筒必须在她的裙子前面，
     否则近景里会像她把一只手贴在耳边。 */
  function mic() {
    const x = 1160;
    return `<g id="mic" class="mic-set">
      <ellipse cx="${x}" cy="702" rx="42" ry="6.5" fill="none" stroke="#61718a" stroke-width="1.2"/>
      <path d="M${x} 702V398" fill="none" stroke="#b9c7df" stroke-width="2"/>
      <path d="M${x} 694 Q${x - 33} 708 ${x - 57} 702" fill="none" stroke="#61718a" stroke-width="1.2"/>
      <path d="M${x} 694 Q${x + 33} 708 ${x + 57} 701" fill="none" stroke="#61718a" stroke-width="1.2"/>
      <path d="M${x - 28} 702 L${x} 688 L${x + 28} 702" fill="none" stroke="#61718a" stroke-width="1.1"/>
      <path d="M${x - 4} 398H${x + 4}V388H${x - 4}Z" fill="#0a0d14" stroke="#c7d4e8" stroke-width="1"/>
      <g transform="translate(${x} 374)">
        <path d="M-7 -18Q-7 -25 0 -25Q7 -25 7 -18V11Q7 18 0 18Q-7 18 -7 11Z" fill="#0a0d14" stroke="#d2ddeb" stroke-width="1.5"/>
        <path d="M-5 -13H5M-5 -8H5M-5 -3H5M-5 2H5" stroke="#7f90ad" stroke-width=".8" opacity=".8"/>
        <path d="M-7 13H7" stroke="#c7d4e8" stroke-width="1.1"/>
      </g>
      <path d="M${x} 396 Q${x - 8} 430 ${x + 12} 462 Q${x + 30} 496 ${x + 18} 532 Q${x + 8} 562 ${x + 26} 592"
        fill="none" stroke="#40506b" stroke-width="1.1"/>
    </g>`;
  }

  function audience() {
    let s = '<g id="audience" class="audience">';
    // 左前桌
    s += `<g transform="translate(704 856)">
      <ellipse cx="0" cy="0" rx="112" ry="24" fill="#04060b" stroke="#2b3854" stroke-width="1.1"/>
      <path d="M-92 8V76M92 8V76" stroke="#2b3854" stroke-width="1.1" fill="none"/>
      <path d="M-16 -8V-34M22 -6V-30" stroke="#4c5a78" stroke-width="1.1" fill="none"/>
      <ellipse cx="4" cy="-34" rx="9" ry="3" fill="#4c5a78" opacity=".5"/>
      <path d="M34 -6 Q44 -24 38 -40" stroke="#61718a" stroke-width="1" fill="none"/>
    </g>`;
    s += seated(676, 806, 1, '#05070c', 0);
    s += seated(760, 814, -1, '#05070c', 1);

    // 右前桌
    s += `<g transform="translate(1452 862)">
      <ellipse cx="0" cy="0" rx="120" ry="26" fill="#04060b" stroke="#2b3854" stroke-width="1.1"/>
      <path d="M-98 8V78M98 8V78" stroke="#2b3854" stroke-width="1.1" fill="none"/>
      <path d="M-6 -10 Q2 -30 18 -28 Q34 -24 30 -8" stroke="#4c5a78" stroke-width="1.1" fill="none"/>
    </g>`;
    s += seated(1410, 812, 1, '#05070c', 2);
    s += seated(1494, 818, -1, '#05070c', 3);

    // 后排一个站着的剪影
    s += standing(1266, 848, '#04060b', 4);

    s += '</g>';
    return s;
  }

  function seated(x, y, face, fill, idx) {
    const f = face;
    return `<g class="seat" transform="translate(${x} ${y}) scale(${f} 1)">
      <path d="M-34 44V4Q-34 -30 -2 -34Q30 -30 32 4V44Z" fill="${fill}" stroke="#2f3d5b" stroke-width="1.1"/>
      <g class="head">
        <circle cx="0" cy="-58" r="25" fill="${fill}" stroke="#3c4c6c" stroke-width="1.1"/>
        <path d="M-30 -68 Q-24 -96 4 -96 Q34 -94 32 -66" fill="${fill}" stroke="#3c4c6c" stroke-width="1.1"/>
        <path d="M-28 -72 L34 -72" stroke="#3c4c6c" stroke-width="1.1" fill="none"/>
      </g>
      <path class="hand-l" d="M-22 14 Q-34 2 -30 -10" fill="none" stroke="#5a6a88" stroke-width="1.3"/>
      <path class="hand-r" d="M24 14 Q38 4 34 -8" fill="none" stroke="#5a6a88" stroke-width="1.3"/>
      <path d="M-4 44V82M24 44V82" stroke="#2f3d5b" stroke-width="1.1" fill="none"/>
      <text x="0" y="98" fill="none" stroke="none">${idx}</text>
    </g>`;
  }

  function standing(x, y, fill, idx) {
    return `<g class="seat" transform="translate(${x} ${y})">
      <path d="M-30 46V-6Q-30 -44 0 -48Q30 -44 30 -6V46Z" fill="${fill}" stroke="#2f3d5b" stroke-width="1.1"/>
      <circle cx="0" cy="-76" r="24" fill="${fill}" stroke="#3c4c6c" stroke-width="1.1"/>
      <path d="M-28 -84 Q-22 -112 4 -112 Q32 -110 30 -82" fill="${fill}" stroke="#3c4c6c" stroke-width="1.1"/>
      <path class="hand-l" d="M-24 0 Q-40 -12 -38 -26" fill="none" stroke="#5a6a88" stroke-width="1.3"/>
      <path class="hand-r" d="M24 0 Q40 -12 38 -26" fill="none" stroke="#5a6a88" stroke-width="1.3"/>
      <text x="0" y="64" fill="none" stroke="none">${idx}</text>
    </g>`;
  }

  function gradients() {
    return `
      <radialGradient id="warmGlow" cx="50%" cy="45%" r="50%">
        <stop offset="0" stop-color="#f2c36e" stop-opacity=".55"/>
        <stop offset=".55" stop-color="#f2c36e" stop-opacity=".16"/>
        <stop offset="1" stop-color="#f2c36e" stop-opacity="0"/>
      </radialGradient>
      <linearGradient id="beamGrad" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stop-color="#f2c36e" stop-opacity=".24"/>
        <stop offset=".68" stop-color="#f2c36e" stop-opacity=".06"/>
        <stop offset="1" stop-color="#f2c36e" stop-opacity="0"/>
      </linearGradient>
      <radialGradient id="poolGrad" cx="50%" cy="50%" r="50%">
        <stop offset="0" stop-color="#ffd88e" stop-opacity=".55"/>
        <stop offset=".55" stop-color="#f2c36e" stop-opacity=".2"/>
        <stop offset="1" stop-color="#f2c36e" stop-opacity="0"/>
      </radialGradient>
      <radialGradient id="dimGrad" cx="74%" cy="45%" r="66%">
        <stop offset="0" stop-color="#020409" stop-opacity="0"/>
        <stop offset=".4" stop-color="#020409" stop-opacity="0"/>
        <stop offset=".68" stop-color="#020409" stop-opacity=".45"/>
        <stop offset="1" stop-color="#020409" stop-opacity=".82"/>
      </radialGradient>`;
  }

  function barGlow() {
    return `<g id="barGlow">
      <ellipse cx="300" cy="316" rx="120" ry="120" fill="url(#warmGlow)" opacity=".7"/>
      <ellipse cx="752" cy="316" rx="120" ry="120" fill="url(#warmGlow)" opacity=".7"/>
      <ellipse cx="520" cy="176" rx="150" ry="70" fill="url(#warmGlow)" opacity=".26"/>
    </g>`;
  }

  function stageLight() {
    return `<g id="stageLight">
      <polygon points="1036,0 1326,0 1398,706 972,706" fill="url(#beamGrad)"
        filter="url(#softBlur)" style="mix-blend-mode:screen"/>
      <ellipse cx="1180" cy="694" rx="286" ry="52" fill="url(#poolGrad)"
        filter="url(#softBlur)" style="mix-blend-mode:screen"/>
      ${motes()}
    </g>`;
  }

  function motes() {
    const pts = [
      [1092, 170, .7], [1152, 250, 1.2], [1218, 330, .9], [1250, 210, .5],
      [1120, 420, 1.6], [1190, 470, 2.1], [1258, 540, 1.4], [1080, 560, 2.4],
      [1230, 620, 1.8], [1140, 610, .8], [1280, 420, 1.1], [1064, 360, 1.9],
      [1310, 300, 2.2], [1170, 570, 2.6], [1100, 300, .4], [1270, 650, 1.5],
    ];
    return pts.map(([x, y, d], i) =>
      `<circle class="mote" cx="${x}" cy="${y}" r="${1 + (i % 3) * .45}" fill="#f8dcaa" opacity="0"
        style="animation-delay:${d}s;animation-duration:${7 + (i % 4)}s"/>`).join('');
  }

  function defs() {
    return `<defs>
      <filter id="neonGlow" x="-60%" y="-60%" width="220%" height="220%">
        <feGaussianBlur stdDeviation="3.2" result="b"/>
        <feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge>
      </filter>
      <filter id="softBlur" x="-40%" y="-40%" width="180%" height="180%">
        <feGaussianBlur stdDeviation="9"/>
      </filter>
      ${gradients()}
    </defs>`;
  }

  function build() {
    return `${defs()}
      <g id="setBack">${backWall()}</g>
      ${barGlow()}
      <g id="barBack">${barBack()}</g>
      <g id="castBm"></g>
      <g id="barFront">${barCounter()}</g>
      <g id="stageSet">${stage()}</g>
      <g id="stageLight-wrap">${stageLight()}</g>
      <g id="castNy"></g>
      <g id="mic-wrap">${mic()}</g>
      <g id="castNl"></g>
      <g id="audience-wrap" opacity="0">${audience()}</g>
      <g id="houseDim-wrap"><rect id="houseDim" x="0" y="0" width="1600" height="900" fill="url(#dimGrad)"/></g>
      <g id="fg"></g>`;
  }

  window.Art = { build };
})();
