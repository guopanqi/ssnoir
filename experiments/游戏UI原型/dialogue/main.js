import { mountPortraitEffects } from './portrait-effects.js?v=4';
import { opening, responses } from './sample.js?v=5';
import { cast, displayName, mountPortraitEditor, renderPortraits } from './portraits.js?v=6';

const game = document.querySelector('#game');
const sideButton = document.querySelector('#dialogue-preview');
const bottomButton = document.querySelector('#dialogue-bottom-preview');
const speedControl = document.querySelector('#dialogue-speed');
const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
const state = { phase: 'closed', layout: 'bottom', queue: [], cursor: 0, entries: [], branch: false, auto: false, history: false, revealed: 0, elapsed: 0, readingTime: 0 };
let overlay, log, footer, currentText, currentItem, frame, epoch = 0, lastTime = 0;
const inertBefore = new Map();
let portraitEffects;

function phase(value) {
  state.phase = value;
  if (overlay) {
    overlay.dataset.phase = value;
    const historyButton = overlay.querySelector('[data-history]');
    if (historyButton) historyButton.disabled = ['entering', 'exiting'].includes(value);
  }
}
function actionButton(text, label, action) {
  const button = document.createElement('button'); button.type = 'button'; button.textContent = text;
  if (label) button.setAttribute('aria-label', label);
  button.onclick = action; return button;
}
function focusWithoutInterrupting(element) {
  if (!document.activeElement?.closest('#portrait-settings, input, select, textarea')) element?.focus({ preventScroll: true });
}
function refreshCast() {
  portraitEffects?.sync();
  if (!overlay) return;
  renderPortraits(overlay.querySelector('.dialogue-stage'));
  portraitEffects?.applyStage(overlay.querySelector('.dialogue-stage'));
  overlay.querySelectorAll('[data-name-for]').forEach(element => { element.textContent = displayName(element.dataset.nameFor); });
  overlay.setAttribute('aria-label', `${cast.map(actor => actor.name).join('与')}的对话`);
}
mountPortraitEditor(document.querySelector('#portrait-settings'), refreshCast);
portraitEffects = mountPortraitEffects(document.querySelector('#portrait-effects'), cast, refreshCast);

function selectLayout(layout) {
  state.layout = layout;
  if (overlay) overlay.classList.toggle('dialogue-bottom', layout === 'bottom');
  sideButton.setAttribute('aria-pressed', String(!!overlay && layout === 'side'));
  bottomButton.setAttribute('aria-pressed', String(!!overlay && layout === 'bottom'));
  if (log) log.scrollTop = log.scrollHeight;
}
function animate(element, keyframes, options = {}) {
  return element.animate(keyframes, { duration: reducedMotion.matches ? 1 : 500, easing: 'cubic-bezier(.2,.8,.2,1)', fill: 'both', ...options, ...(reducedMotion.matches ? { duration: 1, delay: 0 } : {}) });
}
async function moveStage(entering) {
  const animations = [];
  for (const figure of overlay.querySelectorAll('.dialogue-figure')) {
    const offscreen = `translateX(${figure.classList.contains('left') ? '-' : ''}110%)`;
    const from = entering ? { transform: offscreen, opacity: 0 } : { transform: getComputedStyle(figure).transform, opacity: getComputedStyle(figure).opacity };
    figure.getAnimations().forEach(animation => animation.cancel());
    animations.push(animate(figure, [from, { transform: entering ? 'translateX(0)' : offscreen, opacity: entering ? 1 : 0 }], { duration: entering ? 660 : 400, delay: entering && figure.classList.contains('right') ? 85 : 0 }));
  }
  const panel = overlay.querySelector('.dialogue-panel');
  animations.push(animate(panel, entering ? [{ opacity: 0, transform: 'translateY(22px)' }, { opacity: 1, transform: 'translateY(0)' }] : [{ opacity: 1 }, { opacity: 0, transform: 'translateY(14px)' }], { duration: entering ? 360 : 220, delay: entering ? 240 : 0 }));
  await Promise.all(animations.map(animation => animation.finished.catch(() => {})));
  if (entering) animations.forEach(animation => animation.cancel());
}
function footerState() {
  const hadFocus = footer.contains(document.activeElement);
  footer.replaceChildren();
  if (state.phase === 'choice') { showChoices(); return; }
  const ended = state.phase === 'ended';
  const button = actionButton('', ended ? '返回场景' : state.phase === 'typing' ? '显示完整对白' : '继续对话', advance);
  button.className = 'dialogue-next';
  const hint = document.createElement('small');
  hint.textContent = ended ? '对话结束' : state.phase === 'typing' ? '点击补全' : '点击 / 空格继续';
  const label = document.createElement('span'); label.textContent = ended ? '返回场景' : state.phase === 'typing' ? '补全' : '继续';
  button.append(hint, label); button.disabled = ['entering', 'exiting', 'choosing'].includes(state.phase);
  footer.append(button);
  if (hadFocus && !button.disabled) focusWithoutInterrupting(button);
}
function finishText() {
  if (!currentText) return;
  currentText.textContent = currentItem.text;
  state.revealed = [...currentItem.text].length; state.elapsed = 0; state.readingTime = 0;
  if (state.cursor === state.queue.length) phase(state.branch ? 'ended' : 'choice');
  else phase('reading');
  footerState();
  log.scrollTop = log.scrollHeight;
}
function nextTurn() {
  const item = state.queue[state.cursor++];
  if (!item) return;
  currentItem = item;
  log.querySelectorAll('.current').forEach(element => element.classList.remove('current'));
  const entry = document.createElement('article'); entry.className = `dialogue-turn current${item.narration ? ' narration' : ''}`;
  if (!item.narration) {
    const name = document.createElement('div'); name.className = 'dialogue-speaker'; name.dataset.nameFor = item.who; name.textContent = displayName(item.who); entry.append(name);
  }
  const p = document.createElement('p');
  const accessible = document.createElement('span'); accessible.className = 'dialogue-sr-only'; accessible.textContent = item.text;
  currentText = document.createElement('span'); currentText.setAttribute('aria-hidden', 'true'); p.append(accessible, currentText); entry.append(p); log.append(entry);
  state.entries.push(item);
  for (const figure of overlay.querySelectorAll('.dialogue-figure')) {
    figure.classList.toggle('active', !item.narration && figure.dataset.speaker === item.who);
    figure.classList.toggle('narrating', !!item.narration);
  }
  state.revealed = 0; state.elapsed = 0; state.readingTime = 0; phase('typing'); footerState();
  if (Number(speedControl.value) === 0 || reducedMotion.matches) finishText();
  log.scrollTop = log.scrollHeight;
}
function advance() {
  if (state.history) return;
  if (state.phase === 'typing') finishText();
  else if (state.phase === 'reading') nextTurn();
  else if (state.phase === 'ended') close();
}
function showChoices() {
  const list = document.createElement('div'); list.className = 'dialogue-choices'; list.setAttribute('role', 'group'); list.setAttribute('aria-label', '选择回应');
  responses.forEach((response, index) => {
    const button = actionButton('', response.label, async () => {
      if (state.phase !== 'choice') return;
      phase('choosing'); const token = epoch;
      list.querySelectorAll('button').forEach(item => { item.disabled = true; }); button.classList.add('chosen');
      await animate(list, [{ opacity: 1 }, { opacity: 0, transform: 'translateY(5px)' }], { duration: 180 }).finished.catch(() => {});
      if (token !== epoch || !overlay) return;
      state.queue = response.lines; state.cursor = 0; state.branch = true; nextTurn();
      focusWithoutInterrupting(footer.querySelector('button'));
    });
    const number = document.createElement('span'); number.className = 'choice-number'; number.textContent = `${index + 1}`;
    const text = document.createElement('span'); text.textContent = response.label; button.append(number, text); list.append(button);
  });
  footer.append(list);
  animate(list, [{ opacity: 0, transform: 'translateY(10px)' }, { opacity: 1, transform: 'translateY(0)' }], { duration: 260 });
  focusWithoutInterrupting(list.querySelector('button'));
}
function showHistory(show) {
  if (!overlay || ['entering', 'exiting'].includes(state.phase)) return;
  state.history = show;
  const sheet = overlay.querySelector('.dialogue-history'); sheet.hidden = !show;
  for (const element of overlay.children) if (element !== sheet) element.inert = show;
  overlay.querySelector('[data-history]').setAttribute('aria-expanded', String(show));
  if (show) {
    const list = sheet.querySelector('.history-entries'); list.replaceChildren();
    for (const item of state.entries) {
      const row = document.createElement('article'); row.className = item.narration ? 'history-turn narration' : 'history-turn';
      const name = document.createElement('strong'); name.textContent = item.narration ? '旁白' : displayName(item.who);
      if (!item.narration) name.dataset.nameFor = item.who;
      const p = document.createElement('p'); p.textContent = item.text; row.append(name, p); list.append(row);
    }
    list.scrollTop = list.scrollHeight; sheet.querySelector('[data-history-close]').focus({ preventScroll: true });
  } else overlay.querySelector('[data-history]').focus({ preventScroll: true });
}
function tick(time) {
  if (!overlay) return;
  const delta = lastTime ? Math.min(64, time - lastTime) : 0; lastTime = time;
  const paused = state.history || document.hidden || document.activeElement?.closest('#portrait-settings input, #portrait-settings select');
  if (!paused && state.phase === 'typing') {
    const characters = [...currentItem.text]; const speed = Number(speedControl.value);
    if (speed === 0 || reducedMotion.matches) finishText();
    else {
      state.elapsed += delta;
      while (state.revealed < characters.length) {
        const previous = characters[state.revealed - 1] || '';
        const delay = /[。！？…]/.test(previous) ? speed + 150 : /[，、；]/.test(previous) ? speed + 65 : speed;
        if (state.elapsed < delay) break;
        state.elapsed -= delay; state.revealed++;
      }
      currentText.textContent = characters.slice(0, state.revealed).join('');
      if (state.revealed >= characters.length) finishText();
    }
  } else if (!paused && state.auto && state.phase === 'reading') {
    state.readingTime += delta;
    if (state.readingTime > Math.min(4500, Math.max(1500, currentItem.text.length * 65))) nextTurn();
  }
  frame = requestAnimationFrame(tick);
}
function destroy() {
  epoch++; cancelAnimationFrame(frame); lastTime = 0;
  if (overlay) { overlay.getAnimations({ subtree: true }).forEach(animation => animation.cancel()); overlay.remove(); }
  overlay = null; currentText = null; state.history = false;
  for (const [element, inert] of inertBefore) element.inert = inert;
  inertBefore.clear(); game.classList.remove('dialogue-open');
}
async function close() {
  if (!overlay || state.phase === 'exiting') return;
  state.history = false; overlay.querySelector('.dialogue-history').hidden = true;
  phase('exiting'); footerState(); const token = epoch;
  await moveStage(false);
  if (token !== epoch) return;
  destroy(); phase('closed'); document.body.classList.remove('dialogue-mode');
  document.querySelector('#action-tab').setAttribute('aria-pressed', 'true'); document.querySelector('#dialogue-tab').setAttribute('aria-pressed', 'false');
  sideButton.setAttribute('aria-pressed', 'false'); bottomButton.setAttribute('aria-pressed', 'false');
  document.querySelector('#action-tab').focus({ preventScroll: true });
}
async function open(layout = state.layout) {
  if (overlay && state.phase !== 'exiting') { selectLayout(layout); return; }
  if (overlay) destroy();
  const token = ++epoch;
  document.body.classList.add('dialogue-mode');
  document.querySelector('#action-tab').setAttribute('aria-pressed', 'false'); document.querySelector('#dialogue-tab').setAttribute('aria-pressed', 'true');
  document.querySelector('#reset').click();
  for (const element of game.children) { inertBefore.set(element, element.inert); element.inert = true; }
  game.classList.add('dialogue-open');
  overlay = document.createElement('section'); overlay.className = 'dialogue-preview'; overlay.setAttribute('role', 'region');
  overlay.innerHTML = `<div class="dialogue-stage"><div class="dialogue-atmosphere"></div><div class="dialogue-reference"></div></div>
    <header class="dialogue-topbar"><div class="dialogue-location"><span>老街酒馆</span><small>散场以后</small></div><div class="dialogue-tools"><button type="button" data-auto aria-pressed="${state.auto}">自动</button><button type="button" data-history aria-expanded="false">回看</button><button type="button" data-close>返回场景</button></div></header>
    <section class="dialogue-panel"><div class="dialogue-panel-top">散场以后 <span>对话记录</span></div><div class="dialogue-log" role="log" aria-live="polite" aria-relevant="additions" aria-label="对话记录" tabindex="0"></div><div class="dialogue-footer"></div></section>
    <section class="dialogue-history" role="dialog" aria-label="回看对话记录" hidden><div class="history-backdrop"></div><div class="history-sheet"><header><span>对话记录</span><button type="button" data-history-close>返回对话</button></header><div class="history-entries" tabindex="0"></div><small>回看期间暂停播放 · Esc 返回</small></div></section>`;
  game.append(overlay); log = overlay.querySelector('.dialogue-log'); footer = overlay.querySelector('.dialogue-footer');
  Object.assign(state, { queue: opening, cursor: 0, entries: [], branch: false, history: false });
  refreshCast(); selectLayout(layout); phase('entering'); footerState();
  overlay.querySelector('[data-auto]').onclick = event => { state.auto = !state.auto; state.readingTime = 0; event.currentTarget.setAttribute('aria-pressed', String(state.auto)); };
  overlay.querySelector('[data-history]').onclick = () => showHistory(!state.history);
  overlay.querySelector('[data-history-close]').onclick = () => showHistory(false);
  overlay.querySelector('.history-backdrop').onclick = () => showHistory(false);
  overlay.querySelector('[data-close]').onclick = close;
  overlay.querySelector('.dialogue-panel').onclick = event => { if (!event.target.closest('button') && !window.getSelection().toString()) advance(); };
  frame = requestAnimationFrame(tick);
  await moveStage(true);
  if (token !== epoch || !overlay || state.phase !== 'entering') return;
  nextTurn(); focusWithoutInterrupting(footer.querySelector('button'));
}
sideButton.onclick = () => open('side'); bottomButton.onclick = () => open('bottom');
document.querySelector('#dialogue-replay').onclick = () => { const layout = state.layout; destroy(); open(layout); };
document.querySelector('#action-tab').onclick = close;
document.querySelector('#dialogue-tab').onclick = () => open(state.layout);
speedControl.onchange = () => { if (state.phase === 'typing' && Number(speedControl.value) === 0) finishText(); };
document.addEventListener('keydown', event => {
  if (!overlay || event.isComposing || event.target.closest('input, select, textarea, summary')) return;
  if (event.repeat) { if (['Space', 'Enter'].includes(event.code)) event.preventDefault(); return; }
  if (event.key === 'Escape') { event.preventDefault(); state.history ? showHistory(false) : close(); return; }
  if (state.history) return;
  if (state.phase === 'choice') {
    const buttons = [...footer.querySelectorAll('.dialogue-choices button')];
    if (/^[12]$/.test(event.key)) { event.preventDefault(); buttons[Number(event.key) - 1]?.click(); return; }
    if (['ArrowDown', 'ArrowUp', 'ArrowLeft', 'ArrowRight'].includes(event.key)) {
      event.preventDefault(); const index = buttons.indexOf(document.activeElement); const offset = ['ArrowDown', 'ArrowRight'].includes(event.key) ? 1 : -1;
      buttons[(index + offset + buttons.length) % buttons.length]?.focus(); return;
    }
  }
  if (['Space', 'Enter'].includes(event.code) && !event.target.closest('button,a')) { event.preventDefault(); advance(); }
});
if (new URLSearchParams(location.search).get('experiment') === 'dialogue') open('bottom');
