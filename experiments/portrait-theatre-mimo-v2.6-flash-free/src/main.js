// 页面装配：幕布、片段导航、控制条、说明区、键盘与舞台缩放。
(() => {
  const $ = (sel) => document.querySelector(sel);
  const stageEl = $('#stage');
  const wrapEl = $('#stageWrap');
  const curtain = $('#curtain');
  const card = $('#curtainCard');

  const stage = new PT.Stage(stageEl);

  const ui = {
    scenes: $('#scenes'),
    progress: $('#progress'),
    info: $('#info'),
    toggle: $('[data-act="toggle"]'),
    replay: $('[data-act="replay"]'),
    prev: $('[data-act="prev"]'),
    next: $('[data-act="next"]'),
    mute: $('[data-act="mute"]'),
  };

  const player = new PT.Player(stage, {
    onState(snap) {
      ui.toggle.textContent = snap.playing ? '暂停 (P)' : '继续 (P)';
      ui.toggle.disabled = !snap.started;
      ui.progress.textContent = snap.total
        ? `第 ${Math.max(1, snap.beatIndex + 1)} / ${snap.total} 拍`
        : `${snap.total} 拍`;
      [...ui.scenes.children].forEach((b, i) => b.classList.toggle('active', i === snap.sceneIndex));
      const s = PT.SCENES[snap.sceneIndex];
      if (s) renderInfo(s);
    },
    onEnd(index) {
      const next = index + 1;
      closeCurtain(`
        <h2>片段结束</h2>
        <p>${PT.SCENES[index].title}<br>${PT.SCENES[index].setting}</p>
        <div class="actions">
          <button data-curtain="replay">重看这一段 (R)</button>
          ${next < PT.SCENES.length ? `<button class="primary" data-curtain="next">下一片段</button>` : ''}
        </div>`);
      card.querySelector('[data-curtain="replay"]').onclick = () => { player.replay(); openCurtain(); };
      const nb = card.querySelector('[data-curtain="next"]');
      if (nb) nb.onclick = () => { player.openScene(next); openCurtain(); };
    },
  });

  // ---------- 幕布 ----------
  function closeCurtain(html) {
    if (html) card.innerHTML = html;
    curtain.classList.remove('open');
  }
  function openCurtain() { curtain.classList.add('open'); }

  function startShow() {
    PT.audio.init();
    PT.audio.setMuted(false);
    ui.mute.textContent = '声音：开';
    player.openScene(0);
    openCurtain();
  }

  // ---------- 片段导航 ----------
  PT.SCENES.forEach((s, i) => {
    const b = document.createElement('button');
    b.textContent = s.title.replace(/^片段/, '').replace(/：/, ' · ');
    b.onclick = () => {
      if (player.sceneIndex === i && player.started) { player.replay(); openCurtain(); return; }
      player.openScene(i);
      openCurtain();
    };
    ui.scenes.appendChild(b);
  });

  // ---------- 说明区 ----------
  function renderInfo(s) {
    const rows = [
      ['场景', s.setting],
      ['人物', s.cast],
      ...(s.propNote ? [['道具', s.propNote]] : []),
      ['作者说明', s.note],
    ];
    ui.info.innerHTML = rows
      .map(([k, v]) => `<div class="card"><h3>${k}</h3><p>${v}</p></div>`)
      .join('');
  }

  // ---------- 控制条 ----------
  ui.toggle.onclick = () => player.toggle();
  ui.replay.onclick = () => { player.replay(); openCurtain(); };
  ui.prev.onclick = () => player.prev();
  ui.next.onclick = () => player.next();
  ui.mute.onclick = () => {
    const m = !PT.audio.muted;
    PT.audio.setMuted(m);
    ui.mute.textContent = m ? '声音：关' : '声音：开';
    ui.mute.classList.toggle('on', !m);
    if (!m && player.sceneIndex >= 0) PT.audio.rain(!!stage.state.fx.rain);
  };
  ui.mute.classList.add('on');

  stageEl.addEventListener('click', (e) => {
    if (e.target.closest('.curtain')) return;
    if (!curtain.classList.contains('open')) return;
    player.next();
  });

  document.addEventListener('keydown', (e) => {
    if (e.target.matches('input, textarea')) return;
    const k = e.key.toLowerCase();
    if (k === ' ' || k === 'enter') { e.preventDefault(); curtain.classList.contains('open') ? player.next() : null; }
    else if (k === 'p') player.toggle();
    else if (k === 'r') { player.replay(); openCurtain(); }
    else if (k === 'arrowright') player.next();
    else if (k === 'arrowleft') player.prev();
    else if (k === 'm') ui.mute.click();
    else if (k >= '1' && k <= '6') { const i = +k - 1; if (PT.SCENES[i]) { player.openScene(i); openCurtain(); } }
  });

  window.ptTheatre = { player, stage, curtain: () => curtain.classList.contains('open') };

  // ---------- 舞台缩放（设计分辨率 1280x720，任何窗口按宽度等比缩放） ----------
  function fit() {
    const s = wrapEl.clientWidth / 1280;
    stageEl.style.transform = `scale(${s})`;
    wrapEl.style.height = `${Math.round(720 * s)}px`;
  }
  window.addEventListener('resize', fit);
  fit();

  // ---------- 预载立绘 ----------
  const files = Object.values(PT.cast).map((c) => c.file);
  let left = files.length;
  const btn = document.createElement('button');
  btn.className = 'primary';
  btn.disabled = true;
  btn.textContent = `载入中… 0%`;
  btn.onclick = () => { if (!btn.disabled) startShow(); };
  card.innerHTML = `
    <h2>立绘剧场</h2>
    <p>Portrait Theatre · 六段基准剧本 · 纯色舞台与霓虹立绘<br>模型后缀 <b>mimo-v2.6-flash-free</b></p>
    <div class="actions"></div>`;
  card.querySelector('.actions').appendChild(btn);

  const done = () => {
    left -= 1;
    const pct = Math.round(((files.length - left) / files.length) * 100);
    btn.textContent = left > 0 ? `载入中… ${pct}%` : '开演';
    if (left <= 0) btn.disabled = false;
  };
  for (const f of files) {
    const img = new Image();
    img.onload = done;
    img.onerror = () => { btn.textContent = `素材载入失败: ${f}`; };
    img.src = f;
  }
  if (!files.length) { btn.disabled = false; btn.textContent = '开演'; }
})();
