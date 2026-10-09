import './style.css';
import { stories, cast } from './stories.js';
import { Stage, Sound } from './stage.js';
import { Player } from './player.js';
const app = document.querySelector('#app');
app.innerHTML = `<main class="lab"><header><div><span class="eyebrow">SSNOIR / PORTRAIT THEATRE</span><h1>立绘剧场<span>表达力实验</span></h1></div><div class="header-note">少量动作 · 完整的情绪<br>七组短戏 / A·B 对照</div></header><section class="viewport" aria-label="舞台"><div class="stage" tabindex="0" aria-label="点击推进对白"><div class="scenery"></div><div class="scene-heading"><span class="scene-subtitle"></span><h2 class="scene-title"></h2></div><div class="actors"></div><div class="fx"></div><div class="dialogue" hidden><div class="speaker"></div><div class="words" aria-live="polite"></div><div class="continue">点击继续 <span>↵</span></div></div><div class="end-card" hidden><small>幕落</small><strong>这场戏，到这里。</strong><div class="questions"></div><div class="end-buttons"><button id="compare">看另一版本</button><button id="next-scene">看下一场</button><button id="again">重新试演</button></div></div></div><div class="loading">正在准备演员……</div></section><div class="controls"><div class="scene-tabs" role="group" aria-label="选择场次">${stories.map((s, i) => `<button data-story="${i}" class="${i === 3 ? 'selected' : ''}">${i < 3 ? '原场' : `实验 ${i - 2}`} <span>${s.title}</span></button>`).join('')}</div><div class="play-controls"><button id="restart">重播</button><button id="pause">暂停</button><button id="sound" aria-pressed="false">声音：关</button><button id="debug" aria-expanded="false">试演面板</button></div></div><section class="study-controls"><div class="variants" role="group" aria-label="对照版本"></div><div class="view-options"><label><input id="autoplay" type="checkbox">自动播放</label><label><input id="hide-captions" type="checkbox">隐藏字幕观察</label></div></section><footer><p id="scene-note"></p><span id="status"></span></footer><section class="debug-panel" hidden><div class="debug-toolbar"><label>跳到拍点 <select id="beat-select"></select></label><button id="jump">跳转</button><button id="next-beat">下一拍</button><button id="continue-button">推进对白</button></div><ol id="beats"></ol><pre id="snapshot"></pre></section><p class="help">点击舞台 / Enter / 空格推进对白 · P 暂停 · R 重播。隐藏字幕会自动播放，并隐藏场内标题及解释；没有角色配音，因此只检验画面，不用于判断对白或声音表演。</p></main>`;
const viewport = app.querySelector('.viewport');
const root = app.querySelector('.stage');
new ResizeObserver(() => { const scale = viewport.clientWidth / 1280; root.style.transform = `scale(${scale})`; viewport.style.height = `${720 * scale}px`; }).observe(viewport);
const stage = new Stage(root);
const sound = new Sound(); stage.sound = sound;
let current = 3, variant = 0, debugVisible = false, loaded = false;
const player = new Player(stage, p => {
  app.querySelector('#pause').textContent = p.paused ? '继续' : '暂停';
  app.querySelector('#status').textContent = p.state === 'error' ? `演出错误：${p.error}` : p.state === 'ended' ? '本场结束' : `${String(p.beat + 1).padStart(2, '0')} / ${String(p.story.beats.length).padStart(2, '0')} · ${p.paused ? '已暂停' : p.state === 'dialogue' ? p.auto ? '自动播放' : '等候对白推进' : '演出中'}`;
  app.querySelector('.end-card').hidden = p.state !== 'ended';
  if (p.state === 'ended') stage.silence();
  app.querySelectorAll('#beats li').forEach((el, i) => el.classList.toggle('active', i === p.beat));
  app.querySelector('#beat-select').value = String(p.beat);
  app.querySelector('#snapshot').textContent = JSON.stringify(stage.snapshot(), null, 2);
});
function resolved(index = current, selectedVariant = variant) {
  const story = stories[index];
  return story.variants ? { ...story, ...story.variants[selectedVariant] } : story;
}
function refreshOptions() {
  const hidden = app.querySelector('#hide-captions').checked;
  const auto = app.querySelector('#autoplay');
  auto.disabled = hidden;
  player.auto = hidden || auto.checked;
  root.classList.toggle('observe-only', hidden);
  app.querySelector('#scene-note').textContent = hidden ? '先看发生了什么。幕落后，再回答观察问题。' : stories[current].note;
  app.querySelector('.variants').innerHTML = (stories[current].variants || []).map((v, i) => `<button data-variant="${i}" aria-pressed="${i === variant}" class="${i === variant ? 'selected' : ''}">${v.label}${hidden ? '' : ` · ${v.description}`}</button>`).join('');
}
function load(index, beat = 0, selectedVariant = index === current ? variant : 0) {
  if (!loaded) return;
  current = index; variant = selectedVariant;
  const story = resolved();
  app.querySelector('.scene-title').textContent = story.title;
  app.querySelector('.scene-subtitle').textContent = `${story.subtitle}${story.label ? ` / ${story.label}` : ''}`;
  app.querySelectorAll('[data-story]').forEach((el, i) => el.classList.toggle('selected', i === index));
  app.querySelector('#beat-select').innerHTML = story.beats.map((b, i) => `<option value="${i}">${i + 1} · ${b.label}</option>`).join('');
  app.querySelector('#beats').innerHTML = story.beats.map((b, i) => `<li><button data-beat="${i}"><span>${String(i + 1).padStart(2, '0')}</span>${b.label}</button></li>`).join('');
  app.querySelector('.questions').innerHTML = (story.questions || []).map(q => `<p>${q}</p>`).join('');
  app.querySelector('#compare').hidden = !story.variants;
  refreshOptions();
  player.load(story, beat);
}
app.addEventListener('click', e => {
  const scene = e.target.closest('[data-story]'); if (scene) load(Number(scene.dataset.story));
  const beat = e.target.closest('[data-beat]'); if (beat) load(current, Number(beat.dataset.beat));
  const version = e.target.closest('[data-variant]'); if (version) load(current, 0, Number(version.dataset.variant));
});
root.addEventListener('click', e => { if (!e.target.closest('button')) player.advance(); });
app.querySelector('#restart').onclick = () => load(current);
app.querySelector('#again').onclick = () => load(current);
app.querySelector('#compare').onclick = () => load(current, 0, (variant + 1) % stories[current].variants.length);
app.querySelector('#next-scene').onclick = () => load((current + 1) % stories.length);
app.querySelector('#pause').onclick = () => { if (loaded) player.pause(); };
app.querySelector('#sound').onclick = async e => { await sound.enable(!sound.enabled); e.target.textContent = `声音：${sound.enabled ? '开' : '关'}`; e.target.setAttribute('aria-pressed', String(sound.enabled)); };
app.querySelector('#debug').onclick = e => { debugVisible = !debugVisible; app.querySelector('.debug-panel').hidden = !debugVisible; e.target.setAttribute('aria-expanded', String(debugVisible)); };
app.querySelector('#jump').onclick = () => load(current, Number(app.querySelector('#beat-select').value));
app.querySelector('#next-beat').onclick = () => load(current, Math.min(player.beat + 1, resolved().beats.length - 1));
app.querySelector('#continue-button').onclick = () => player.advance();
app.querySelector('#autoplay').onchange = () => { refreshOptions(); if (loaded) player.update(); };
app.querySelector('#hide-captions').onchange = () => { refreshOptions(); if (loaded) load(current); };
document.addEventListener('keydown', e => { if (!loaded || e.target.closest('button, select, input')) return; if (['Enter', ' '].includes(e.key)) { e.preventDefault(); player.advance(); } if (e.key.toLowerCase() === 'p') player.pause(); if (e.key.toLowerCase() === 'r') load(current); });
async function prepare() {
  try {
    await Promise.all(Object.values(cast).flatMap(c => Object.values(c.poses)).map(src => new Promise((resolve, reject) => { const img = new Image(); img.onload = resolve; img.onerror = () => reject(Error(`无法加载素材 ${src}`)); img.src = src; })));
    loaded = true; app.querySelector('.loading').hidden = true; load(current);
  } catch (error) { app.querySelector('.loading').textContent = error.message; console.error(error); }
}
prepare();
window.stageLab = { player, stage, stories, load, resolved };
