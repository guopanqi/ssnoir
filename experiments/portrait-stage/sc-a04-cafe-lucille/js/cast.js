/* 关节人偶只是“摆姿势的工具”：
   每个姿势是一次整体的关节角度/手部目标，切换时用 110ms 的硬切感收束；
   不做逐帧骨骼动画。正式游戏会把这些姿势替换成对应立绘。 */
(function () {
  const DEFS = {
    lu: {
      name: '露西',
      height: 398,
      facing: -1,
      skin: '#d8b294',
      hair: '#3a2018',
      dress: '#6d3a3a',
      dressDark: '#4e2828',
      apron: '#d9cdb4',
      shirt: '#e0d6c2',
      accent: '#c9a45c',
      lip: '#9a2f3b',
      pants: '#3a2b2b',
      shoe: '#121215',
      role: 'cafe',
    },
    nl: {
      name: '尼尔',
      height: 408,
      facing: 1,
      skin: '#c6a184',
      hair: '#2b2420',
      coat: '#4b463d',
      coatDark: '#35322c',
      shirt: '#c9c4b7',
      tie: '#2e2b2a',
      pants: '#1d1e22',
      shoe: '#090a0d',
      hat: '#28251f',
      role: 'bar',
    },
  };

  const EXPR = {
    flat: {
      bl: 'M-17 -385 Q-11 -389 -6 -386',
      br: 'M6 -386 Q11 -389 17 -385',
      mouth: 'M-8 -351 Q0 -349 8 -351',
      eye: 1,
    },
    talk: {
      bl: 'M-17 -386 Q-11 -391 -6 -387',
      br: 'M6 -387 Q11 -391 17 -386',
      mouth: 'M-7 -352 Q0 -345 7 -352 Q0 -349 -7 -352',
      eye: 1,
      open: true,
    },
    tired: {
      bl: 'M-17 -382 Q-11 -385 -6 -383',
      br: 'M6 -383 Q11 -385 17 -382',
      mouth: 'M-8 -351 Q0 -353 8 -351',
      eye: .52,
    },
    dry: {
      bl: 'M-17 -386 Q-11 -391 -5 -385',
      br: 'M6 -385 Q11 -390 17 -386',
      mouth: 'M-8 -351 Q1 -349 8 -352',
      eye: .68,
    },
    wry: {
      bl: 'M-17 -387 Q-10 -390 -5 -386',
      br: 'M6 -384 Q12 -392 17 -387',
      mouth: 'M-8 -351 Q2 -347 9 -353',
      eye: .76,
    },
    smile: {
      bl: 'M-17 -386 Q-11 -390 -5 -386',
      br: 'M6 -386 Q12 -390 17 -385',
      mouth: 'M-9 -352 Q0 -344 9 -352 Q0 -348 -9 -352',
      eye: .62,
      open: true,
    },
    laugh: {
      bl: 'M-18 -388 Q-11 -392 -5 -388',
      br: 'M6 -388 Q12 -392 18 -388',
      mouth: 'M-8 -353 Q0 -341 8 -353 Q0 -347 -8 -353Z',
      eye: .3,
      open: true,
    },
    down: {
      bl: 'M-17 -382 Q-11 -384 -6 -382',
      br: 'M6 -382 Q11 -384 17 -382',
      mouth: 'M-7 -351 Q0 -354 7 -351',
      eye: .18,
    },
    side: {
      bl: 'M-17 -385 Q-11 -388 -6 -385',
      br: 'M6 -385 Q11 -389 17 -386',
      mouth: 'M-8 -351 Q0 -349 7 -352',
      eye: .8,
      lookX: 2,
    },
  };

  const POSES = {
    /* ——— 露西 ——— */
    luCountA: { body: 2, head: 20, laTarget: [-12, -206], raTarget: [16, -208], face: 'down' },
    luCountB: { body: 2, head: 18, laTarget: [-8, -206], raTarget: [24, -210], face: 'down' },
    luLook: { body: 0, head: 3, laTarget: [-14, -206], raTarget: [14, -208], face: 'flat' },
    luNotice: { body: -2, head: -9, laTarget: [-14, -206], raTarget: [18, -218], face: 'side' },
    luTalk: { body: -1, head: -4, laTarget: [-14, -206], raTarget: [30, -240], face: 'talk' },
    luDry: { body: 0, head: -7, laTarget: [-12, -206], raTarget: [24, -222], face: 'dry' },
    luSmile: { body: -2, head: -5, laTarget: [-14, -206], raTarget: [27, -226], face: 'smile' },
    luLaugh: { body: -4, head: -9, laTarget: [-16, -206], raTarget: [30, -230], face: 'laugh' },
    luReach: { body: -7, head: 6, laTarget: [-18, -206], raTarget: [108, -238], face: 'side' },
    luSteady: { body: -4, head: 5, laTarget: [-16, -206], raTarget: [56, -226], face: 'flat' },
    luPlace: { body: -2, head: 10, laTarget: [-14, -206], raTarget: [26, -210], face: 'flat' },
    luWipeA: { body: 3, head: 15, laTarget: [-28, -206], raTarget: [30, -212], face: 'down' },
    luWipeB: { body: 2, head: 13, laTarget: [-28, -206], raTarget: [44, -214], face: 'down' },
    luTurn: { body: 9, head: 17, laTarget: [-20, -206], raTarget: [38, -212], face: 'flat' },

    /* ——— 尼尔 ——— */
    nlTray: { body: 2, head: 0, laTarget: [-42, -252], raTarget: [42, -252], face: 'tired' },
    nlTrayTalk: { body: 1, head: 5, laTarget: [-42, -252], raTarget: [42, -252], face: 'talk' },
    nlTrayNod: { body: 2, head: 8, laTarget: [-42, -252], raTarget: [42, -252], face: 'flat' },
    nlTrayWry: { body: 0, head: -3, laTarget: [-42, -252], raTarget: [42, -252], face: 'wry' },
    nlTrayFlat: { body: 1, head: 2, laTarget: [-42, -252], raTarget: [42, -252], face: 'flat' },
    nlTrayListen: { body: 2, head: 7, laTarget: [-44, -250], raTarget: [44, -250], face: 'side' },
    nlTrayShift: { body: 3, head: 10, laTarget: [-40, -254], raTarget: [46, -250], face: 'down' },
    nlTrayRelax: { body: 1, head: -1, laTarget: [-38, -248], raTarget: [38, -248], face: 'flat' },
  };

  const instances = {};
  let report = () => {};

  const clamp = (v, a, b) => Math.max(a, Math.min(b, v));

  /* 两骨 IK：把“手放在哪里”换算成肩/肘角度 */
  function solveArm(side, target) {
    const S = { x: side * 34, y: -318 };
    const L1 = 56;
    const L2 = 52;
    let dx = target[0] - S.x;
    let dy = target[1] - S.y;
    let d = Math.hypot(dx, dy);
    const maxD = L1 + L2 - .02;
    const minD = Math.abs(L1 - L2) + .02;
    d = clamp(d, minD, maxD);
    const ux = dx / (Math.hypot(dx, dy) || 1);
    const uy = dy / (Math.hypot(dx, dy) || 1);
    const tx = S.x + ux * d;
    const ty = S.y + uy * d;

    const a = (L1 * L1 - L2 * L2 + d * d) / (2 * d);
    const h = Math.sqrt(Math.max(0, L1 * L1 - a * a));
    const e1 = { x: S.x + ux * a - uy * h, y: S.y + uy * a + ux * h };
    const e2 = { x: S.x + ux * a + uy * h, y: S.y + uy * a - ux * h };
    const score = e => (e.y * 2 + side * e.x * .35);
    const E = score(e1) >= score(e2) ? e1 : e2;

    const alpha = Math.atan2(-(E.x - S.x), E.y - S.y) * 180 / Math.PI;
    const gamma = Math.atan2(-(tx - E.x), ty - E.y) * 180 / Math.PI;
    return [alpha, gamma - alpha];
  }

  function headMarkup(def) {
    const { skin, hair } = def;
    const isNl = def.role === 'bar' && def.hat;
    const isLu = def.role === 'cafe';

    let s = `<g class="head" style="transform-origin:0px -338px">`;
    s += `<path d="M-10 -344H10V-330Q0 -322 -10 -330Z" fill="${skin}"/>`;
    s += `<path d="M-10 -344H10V-336Q0 -330 -10 -338Z" fill="#000" opacity=".18"/>`;
    s += `<ellipse cx="-27" cy="-369" rx="5" ry="8" fill="${skin}"/>`;
    s += `<ellipse cx="27" cy="-369" rx="5" ry="8" fill="${skin}"/>`;

    const face = `M-27 -371Q-27 -342 0 -336Q27 -342 27 -371Q27 -402 0 -405Q-27 -402 -27 -371Z`;
    s += `<path d="${face}" fill="${skin}"/>`;
    s += `<path d="${face}" fill="#000" opacity=".07"/>`;
    s += `<path d="M-27 -371Q-27 -342 0 -336Q27 -342 27 -371" fill="none" stroke="#7e553f" stroke-width="1" opacity=".3"/>`;

    s += `<path d="M2 -376Q9 -369 7 -363Q4 -360 0 -362" fill="none" stroke="#8c5c45" stroke-width="1.3" opacity=".6"/>`;
    s += `<path d="M0 -359V-355" stroke="#8c5c45" stroke-width=".9" opacity=".4"/>`;

    const eye = (x, side) => `
      <g class="eye-group">
        <ellipse class="eye" cx="${x}" cy="-373" rx="5.2" ry="2.9" fill="#eee6d6"/>
        <circle class="pupil" cx="${x}" cy="-373" r="1.9" fill="#1a1411"/>
        <path d="M${x - 6} -374Q${x} -378 ${x + 6} -374" fill="none" stroke="#2a1a16" stroke-width="1.1" opacity=".8"/>
        ${isLu ? `<path d="M${x + side * 5} -376 Q${x + side * 8} -378 ${x + side * 9} -375" fill="none" stroke="#1a1012" stroke-width=".9"/>` : ''}
      </g>`;
    s += eye(-10, -1) + eye(10, 1);

    const browW = isLu ? 1.6 : 2.1;
    s += `<path class="brow" d="M-17 -385Q-11 -389 -6 -386" fill="none" stroke="${hair}" stroke-width="${browW}" stroke-linecap="round"/>`;
    s += `<path class="brow" d="M6 -386Q11 -389 17 -385" fill="none" stroke="${hair}" stroke-width="${browW}" stroke-linecap="round"/>`;

    s += `<path class="mouth" d="M-8 -351Q0 -349 8 -351" fill="none" stroke="${def.lip || '#6d4039'}" stroke-width="2" stroke-linecap="round"/>`;

    if (isNl) {
      s += `<path d="M-18 -346Q0 -337 18 -346" fill="none" stroke="#6b5848" stroke-width=".8" opacity=".35" stroke-dasharray="1 3"/>`;
      s += `<path d="M-19 -355Q0 -346 19 -355" fill="none" stroke="#6b5848" stroke-width=".6" opacity=".22"/>`;
      s += `<ellipse cx="0" cy="-392" rx="43" ry="10" fill="${def.hat}" stroke="#15130f" stroke-width="1"/>
        <path d="M-27 -394Q-25 -430 0 -430Q25 -430 27 -394Z" fill="${def.hat}" stroke="#15130f" stroke-width="1"/>
        <path d="M-27 -402Q0 -412 27 -402" fill="none" stroke="#4a4234" stroke-width="1.1"/>
        <path d="M-24 -396Q0 -401 24 -396" fill="none" stroke="#0b0a08" stroke-width="2"/>`;
    } else if (isLu) {
      // 齐颈短发，发尾向两侧收
      s += `<path d="M-29 -380Q-33 -414 -6 -418Q20 -421 29 -398Q33 -388 29 -373Q24 -394 4 -399Q-16 -403 -27 -385Z" fill="${hair}"/>`;
      s += `<path d="M-29 -380Q-36 -354 -27 -337Q-18 -345 -21 -366Z" fill="${hair}"/>`;
      s += `<path d="M29 -376Q38 -356 29 -338Q20 -346 23 -366Z" fill="${hair}"/>`;
      s += `<path d="M-20 -410Q-2 -420 18 -406" fill="none" stroke="#4d3028" stroke-width="1.1" opacity=".75"/>`;
      // 鬓角铅笔
      s += `<path d="M29 -368L36 -383" stroke="${def.accent}" stroke-width="2" stroke-linecap="round"/>`;
      s += `<path d="M36 -383L40 -388" stroke="#2a2118" stroke-width="2.2" stroke-linecap="round"/>`;
      // 耳钉
      s += `<circle cx="30" cy="-354" r="2" fill="${def.accent}"/>`;
    }

    s += `<path d="M-10 -341Q0 -333 10 -341" fill="none" stroke="#000" stroke-width="2" opacity=".12"/>`;
    s += `</g>`;
    return s;
  }

  function legMarkup(def, side) {
    const x = side * 15;
    const pants = def.pants || '#141518';
    const shoe = def.shoe || '#090a0d';
    return `<g class="leg leg${side < 0 ? 'L' : 'R'}" style="transform-origin:${x}px -188px">
      <path d="M${x - 9} -188Q${x - 11} -148 ${x - 8} -110L${x + 8} -110Q${x + 11} -148 ${x + 9} -188Z" fill="${pants}"/>
      <g class="shin" style="transform-origin:${x}px -110px">
        <path d="M${x - 8} -110Q${x - 9} -66 ${x - 7} -22L${x + 7} -22Q${x + 9} -66 ${x + 8} -110Z" fill="${pants}"/>
        <path d="M${x - 8} -28L${x + 7} -28L${x + (side > 0 ? 18 : 16)} -14Q${x + (side > 0 ? 22 : 20)} -4 ${x + 8} -5L${x - 10} -8Z"
          fill="${shoe}"/>
        <path d="M${x + 4} -8L${x + 16} -7" stroke="#5a6378" stroke-width=".9" opacity=".5"/>
      </g>
    </g>`;
  }

  function armMarkup(def, side) {
    const x = side * 34;
    const isLu = def.role === 'cafe';
    const sleeve = isLu ? def.shirt : (def.coat || def.shirt);
    const fore = isLu ? def.skin : (def.coatDark || def.coat);
    const hand = def.skin;
    const cls = side < 0 ? 'armL' : 'armR';
    const upperD = `M${x - 9} -318Q${x - 12} -290 ${x - 8} -262L${x + 8} -262Q${x + 12} -290 ${x + 9} -318Z`;
    const foreD = `M${x - 8} -262Q${x - 9} -236 ${x - 7} -212L${x + 7} -212Q${x + 9} -236 ${x + 8} -262Z`;

    let s = `<g class="arm ${cls}" style="transform-origin:${x}px -318px">
      <path d="${upperD}" fill="${sleeve}"/>
      <path d="${upperD}" fill="#000" opacity=".08"/>
      <path d="${upperD}" fill="none" stroke="#000" stroke-width=".8" opacity=".2"/>`;

    if (isLu) {
      // 卷起的袖口
      s += `<rect x="${x - 9}" y="-270" width="18" height="9" fill="${def.shirt}"/>`;
      s += `<rect x="${x - 9}" y="-270" width="18" height="2" fill="#b7ab93"/>`;
      s += `<path d="M${x - 9} -270H${x + 9}" stroke="#9f947e" stroke-width=".8"/>`;
    }
    if (def.role === 'bar' && side < 0) {
      // 码头留下的煤灰，只在对应分支显示
      s += `<g class="coal" opacity="0">
        <path d="M${x - 9} -300Q${x - 3} -292 ${x - 8} -282" fill="none" stroke="#7f786c" stroke-width="4" opacity=".7" stroke-linecap="round"/>
        <path d="M${x + 3} -304Q${x + 8} -294 ${x + 2} -286" fill="none" stroke="#6a6358" stroke-width="2.6" opacity=".6" stroke-linecap="round"/>
      </g>`;
    }

    s += `<g class="fore" style="transform-origin:${x}px -262px">
      <path d="${foreD}" fill="${fore}"/>
      <path d="${foreD}" fill="#000" opacity=".08"/>
      <path d="${foreD}" fill="none" stroke="#000" stroke-width=".8" opacity=".18"/>
      <ellipse cx="${x}" cy="-207" rx="7" ry="9" fill="${hand}"/>
      <path d="M${x - 5} -202Q${x} -198 ${x + 4} -202" fill="none" stroke="#8c5c45" stroke-width=".8" opacity=".5"/>
    </g></g>`;
    return s;
  }

  function torsoMarkup(def) {
    if (def.role === 'cafe') {
      return `
        <path d="M-34 -322Q-39 -284 -35 -238Q-46 -166 -54 -86L54 -86Q46 -166 35 -238Q39 -284 34 -322Q18 -334 0 -336Q-18 -334 -34 -322Z" fill="${def.dress}"/>
        <path d="M-34 -322Q-39 -284 -35 -238Q-46 -166 -54 -86L-18 -86Q-28 -166 -24 -238Q-29 -284 -24 -322Z" fill="#000" opacity=".13"/>
        <path d="M34 -322Q39 -284 35 -238Q46 -166 54 -86L18 -86Q28 -166 24 -238Q29 -284 24 -322Z" fill="#fff" opacity=".04"/>
        <path d="M-22 -322Q0 -302 22 -322L18 -334Q0 -340 -18 -334Z" fill="${def.shirt}"/>
        <path d="M-22 -322Q0 -302 22 -322" fill="none" stroke="${def.apron}" stroke-width="1.2" opacity=".7"/>
        <path d="M-8 -318V-302M8 -318V-302" stroke="${def.accent}" stroke-width="1.1" opacity=".7"/>
        <circle cx="0" cy="-304" r="1.5" fill="${def.accent}"/>
        <path d="M-24 -250H24L34 -96H-34Z" fill="${def.apron}"/>
        <path d="M-24 -250H24M-32 -214H32" stroke="#b7ab93" stroke-width="1" opacity=".8"/>
        <path d="M-32 -214H-44M32 -214H44" stroke="${def.apron}" stroke-width="4"/>
        <path d="M-34 -238Q0 -230 34 -238" fill="none" stroke="${def.dressDark}" stroke-width="4" opacity=".85"/>
        <path d="M-58 -84Q-34 -94 0 -94Q34 -94 58 -84" fill="none" stroke="${def.dressDark}" stroke-width="1.2" opacity=".8"/>
        <path d="M-30 -84V-56M30 -84V-56" stroke="${def.dressDark}" stroke-width="1" opacity=".4"/>`;
    }
    return `
      <path d="M-38 -320Q-45 -274 -43 -122L-22 -116L-18 -210H18L22 -116L43 -122Q45 -274 38 -320Q18 -334 0 -336Q-18 -334 -38 -320Z" fill="${def.coat}"/>
      <path d="M-38 -320Q-45 -274 -43 -122L-22 -116L-18 -210H-4L-8 -320Q-20 -330 -38 -320Z" fill="#000" opacity=".1"/>
      <path d="M-18 -210H18L22 -116L44 -122Q45 -274 38 -320Q18 -334 0 -336Q-18 -334 -38 -320Q-45 -274 -43 -122L-22 -116Z" fill="none" stroke="#201d19" stroke-width="1.2" opacity=".65"/>
      <path d="M-14 -320L0 -290L14 -320L10 -252H-10Z" fill="${def.shirt}"/>
      <path d="M-5 -316L0 -306L5 -316L3 -260H-3Z" fill="${def.tie}"/>
      <path d="M-38 -320Q-22 -306 -12 -300M38 -320Q22 -306 12 -300" fill="none" stroke="${def.coatDark}" stroke-width="2.2"/>
      <path d="M-40 -238Q0 -230 40 -238" fill="none" stroke="${def.coatDark}" stroke-width="6"/>
      <rect x="-8" y="-242" width="16" height="9" fill="#7a6a4b" opacity=".9"/>
      <path d="M-31 -210Q-20 -204 -13 -210M31 -210Q20 -204 13 -210" fill="none" stroke="${def.coatDark}" stroke-width="1"/>
      <path d="M-22 -116L-10 -114M22 -116L10 -114" stroke="${def.shoe}" stroke-width="2" opacity=".7"/>`;
  }

  function trayMarkup() {
    return `<g class="tray-prop">
      <ellipse cx="0" cy="-242" rx="58" ry="10" fill="#0b0d12" stroke="#c9b489" stroke-width="1.3"/>
      <ellipse cx="0" cy="-247" rx="50" ry="6.5" fill="#181a20" stroke="#8f8267" stroke-width="1"/>
      <g id="tray-cup-a" transform="translate(-20 -248) scale(.62)">
        <ellipse cx="0" cy="0" rx="13" ry="3.6" fill="none" stroke="#bdcadf" stroke-width="1.2"/>
        <path d="M-10 -1L-7 20H7L10 -1" fill="#0b0f18" fill-opacity=".6" stroke="#d2ddec" stroke-width="1.5"/>
        <path d="M-7 7H7L6 19H-6Z" fill="#6b4527" opacity=".7"/>
        <path d="M10 2Q16 4 15 9Q14 14 9 14" fill="none" stroke="#d2ddec" stroke-width="1.2"/>
      </g>
      <g id="tray-cup-b" transform="translate(22 -248) scale(.62)">
        <g class="cup-inner">
          <ellipse cx="0" cy="0" rx="13" ry="3.6" fill="none" stroke="#bdcadf" stroke-width="1.2"/>
          <path d="M-10 -1L-7 20H7L10 -1" fill="#0b0f18" fill-opacity=".6" stroke="#d2ddec" stroke-width="1.5"/>
          <path d="M-7 7H7L6 19H-6Z" fill="#6b4527" opacity=".7"/>
          <path d="M10 2Q16 4 15 9Q14 14 9 14" fill="none" stroke="#d2ddec" stroke-width="1.2"/>
        </g>
      </g>
    </g>`;
  }

  function buildMarkup(id, def) {
    const tray = id === 'nl' ? trayMarkup() : '';
    return `<g class="puppet" id="p_${id}" style="opacity:0">
      <ellipse cx="0" cy="2" rx="${id === 'lu' ? 54 : 58}" ry="10" fill="#000" opacity=".44"/>
      <g class="mirror" transform="scale(${def.facing} 1)">
        ${legMarkup(def, -1)}
        ${legMarkup(def, 1)}
        <g class="body" style="transform-origin:0px -188px">
          ${torsoMarkup(def)}
          ${armMarkup(def, -1)}
          ${armMarkup(def, 1)}
          ${tray}
          ${headMarkup(def)}
        </g>
      </g>
    </g>`;
  }

  function buildInto(container, id) {
    const def = DEFS[id];
    container.insertAdjacentHTML('beforeend', buildMarkup(id, def));
    const root = document.getElementById('p_' + id);
    instances[id] = {
      id,
      def,
      root,
      mirror: root.querySelector('.mirror'),
      body: root.querySelector('.body'),
      head: root.querySelector('.head'),
      armL: root.querySelector('.armL'),
      armR: root.querySelector('.armR'),
      foreL: root.querySelector('.armL .fore'),
      foreR: root.querySelector('.armR .fore'),
      legL: root.querySelector('.legL'),
      legR: root.querySelector('.legR'),
      shinL: root.querySelector('.legL .shin'),
      shinR: root.querySelector('.legR .shin'),
      st: { x: -400, y: 820, s: 1, f: def.facing, pose: '—', face: 'flat' },
    };
    return instances[id];
  }

  function setFace(id, name) {
    const inst = instances[id];
    const e = EXPR[name] || EXPR.flat;
    const root = inst.root;
    const brows = root.querySelectorAll('.brow');
    brows[0].setAttribute('d', e.bl);
    brows[1].setAttribute('d', e.br);
    const mouth = root.querySelector('.mouth');
    mouth.setAttribute('d', e.mouth);
    if (e.open) {
      mouth.setAttribute('fill', inst.def.lip || '#66262d');
      mouth.setAttribute('stroke', inst.def.lip || '#66262d');
    } else {
      mouth.setAttribute('fill', 'none');
      mouth.setAttribute('stroke', inst.def.lip || '#6d4039');
    }
    root.querySelectorAll('.eye').forEach(eye => {
      eye.style.transform = `scaleY(${e.eye})`;
    });
    root.querySelectorAll('.pupil').forEach(p => {
      p.setAttribute('transform', `translate(${e.lookX || 0} ${e.lookY || 0})`);
    });
    inst.st.face = name;
  }

  function setRoot(id, x, y, s, ms) {
    const inst = instances[id];
    inst.st.x = x;
    inst.st.y = y;
    inst.st.s = s;
    inst.root.style.transitionDuration = (ms ?? 0) + 'ms';
    inst.root.style.transform = `translate(${x}px,${y}px) scale(${s})`;
  }

  function setFacing(id, f) {
    const inst = instances[id];
    inst.st.f = f;
    inst.mirror.setAttribute('transform', `scale(${f} 1)`);
  }

  function apply(id, p = {}) {
    const inst = instances[id];
    const body = p.body || 0;
    const head = p.head || 0;
    inst.body.style.transform = `rotate(${body}deg)`;
    inst.head.style.transform = `rotate(${head}deg)`;

    let la = p.la || [8, 5];
    let ra = p.ra || [-8, -5];
    if (p.laTarget) la = solveArm(-1, p.laTarget);
    if (p.raTarget) ra = solveArm(1, p.raTarget);

    inst.armL.style.transform = `rotate(${la[0]}deg)`;
    inst.foreL.style.transform = `rotate(${la[1]}deg)`;
    inst.armR.style.transform = `rotate(${ra[0]}deg)`;
    inst.foreR.style.transform = `rotate(${ra[1]}deg)`;

    const ll = p.ll || [0, 0];
    const rl = p.rl || [0, 0];
    inst.legL.style.transform = `rotate(${ll[0]}deg)`;
    inst.shinL.style.transform = `rotate(${ll[1]}deg)`;
    inst.legR.style.transform = `rotate(${rl[0]}deg)`;
    inst.shinR.style.transform = `rotate(${rl[1]}deg)`;

    if (p.f !== undefined) setFacing(id, p.f);
    if (p.face) setFace(id, p.face);
    if (p.x !== undefined || p.y !== undefined || p.s !== undefined) {
      setRoot(
        id,
        p.x !== undefined ? p.x : inst.st.x,
        p.y !== undefined ? p.y : inst.st.y,
        p.s !== undefined ? p.s : inst.st.s,
        p.move ?? 0
      );
    }

    inst.st.pose = p.poseName || p.label || inst.st.pose;
    report(id, inst.st);
  }

  function pose(id, name, opts = {}) {
    const p = { ...(POSES[name] || {}), ...opts, poseName: opts.label || name };
    apply(id, p);
  }

  function move(id, x, y, ms = 600, s) {
    const inst = instances[id];
    setRoot(id, x, y, s === undefined ? inst.st.s : s, ms);
  }

  function show(id, on = true, ms = 420) {
    const inst = instances[id];
    inst.root.style.transitionDuration = ms + 'ms';
    inst.root.style.opacity = on ? 1 : 0;
  }

  function reset(id, x, y, s = 1, poseName) {
    const inst = instances[id];
    inst.root.style.transitionDuration = '0ms';
    inst.root.style.opacity = 0;
    inst.root.style.filter = 'none';
    setRoot(id, x, y, s, 0);
    setFacing(id, inst.def.facing);
    inst.root.classList.add('snap');
    pose(id, poseName || (id === 'lu' ? 'luCountA' : 'nlTray'));
    inst.root.classList.remove('snap');
  }

  window.Cast = {
    POSES,
    DEFS,
    buildInto,
    pose,
    apply,
    setFace,
    setFacing,
    move,
    show,
    reset,
    get: id => instances[id],
    onReport(fn) { report = fn; },
  };
})();
