/* 世界视角的 HUD：案卷语言（与标注同一套视觉语法——暗卡、细边、打孔刻痕、
   宋体加字距、纸白常态 / 琥珀交互 / 红色唯一强调）。
   布局：左上=导航+任务，右上=功能菜单，左下=人物与行动骰子，右下=随身物品。
   HUD 是 HTML 覆盖层（拖拽需要 DOM），但样式对齐场景内的标注卡片；
   根容器不拦截指针，只有面板本身接收事件，画布的轨道控制不受影响。 */

const SVG_ICONS = {
  mail: '<svg viewBox="0 0 24 24"><rect x="3" y="6" width="18" height="12" rx="1"/><path d="M3.5 7l8.5 6 8.5-6"/></svg>',
  flask: '<svg viewBox="0 0 24 24"><path d="M10 3h4M11 3v5l-5 9a2 2 0 0 0 1.8 3h8.4a2 2 0 0 0 1.8-3l-5-9V3"/><path d="M8.5 15h7"/></svg>',
  watch: '<svg viewBox="0 0 24 24"><circle cx="12" cy="13" r="7"/><path d="M12 10v3.5l2.5 1.5M10 3h4M10 21h4"/></svg>',
  key: '<svg viewBox="0 0 24 24"><circle cx="8" cy="8" r="4"/><path d="M11 11l9 9M17 17l2-2M14.5 19.5l2-2"/></svg>',
};

const CHARS = [
  { name: '侦探', sub: '私家侦探 · 你', dice: [4, 2, 6], kind: 'sleuth' },
  { name: '夜莺', sub: '歌女 · 同行', dice: [5, 1], kind: 'nightingale' },
];

const ITEMS = [
  { name: '勒索信', icon: 'mail' },
  { name: '酒壶', icon: 'flask' },
  { name: '怀表', icon: 'watch' },
  { name: '万能钥匙', icon: 'key' },
];

/* 半身像占位：黑色电影剪影（礼帽侦探 / 波浪发歌女），单侧冷银轮廓光。 */
function bustCanvas(kind) {
  const c = document.createElement('canvas');
  c.width = 240; c.height = 200;
  const x = c.getContext('2d');
  const g = x.createLinearGradient(0, 0, 0, 200);
  g.addColorStop(0, '#131824');
  g.addColorStop(1, '#05070a');
  x.fillStyle = g; x.fillRect(0, 0, 240, 200);
  x.fillStyle = '#04060a';
  x.strokeStyle = 'rgba(140,150,170,0.5)';
  x.lineWidth = 2;
  if (kind === 'sleuth') {
    x.beginPath();                                   // 肩
    x.moveTo(28, 200); x.quadraticCurveTo(50, 142, 96, 134);
    x.lineTo(144, 134); x.quadraticCurveTo(190, 142, 212, 200);
    x.fill(); x.stroke();
    x.beginPath();                                   // 礼帽冠
    x.moveTo(88, 96); x.quadraticCurveTo(90, 58, 120, 56);
    x.quadraticCurveTo(150, 58, 152, 96); x.closePath();
    x.fill(); x.stroke();
    x.beginPath();                                   // 帽檐
    x.ellipse(120, 100, 58, 12, 0, 0, Math.PI * 2);
    x.fill(); x.stroke();
    x.fillRect(102, 100, 36, 30);                    // 脸的下半（阴影里）
  } else {
    x.beginPath();                                   // 肩
    x.moveTo(30, 200); x.quadraticCurveTo(56, 146, 100, 138);
    x.lineTo(140, 138); x.quadraticCurveTo(184, 146, 210, 200);
    x.fill(); x.stroke();
    x.beginPath();                                   // 波浪发
    x.moveTo(84, 128); x.quadraticCurveTo(70, 80, 96, 58);
    x.quadraticCurveTo(120, 40, 144, 58);
    x.quadraticCurveTo(170, 80, 156, 128);
    x.quadraticCurveTo(148, 108, 120, 104);
    x.quadraticCurveTo(92, 108, 84, 128);
    x.fill(); x.stroke();
    x.beginPath(); x.arc(146, 118, 3.5, 0, Math.PI * 2);  // 耳环（唯一的一点琥珀）
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
  host.innerHTML = `
    <div class="hud-tl">
      <div class="hud-nav">
        <button id="hud-back" class="hud-back" disabled>‹ 返回</button>
        <div class="hud-breadcrumb"><span>世界</span><span class="hud-separator">/</span><strong id="hud-location">城市</strong></div>
      </div>
      <button class="hud-mission" id="hud-mission" aria-expanded="false" aria-controls="hud-mission-detail">
        <span class="hud-mission-mark"></span><span>码头疑云</span><span class="hud-mission-progress">1 / 3</span><span class="hud-mission-chevron">⌄</span>
      </button>
      <div class="hud-mission-detail" id="hud-mission-detail" hidden>
        <strong>查清夜间货物的去向</strong><p>去老街酒馆，找夜班的司机。</p>
        <span>下一步 · 调查码头货单</span>
      </div>
      <div class="hud-log" id="hud-log"></div>
    </div>
    <div class="hud-tr">
      ${['档', '图', '设'].map((t) => `<button class="hud-menu card" title="${({档:'档案',图:'地图',证:'证物',设:'设置'})[t]}">${t}</button>`).join('')}

    </div>
    <div class="hud-bl">
      <div class="hud-label">在场人物</div>
      <div class="hud-chars" id="hud-chars"></div>
    </div>
    <div class="hud-br">
      <div class="hud-label">随身物品</div>
      <div class="hud-items" id="hud-items"></div>
    </div>
  `;

  const log = host.querySelector('#hud-log');
  const backBtn = host.querySelector('#hud-back');
  const mission=host.querySelector('#hud-mission');
  const missionDetail=host.querySelector('#hud-mission-detail');
  mission.addEventListener('click',()=>{missionDetail.hidden=!missionDetail.hidden;mission.setAttribute('aria-expanded',String(!missionDetail.hidden));});

  function toast(text) {
    const line = document.createElement('div');
    line.className = 'hud-log-line';
    line.textContent = text;
    log.prepend(line);
    while (log.children.length > 3) log.lastChild.remove();
    setTimeout(() => { line.classList.add('fade'); }, 3600);
    setTimeout(() => { line.remove(); }, 5000);
  }

  /* ---------- 人物卡 + 骰子 ---------- */
  const charsEl = host.querySelector('#hud-chars');
  const draggables = [];
  CHARS.forEach((ch) => {
    const card = document.createElement('div');
    card.className = 'hud-char card';
    const bust = document.createElement('canvas');
    bust.className = 'hud-bust';
    bust.width = 240; bust.height = 200;
    bust.getContext('2d').drawImage(bustCanvas(ch.kind), 0, 0);
    const nameRow = document.createElement('div');
    nameRow.className = 'hud-char-name';
    nameRow.innerHTML = `<span>${ch.name}</span><i>${ch.sub}</i>`;
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
    card.append(bust, nameRow, diceRow);
    charsEl.appendChild(card);
  });

  /* ---------- 物品 ---------- */
  const itemsEl = host.querySelector('#hud-items');
  ITEMS.forEach((it) => {
    const el = document.createElement('div');
    el.className = 'hud-item card';
    el.title = it.name;
    el.innerHTML = SVG_ICONS[it.icon];
    itemsEl.appendChild(el);
    draggables.push({ el, data: { kind: 'item', name: it.name } });
  });

  // Visual preview only: select slots; no dragging, spending or removal.
  draggables.forEach(({el,data})=>{
    el.setAttribute('role','button');el.tabIndex=0;
    el.setAttribute('aria-label',data.kind==='die'?`${data.who}行动骰 ${data.value}`:data.name);
    function select(){const wasSelected=el.classList.contains('selected');host.querySelectorAll('.selected').forEach(e=>e.classList.remove('selected'));el.classList.toggle('selected',!wasSelected);}
    el.addEventListener('click',select);el.addEventListener('keydown',e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();select();}});
  });
  backBtn.addEventListener('click', () => opts.onBack?.());
  host.querySelectorAll('.hud-menu').forEach(button=>button.addEventListener('click',()=>opts.onMenu?.(button.textContent)));

  return {
    setView(mode,name) {
      backBtn.disabled=mode!=='focused';
      backBtn.title=mode==='world'?'已在城市视角':'返回城市';
      missionDetail.hidden=true;mission.setAttribute('aria-expanded','false');
      host.querySelector('#hud-location').textContent=mode==='world'?'城市':name;
    },
    toast,
  };
}
