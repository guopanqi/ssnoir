/* 世界视角的 HUD：与场景内标注同一套语言——裸文字导航、深卡面板、发丝线。
   布局：左上=返回+面包屑+单行目标+行动日志，右上=功能菜单，
   左下=人物（半身像/骰子/状态线），右下=物品。
   聚焦层额外有「动作卡」：从建筑垂挂下来的工作单，骰子可拖入骰位。
   HUD 是 HTML 覆盖层，建在 #hud-scale 设计基准画布里（整体等比缩放）；
   根容器不拦截指针，只有面板本身接收事件。 */

const SVG_ICONS = {
  mail: '<svg viewBox="0 0 24 24"><rect x="3" y="6" width="18" height="12" rx="1"/><path d="M3.5 7l8.5 6 8.5-6"/></svg>',
  flask: '<svg viewBox="0 0 24 24"><path d="M10 3h4M11 3v5l-5 9a2 2 0 0 0 1.8 3h8.4a2 2 0 0 0 1.8-3l-5-9V3"/><path d="M8.5 15h7"/></svg>',
  watch: '<svg viewBox="0 0 24 24"><circle cx="12" cy="13" r="7"/><path d="M12 10v3.5l2.5 1.5M10 3h4M10 21h4"/></svg>',
  key: '<svg viewBox="0 0 24 24"><circle cx="8" cy="8" r="4"/><path d="M11 11l9 9M17 17l2-2M14.5 19.5l2-2"/></svg>',
};

const CHARS = [
  { name: '侦探', sub: '私家侦探 · 你', dice: [4, 2, 6], status: { label: '冷静', val: 5, max: 5 } },
  { name: '夜莺', sub: '歌女 · 同行', dice: [5, 1], status: { label: '伤势', val: 1, max: 5 } },
];

const ITEMS = [
  { name: '勒索信', icon: 'mail' },
  { name: '酒壶', icon: 'flask' },
  { name: '怀表', icon: 'watch' },
  { name: '万能钥匙', icon: 'key' },
];

/* 半身像占位：黑色电影剪影（礼帽侦探 / 波浪发歌女），单侧冷银轮廓光 */
function bustCanvas(kind) {
  const c = document.createElement('canvas');
  c.width = 260; c.height = 212;
  const x = c.getContext('2d');
  const g = x.createLinearGradient(0, 0, 0, 212);
  g.addColorStop(0, '#131824'); g.addColorStop(1, '#05070a');
  x.fillStyle = g; x.fillRect(0, 0, 260, 212);
  x.fillStyle = '#04060a';
  x.strokeStyle = 'rgba(140,150,170,0.5)';
  x.lineWidth = 2;
  if (kind === 'sleuth') {
    x.beginPath();                                   // 肩
    x.moveTo(30, 212); x.quadraticCurveTo(54, 150, 104, 142);
    x.lineTo(156, 142); x.quadraticCurveTo(206, 150, 230, 212);
    x.fill(); x.stroke();
    x.beginPath();                                   // 礼帽冠
    x.moveTo(96, 102); x.quadraticCurveTo(98, 62, 130, 60);
    x.quadraticCurveTo(162, 62, 164, 102); x.closePath();
    x.fill(); x.stroke();
    x.beginPath();                                   // 帽檐
    x.ellipse(130, 106, 62, 13, 0, 0, Math.PI * 2);
    x.fill(); x.stroke();
    x.fillRect(110, 106, 40, 32);                    // 脸的下半（阴影里）
  } else {
    x.beginPath();                                   // 肩
    x.moveTo(32, 212); x.quadraticCurveTo(60, 154, 106, 146);
    x.lineTo(150, 146); x.quadraticCurveTo(196, 154, 222, 212);
    x.fill(); x.stroke();
    x.beginPath();                                   // 波浪发
    x.moveTo(90, 136); x.quadraticCurveTo(76, 84, 104, 62);
    x.quadraticCurveTo(130, 42, 154, 62);
    x.quadraticCurveTo(182, 84, 168, 136);
    x.quadraticCurveTo(160, 114, 130, 110);
    x.quadraticCurveTo(100, 114, 90, 136);
    x.fill(); x.stroke();
    x.beginPath(); x.arc(162, 124, 3.5, 0, Math.PI * 2);  // 耳环（唯一的一点琥珀）
    x.fillStyle = '#b08c40'; x.fill();
  }
  return c;
}

const PIPS = {
  1: [[1, 1]],
  2: [[0, 0], [2, 2]],
  3: [[0, 0], [1, 1], [2, 2]],
  4: [[0, 0], [0, 2], [2, 0], [2, 2]],
  5: [[0, 0], [0, 2], [1, 1], [2, 0], [2, 2]],
  6: [[0, 0], [0, 2], [1, 0], [1, 2], [2, 0], [2, 2]],
};

export function mountHUD(host, opts) {
  // 面板建进 #hud-scale 缩放层（HTML 里静态存在；host 其余子节点不动）
  const scaleEl = host.querySelector('#hud-scale') ?? host;
  scaleEl.insertAdjacentHTML('beforeend', `
    <div class="hud-tl">
      <div class="hud-nav">
        <button id="hud-back" class="hud-back hidden">‹ 返回 世界</button>
        <span class="hud-crumb" id="hud-crumb">世界</span>
      </div>
      <div class="hud-objective"><b>目标</b><em>查明夜莺的下落</em></div>
      <div class="hud-log" id="hud-log"></div>
    </div>
    <div class="hud-tr">
      <button class="hud-menu card" title="档案">档</button><button class="hud-menu card" title="地图">图</button><button class="hud-menu card" title="证物">证</button><button class="hud-menu card" title="设置">设</button>
    </div>
    <div class="hud-bl">
      <div class="hud-label">在场人物</div>
      <div class="hud-chars" id="hud-chars"></div>
    </div>
    <div class="hud-br">
      <div class="hud-label">随身物品</div>
      <div class="hud-items" id="hud-items"></div>
    </div>
    <div id="hud-action" class="card hidden">
      <div class="hud-action-tags" id="hud-action-tags"></div>
      <div class="hud-action-title" id="hud-action-title"></div>
      <div class="hud-action-desc" id="hud-action-desc"></div>
      <div class="hud-slotrow"><span class="hud-slot-label">行动力</span><div id="hud-slot" class="hud-slot" title="拖动骰子到此"></div></div>
    </div>
  `);

  const log = scaleEl.querySelector('#hud-log');
  const backBtn = scaleEl.querySelector('#hud-back');
  const crumbEl = scaleEl.querySelector('#hud-crumb');

  function toast(text) {
    const line = document.createElement('div');
    line.className = 'hud-log-line';
    line.textContent = text;
    log.prepend(line);
    while (log.children.length > 3) log.lastChild.remove();
    setTimeout(() => { line.classList.add('fade'); }, 3600);
    setTimeout(() => { line.remove(); }, 5000);
  }

  /* ---------- 人物卡 + 骰子 + 状态线 ---------- */
  const charsEl = scaleEl.querySelector('#hud-chars');
  const draggables = [];
  CHARS.forEach((ch) => {
    const card = document.createElement('div');
    card.className = 'hud-char card';
    const bust = document.createElement('canvas');
    bust.className = 'hud-bust';
    bust.width = 260; bust.height = 212;
    bust.getContext('2d').drawImage(bustCanvas(ch.kind), 0, 0);
    const nameRow = document.createElement('div');
    nameRow.className = 'hud-char-name';
    nameRow.innerHTML = `<span>${ch.name}</span><i>${ch.sub}</i>`;
    const status = document.createElement('div');
    status.className = 'hud-status';
    status.innerHTML = `<span>${ch.status.label}</span><span class="segs">${
      Array.from({ length: ch.status.max }, (_, i) => `<s class="${i < ch.status.val ? 'on' : ''}"></s>`).join('')
    }</span>`;
    const diceRow = document.createElement('div');
    diceRow.className = 'hud-dice';
    ch.dice.forEach((v) => {
      const die = document.createElement('div');
      die.className = 'hud-die';
      die.dataset.value = v;
      PIPS[v].forEach(([r, c]) => {
        const dot = document.createElement('i');
        dot.style.left = `${6 + c * 11}px`;
        dot.style.top = `${6 + r * 11}px`;
        die.appendChild(dot);
      });
      die.title = `行动骰子 · ${v}`;
      diceRow.appendChild(die);
      draggables.push({ el: die, data: { kind: 'die', value: v, who: ch.name } });
    });
    card.append(bust, nameRow, status, diceRow);
    charsEl.appendChild(card);
  });

  /* ---------- 物品 ---------- */
  const itemsEl = scaleEl.querySelector('#hud-items');
  ITEMS.forEach((it) => {
    const el = document.createElement('div');
    el.className = 'hud-item card';
    el.title = it.name;
    el.innerHTML = SVG_ICONS[it.icon];
    itemsEl.appendChild(el);
    draggables.push({ el, data: { kind: 'item', name: it.name } });
  });

  /* ---------- 动作卡（聚焦层的挂牌）+ 骰位 ---------- */
  const actionEl = scaleEl.querySelector('#hud-action');
  const slotEl = scaleEl.querySelector('#hud-slot');
  const leaderSvg = scaleEl.querySelector('#hud-leader');
  let actionOn = false, slotFilledBy = null;

  function showAction(def) {
    if (!def) { actionOn = false; actionEl.classList.add('hidden'); leaderSvg.innerHTML = ''; return; }
    actionOn = true;
    slotFilledBy = null;
    slotEl.textContent = '';
    slotEl.classList.remove('filled');
    actionEl.classList.remove('hidden');
    scaleEl.querySelector('#hud-action-title').textContent = def.title;
    scaleEl.querySelector('#hud-action-desc').textContent = def.desc;
    scaleEl.querySelector('#hud-action-tags').innerHTML = def.tags
      .map((t, i) => `<span class="${i === def.tags.length - 1 && /高|危/.test(t) ? 'risk' : ''}">${t}</span>`).join('');
  }

  /* 面包屑 + 视图 */
  function setCrumb(text) {
    const parts = text.split('›').map((p) => p.trim());
    crumbEl.innerHTML = parts
      .map((p, i) => (i === parts.length - 1 ? `<b>${p}</b>` : `${p}<span class="sep">›</span>`))
      .join('');
  }

  /* ---------- 拖拽：骰子/物品 → 骰位（聚焦）或地标标签（世界） ---------- */
  let hoverRig = null;
  function canvasPos(e) {
    const r = opts.canvasRect();
    return { x: e.clientX - r.left, y: e.clientY - r.top };
  }
  function hitChip(e) {
    if (opts.mode() !== 'world') return null;
    const p = canvasPos(e);
    for (const rig of opts.rigs()) {
      const q = rig.screenRect;
      if (q && p.x >= q.x - 6 && p.x <= q.x + q.w + 6 && p.y >= q.y - 6 && p.y <= q.y + q.h + 6) return rig;
    }
    return null;
  }
  function hitSlot(e) {
    if (opts.mode() !== 'focused' || !actionOn || slotFilledBy) return false;
    const r = slotEl.getBoundingClientRect();
    const m = 8;
    return e.clientX >= r.left - m && e.clientX <= r.right + m && e.clientY >= r.top - m && e.clientY <= r.bottom + m;
  }
  draggables.forEach(({ el, data }) => {
    el.addEventListener('pointerdown', (e) => {
      e.preventDefault();
      e.stopPropagation();
      if (el.classList.contains('spent') || el.classList.contains('gone')) return;
      el.classList.add('dragging');
      if (data.kind === 'die') slotEl.classList.add('hot'); // 骰子一亮，骰位就位
      const gw = el.offsetWidth, gh = el.offsetHeight, gs = opts.hudScale();
      const ghost = el.cloneNode(true);
      ghost.className = `${el.className} hud-ghost`;
      ghost.style.width = `${gw}px`;
      ghost.style.height = `${gh}px`;
      ghost.style.transform = `scale(${gs})`;
      ghost.style.transformOrigin = '0 0';
      document.body.appendChild(ghost);
      el.setPointerCapture?.(e.pointerId);
      const move = (ev) => {
        ghost.style.left = `${ev.clientX - gw * gs / 2}px`;
        ghost.style.top = `${ev.clientY - gh * gs / 2}px`;
        const rig = hitChip(ev);
        if (rig !== hoverRig) {
          if (hoverRig) opts.previewDrop(hoverRig, false);
          hoverRig = rig;
          if (hoverRig) opts.previewDrop(hoverRig, true);
          document.body.style.cursor = hoverRig ? 'copy' : 'grabbing';
        }
      };
      const up = (ev) => {
        window.removeEventListener('pointermove', move);
        window.removeEventListener('pointerup', up);
        ghost.remove();
        el.classList.remove('dragging');
        slotEl.classList.remove('hot');
        document.body.style.cursor = '';
        if (hoverRig) { opts.previewDrop(hoverRig, false); hoverRig = null; }
        // 聚焦层：投入动作卡骰位
        if (data.kind === 'die' && hitSlot(ev)) {
          slotFilledBy = data;
          slotEl.textContent = String(data.value);
          slotEl.classList.add('filled');
          el.classList.add('spent');
          toast(`行动 〔${data.who}〕 · 对〔${opts.actionLoc()}〕投入 骰子·${data.value}`);
          opts.onDrop?.({ loc: opts.actionLoc(), data });
          return;
        }
        // 世界层：投到地标标签
        const rig = hitChip(ev);
        if (rig) {
          if (data.kind === 'die') el.classList.add('spent');
          else { el.classList.add('gone'); el.style.display = 'none'; }
          const what = data.kind === 'die' ? `骰子·${data.value}` : data.name;
          const who = data.kind === 'die' ? `〔${data.who}〕 ` : '';
          toast(`行动 ${who}· 对〔${rig.name}〕投入 ${what}`);
          opts.onDrop?.({ loc: rig.name, data });
        }
      };
      window.addEventListener('pointermove', move);
      window.addEventListener('pointerup', up);
    });
  });

  backBtn.addEventListener('click', () => opts.onBack?.());

  /* ---------- 动作卡每帧定位（屏幕像素 → 设计画布 px） ---------- */
  function placeAction(screen) {
    if (!screen || !actionOn) { leaderSvg.innerHTML = ''; return; }
    const s = opts.hudScale();
    const ax = screen.ax / s, ay = screen.ay / s; // 锚点在设计画布中的位置
    const ch = actionEl.offsetHeight || 120;
    let cx = ax + 96, cy = ay - ch * 0.45;
    cx = Math.max(cx, 12);
    cy = Math.max(cy, 12), cy = Math.min(cy, 968 - ch - 12);
    actionEl.style.left = `${cx}px`;
    actionEl.style.top = `${cy}px`;
    const attachY = cy + 30;
    leaderSvg.innerHTML =
      `<polyline points="${ax},${ay} ${cx - 14},${attachY} ${cx},${attachY}"/>` +
      `<circle cx="${ax}" cy="${ay}" r="2.5"/>`;
  }

  return {
    setView(mode) {
      backBtn.classList.toggle('hidden', mode !== 'focused');
    },
    setCrumb,
    showAction,
    placeAction,
    toast,
    get actionOn() { return actionOn; },
  };
}
