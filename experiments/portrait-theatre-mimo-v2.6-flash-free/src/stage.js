// 舞台渲染：把状态（人物、道具、光、特效）同步到 DOM。
// 所有 cue 都是绝对状态，因此可以无动画地重放到任意拍点。
PT.Stage = class {
  constructor(root) {
    this.root = root;
    this.cast = PT.cast;
    this.state = null;
    this.scene = null;
    this.build();
    this.actorEls = {};
    this.propEls = {};
  }

  build() {
    this.root.innerHTML = `
      <div class="shake">
        <div class="bg"></div>
        <div class="floor"><i></i></div>
        <div class="props"></div>
        <div class="actors"></div>
        <div class="fx">
          <div class="rain r1"></div>
          <div class="rain r2"></div>
          <div class="tears"></div>
          <div class="vignette"></div>
        </div>
      </div>
      <div class="subtitle" aria-live="polite"></div>`;
    this.el = {
      shake: this.root.querySelector('.shake'),
      bg: this.root.querySelector('.bg'),
      floor: this.root.querySelector('.floor'),
      props: this.root.querySelector('.props'),
      actors: this.root.querySelector('.actors'),
      rain1: this.root.querySelector('.r1'),
      rain2: this.root.querySelector('.r2'),
      tears: this.root.querySelector('.tears'),
      subtitle: this.root.querySelector('.subtitle'),
    };
  }

  // ---------- 场景 ----------
  setScene(scene) {
    this.scene = scene;
    this.state = {
      actors: structuredClone(scene.initial.actors),
      props: structuredClone(scene.initial.props || {}),
      fx: Object.assign({ rain: 0, tears: 0, intercomGlow: 0 }, scene.initial.fx || {}),
      light: Object.assign({ focus: null }, scene.initial.light || {}),
    };
    // 换幕时清掉上一幕留下的人物与道具
    this.el.actors.innerHTML = '';
    this.actorEls = {};
    this.el.tears.innerHTML = '';
    for (const id of Object.keys(this.state.actors)) this.ensureActor(id);
    this.el.bg.style.background = scene.colors.bg;
    this.el.floor.style.background = scene.colors.floor;
    this.el.floor.style.setProperty('--line', scene.colors.line);
    this.buildProps();
    this.render({ anim: false });
  }

  ensureActor(role) {
    if (this.actorEls[role]) return;
    const el = document.createElement('div');
    el.className = 'actor';
    el.dataset.role = role;
    el.style.setProperty('--h', `${PT.HEIGHTS[role]}px`);
    el.innerHTML = `<img class="pose" alt="">`;
    this.el.actors.appendChild(el);
    this.actorEls[role] = el;
  }

  sprite(role, pose) {
    const s = this.cast[pose];
    if (!s) throw new Error(`未知姿势: ${role}.${pose}`);
    if (!PT.ROLES.includes(role)) throw new Error(`未知人物: ${role}`);
    return s;
  }

  // ---------- cue 合并 ----------
  apply(cue, { anim = true } = {}) {
    const st = this.state;
    if (cue.poses) for (const [role, pose] of Object.entries(cue.poses)) {
      this.ensureActor(role);
      this.sprite(role, pose);
      st.actors[role].pose = pose;
    }
    if (cue.move) for (const [role, x] of Object.entries(cue.move)) {
      this.ensureActor(role);
      st.actors[role].x = x;
    }
    if (cue.show) for (const [role, v] of Object.entries(cue.show)) {
      this.ensureActor(role);
      st.actors[role].show = v;
    }
    if (cue.facing) for (const [role, v] of Object.entries(cue.facing)) {
      this.ensureActor(role);
      st.actors[role].facing = v;
    }
    if (cue.props) for (const [id, patch] of Object.entries(cue.props)) {
      if (!PROPS[id]) throw new Error(`未知道具: ${id}`);
      st.props[id] = Object.assign({ show: 0 }, st.props[id] || {}, patch);
    }
    if (cue.fx) Object.assign(st.fx, cue.fx);
    if (cue.light) Object.assign(st.light, cue.light);
    this.render({ anim });
    if (cue.fx && 'rain' in cue.fx) PT.audio.rain(!!st.fx.rain);
    if (cue.fx && 'tears' in cue.fx) this.renderTears(anim);
    if (cue.shake && anim) this.shake();
  }

  render({ anim = true } = {}) {
    const st = this.state;
    const layer = this.el.shake;
    this.instantPose = !anim;
    if (!anim) layer.classList.add('no-anim');
    else if (layer.classList.contains('no-anim')) { layer.classList.remove('no-anim'); void layer.offsetWidth; }

    for (const [role, a] of Object.entries(st.actors)) {
      const el = this.actorEls[role];
      if (!el) continue;
      el.style.setProperty('--x', a.x);
      el.style.setProperty('--flip', a.facing === -1 ? -1 : 1);
      el.classList.toggle('gone', !a.show);
      const dim = st.light.focus && st.light.focus !== role;
      el.classList.toggle('dim', !!dim);
      this.setPose(role, el, a.pose);
    }
    for (const [id, p] of Object.entries(st.props)) {
      const el = this.propEls[id];
      if (!el) continue;
      el.classList.toggle('gone', !p.show);
      el.classList.toggle('open', !!p.open);
      el.classList.toggle('lit', !!p.lit);
      el.classList.toggle('shut', !!p.shut);
      el.classList.toggle('glow', !!st.fx.intercomGlow && id === 'intercom');
      el.classList.toggle('crush', !!p.crush);
      if ('x' in p) el.style.left = `${p.x}px`;
    }
    this.el.rain1.classList.toggle('gone', !st.fx.rain);
    this.el.rain2.classList.toggle('gone', !st.fx.rain);
    if (st.fx.rain && !PT.audio.muted) PT.audio.rain(true);
    else PT.audio.rain(false);
  }

  // 重建结束后恢复过渡（此时样式没有变化，不会触发动画）
  endRebuild() {
    const layer = this.el.shake;
    layer.classList.add('no-anim');
    void layer.offsetWidth;
    layer.classList.remove('no-anim');
  }

  setPose(role, el, pose) {
    if (el.dataset.pose === pose) return;
    const s = this.sprite(role, pose);
    const img = el.querySelector('.pose');
    clearTimeout(el._swapTimer);
    if (this.instantPose) {
      img.src = s.file;
      el.dataset.pose = pose;
      el.classList.remove('swap');
      return;
    }
    el.classList.add('swap');           // 先淡出，再换图淡入，避免姿势“闪跳”
    el._swapTimer = setTimeout(() => {
      img.src = s.file;
      el.dataset.pose = pose;
      el.classList.remove('swap');
    }, 160);
  }

  // ---------- 道具 ----------
  buildProps() {
    this.el.props.innerHTML = '';
    this.propEls = {};
    for (const id of Object.keys(this.state.props)) {
      const def = PROPS[id];
      if (!def) throw new Error(`未知道具: ${id}`);
      const el = document.createElement('div');
      el.className = `prop prop-${id}`;
      el.style.left = `${def.x}px`;
      el.style.top = `${def.y}px`;
      el.innerHTML = def.svg();
      this.el.props.appendChild(el);
      this.propEls[id] = el;
    }
  }

  // ---------- 字幕 ----------
  subtitle({ say, text, dir, via }) {
    const box = this.el.subtitle;
    if (dir) {
      box.className = 'subtitle show dir';
      box.innerHTML = `<span class="dtext">${dir}</span>`;
      return;
    }
    box.className = 'subtitle show';
    box.innerHTML =
      `<span class="who" data-who="${say}">${say}</span>` +
      (via ? `<span class="via">${via}</span>` : '') +
      `<span class="txt">${text}</span>`;
  }

  clearSubtitle() { this.el.subtitle.className = 'subtitle'; this.el.subtitle.innerHTML = ''; }

  shake() {
    this.el.shake.classList.remove('shaking');
    void this.el.shake.offsetWidth;
    this.el.shake.classList.add('shaking');
  }

  renderTears(anim) {
    const n = this.state.fx.tears;
    const box = this.el.tears;
    box.innerHTML = '';
    if (!n) return;
    const a = this.state.actors['夜莺'];
    const el = this.actorEls['夜莺'];
    if (!a || !el) throw new Error('眼泪需要夜莺在台上');
    const h = PT.HEIGHTS['夜莺'];
    const sp = this.cast[a.pose];
    if (!sp || sp.role !== '夜莺') throw new Error(`眼泪需要夜莺的姿势，得到: ${a.pose}`);
    const dispW = sp.w * (h / sp.h);
    // 脸颊（手下方）在精灵中的比例位置
    const FX = 0.72, FY = 0.23;
    const originX = a.x + (a.facing === -1 ? (0.5 - FX) : (FX - 0.5)) * dispW;
    const originY = 720 - 104 - h + FY * h;
    for (let i = 0; i < n; i++) {
      const d = document.createElement('i');
      d.className = 'drop' + (anim ? '' : ' static');
      d.style.left = `${originX + i * 14}px`;
      d.style.top = `${originY}px`;
      d.style.animationDelay = `${i * 0.55}s`;
      box.appendChild(d);
    }
  }
};

PT.ROLES = ['夜莺', '尼尔', '经理', '工人'];
PT.HEIGHTS = { 夜莺: 452, 尼尔: 476, 经理: 430, 工人: 300 };

// ---------- 道具定义（全部为霓虹灯管风格的内联 SVG，不使用背景图） ----------
const PROPS = {
  // 入户电铃对讲面板
  intercom: {
    x: 960, y: 286,
    svg: () => `
      <svg viewBox="0 0 64 116" width="64" height="116">
        <rect x="3" y="3" width="58" height="110" rx="6" fill="#0b0f16" stroke="#dfe9ff" stroke-width="3"/>
        <rect x="12" y="12" width="40" height="26" rx="3" fill="none" stroke="#8fb2e6" stroke-width="2"/>
        <g class="btn301">
          <rect x="14" y="76" width="16" height="16" rx="2" fill="#151b25" stroke="#dfe9ff" stroke-width="2"/>
          <text x="22" y="88" font-size="9" fill="#ffd479" text-anchor="middle" font-family="sans-serif">301</text>
        </g>
        <rect x="36" y="76" width="14" height="16" rx="2" fill="none" stroke="#7d8ba3" stroke-width="2"/>
        <rect x="14" y="96" width="36" height="10" rx="2" fill="none" stroke="#7d8ba3" stroke-width="2"/>
        <circle cx="32" cy="56" r="6" fill="none" stroke="#8fb2e6" stroke-width="2"/>
      </svg>`,
  },
  // 公寓楼下的门
  aptDoor: {
    x: 1166, y: 168,
    svg: () => `
      <div class="doorlight"></div>
      <svg class="doorsvg" viewBox="0 0 168 436" width="168" height="436">
        <rect x="6" y="6" width="156" height="424" rx="4" fill="#090c12" stroke="#dfe9ff" stroke-width="4"/>
        <rect x="24" y="26" width="120" height="132" rx="3" fill="none" stroke="#5f7fae" stroke-width="3"/>
        <rect x="24" y="184" width="120" height="100" rx="3" fill="none" stroke="#5f7fae" stroke-width="3"/>
        <rect x="24" y="310" width="120" height="96" rx="3" fill="none" stroke="#5f7fae" stroke-width="3"/>
        <circle cx="134" cy="248" r="8" fill="none" stroke="#ffd479" stroke-width="3"/>
      </svg>`,
  },
  // 靠墙放好的收伞
  umbrellaLean: {
    x: 946, y: 470,
    svg: () => `
      <svg viewBox="0 0 60 150" width="60" height="150">
        <path d="M30 14 C26 46 24 96 26 134" fill="none" stroke="#dfe9ff" stroke-width="4" stroke-linecap="round"/>
        <path d="M26 134 c-8 4 -10 12 -2 14 c6 2 10 -4 8 -10" fill="none" stroke="#dfe9ff" stroke-width="4" stroke-linecap="round"/>
        <path d="M18 34 L30 20 L42 34 Z" fill="none" stroke="#9fb6d8" stroke-width="3" stroke-linejoin="round"/>
        <path d="M22 52 L38 52" stroke="#9fb6d8" stroke-width="3"/>
      </svg>`,
  },
  // 红色“金牌”香烟（全场唯一饱和红）—— y 让它落在尼尔伸出的手上
  pack: {
    x: 545, y: 300,
    svg: () => `
      <svg viewBox="0 0 84 116" width="84" height="116">
        <g class="packbody">
          <rect x="8" y="10" width="68" height="96" rx="5" fill="#c22127" stroke="#ff6b62" stroke-width="3"/>
          <rect x="8" y="34" width="68" height="20" fill="#e9b64d" stroke="none"/>
          <text x="42" y="29" font-size="13" fill="#ffe9b0" text-anchor="middle" font-family="sans-serif" letter-spacing="1">GOLD</text>
          <text x="42" y="86" font-size="11" fill="#ffd9c2" text-anchor="middle" font-family="sans-serif" letter-spacing="2">MEDAL</text>
          <path d="M14 66 q28 10 56 -4" fill="none" stroke="#ff8a80" stroke-width="2.5"/>
        </g>
      </svg>`,
  },
  // 街灯：一根灯柱加一束光
  streetLamp: {
    x: 1146, y: 120,
    svg: () => `
      <div class="lampcone"></div>
      <svg viewBox="0 0 92 500" width="92" height="500">
        <path d="M46 60 v420" stroke="#cfd8ea" stroke-width="6" stroke-linecap="round" fill="none"/>
        <path d="M18 60 h56 l-12 -34 h-32 Z" fill="#0d1018" stroke="#cfd8ea" stroke-width="4" stroke-linejoin="round"/>
        <ellipse class="lampglow" cx="46" cy="62" rx="26" ry="12" fill="#ffd479" opacity="0.75"/>
        <path d="M28 486 h36" stroke="#cfd8ea" stroke-width="6" stroke-linecap="round" fill="none"/>
      </svg>`,
  },
  // 小圆桌 + 房间里的暖灯
  sideTable: {
    x: 1042, y: 402,
    svg: () => `
      <div class="roomglow"></div>
      <svg viewBox="0 0 190 220" width="190" height="220">
        <path d="M18 96 h154" stroke="#e6d3b6" stroke-width="5" stroke-linecap="round" fill="none"/>
        <path d="M34 96 v112 M156 96 v112" stroke="#e6d3b6" stroke-width="5" stroke-linecap="round" fill="none"/>
        <path d="M34 150 h122" stroke="#a98d6a" stroke-width="4" fill="none"/>
        <path d="M126 96 v-26" stroke="#e6d3b6" stroke-width="4" fill="none"/>
        <path d="M104 70 h44 l-9 -34 h-26 Z" fill="#2a2116" stroke="#ffd479" stroke-width="3" stroke-linejoin="round"/>
        <ellipse cx="126" cy="70" rx="22" ry="6" fill="#ffd479" opacity="0.55"/>
      </svg>`,
  },
  // 桌上的帽子
  hat: {
    x: 992, y: 372,
    svg: () => `
      <svg viewBox="0 0 96 56" width="96" height="56">
        <path d="M8 44 q40 14 80 0" fill="#141019" stroke="#e8ddff" stroke-width="3.5" stroke-linecap="round"/>
        <path d="M26 40 q6 -30 22 -30 q16 0 22 30" fill="#141019" stroke="#e8ddff" stroke-width="3.5"/>
        <path d="M28 34 h40" stroke="#7d6fb0" stroke-width="4"/>
      </svg>`,
  },
  // 桌上的手袋
  bag: {
    x: 1090, y: 396,
    svg: () => `
      <svg viewBox="0 0 84 64" width="84" height="64">
        <path d="M26 24 q16 -22 32 0" fill="none" stroke="#e8ddff" stroke-width="3.5"/>
        <rect x="8" y="24" width="68" height="34" rx="6" fill="#141019" stroke="#e8ddff" stroke-width="3.5"/>
        <path d="M8 38 h68" stroke="#7d6fb0" stroke-width="3"/>
        <circle cx="42" cy="38" r="4" fill="#ffd479"/>
      </svg>`,
  },
  // 桌边靠着的收伞
  umbrellaFold: {
    x: 954, y: 348,
    svg: () => `
      <svg viewBox="0 0 56 160" width="56" height="160">
        <path d="M28 24 C24 60 22 112 24 146" fill="none" stroke="#e8ddff" stroke-width="4" stroke-linecap="round"/>
        <path d="M24 146 c-9 4 -11 13 -2 15 c7 2 11 -5 9 -11" fill="none" stroke="#e8ddff" stroke-width="4" stroke-linecap="round"/>
        <path d="M17 44 L28 24 L39 44 Z" fill="#141019" stroke="#e8ddff" stroke-width="3" stroke-linejoin="round"/>
        <path d="M20 66 L36 66" stroke="#7d6fb0" stroke-width="3"/>
      </svg>`,
  },
  // 墙上的电话（正在装）
  wallPhone: {
    x: 1130, y: 246,
    svg: () => `
      <svg viewBox="0 0 132 300" width="132" height="300">
        <path class="cord" d="M64 96 c-34 34 30 58 -6 96 c-26 28 6 46 -6 76" fill="none" stroke="#8fa7c4" stroke-width="3.5" stroke-linecap="round"/>
        <rect x="26" y="24" width="80" height="86" rx="8" fill="#0d1117" stroke="#dfe9ff" stroke-width="4"/>
        <circle cx="66" cy="66" r="24" fill="none" stroke="#8fb2e6" stroke-width="3"/>
        <circle cx="66" cy="66" r="7" fill="#8fb2e6"/>
        <path class="dialglow" d="M66 42 a24 24 0 0 1 22 15" fill="none" stroke="#ffd479" stroke-width="4" stroke-linecap="round"/>
        <rect x="14" y="4" width="104" height="18" rx="9" fill="#141a22" stroke="#dfe9ff" stroke-width="4"/>
        <path d="M14 13 h-8 M118 13 h8" stroke="#dfe9ff" stroke-width="4" stroke-linecap="round"/>
        <path d="M40 110 v176 M92 110 v176" stroke="#4d5f74" stroke-width="3" fill="none"/>
      </svg>`,
  },
  // 剧院大门
  theatreDoor: {
    x: 1076, y: 158,
    svg: () => `
      <div class="doorlight"></div>
      <svg viewBox="0 0 224 452" width="224" height="452">
        <rect x="4" y="4" width="216" height="444" fill="#0a0709" stroke="#f0dfe2" stroke-width="5"/>
        <g class="leaf left">
          <rect x="8" y="8" width="102" height="436" fill="#160c0f" stroke="#c9a6ab" stroke-width="3"/>
          <rect x="22" y="30" width="74" height="150" fill="none" stroke="#8c5560" stroke-width="3"/>
          <rect x="22" y="210" width="74" height="210" fill="none" stroke="#8c5560" stroke-width="3"/>
        </g>
        <g class="leaf right">
          <rect x="114" y="8" width="102" height="436" fill="#160c0f" stroke="#c9a6ab" stroke-width="3"/>
          <rect x="128" y="30" width="74" height="150" fill="none" stroke="#8c5560" stroke-width="3"/>
          <rect x="128" y="210" width="74" height="210" fill="none" stroke="#8c5560" stroke-width="3"/>
        </g>
        <circle cx="104" cy="230" r="7" fill="none" stroke="#ffd479" stroke-width="3"/>
        <circle cx="120" cy="230" r="7" fill="none" stroke="#ffd479" stroke-width="3"/>
      </svg>`,
  },
  // 交接的材料
  papers: {
    x: 700, y: 430,
    svg: () => `
      <svg viewBox="0 0 76 56" width="76" height="56">
        <rect x="6" y="8" width="60" height="42" rx="3" fill="#161a20" stroke="#eef2f8" stroke-width="3" transform="rotate(-6 36 30)"/>
        <rect x="12" y="6" width="60" height="42" rx="3" fill="#eef2f8" stroke="#ffffff" stroke-width="2"/>
        <path d="M20 18 h44 M20 26 h44 M20 34 h30" stroke="#93a1b4" stroke-width="3"/>
      </svg>`,
  },
};

PT.PROPS = PROPS;
