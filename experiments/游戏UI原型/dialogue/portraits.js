import { neutralLook, renderPortraitLook } from './portrait-effects.js?v=4';
// 画像仅保留在当前预览会话；不写入游戏资源，也不上传文件。
const defaults = [
  { id: 'manager', speaker: '经理', name: '经理', side: 'left', mode: 'lines', src: new URL('../../../UnityClient/Assets/Resources/Portraits/Characters/经理.png', import.meta.url).href },
  { id: 'nightingale', speaker: '夜莺', name: '夜莺', side: 'right', src: new URL('../../../UnityClient/Assets/Resources/Portraits/Characters/夜莺.png', import.meta.url).href },
];
export const cast = defaults.map(actor => ({ ...actor, mode: actor.mode || 'original', scale: 100, offset: 0, flip: false, objectURL: null, revision: 0, look: { ...neutralLook }, compareOriginal: false }));
export function displayName(speaker) { return cast.find(actor => actor.speaker === speaker)?.name || speaker; }

export function renderPortraits(stage) {
  for (const actor of cast) {
    let figure = stage.querySelector(`[data-actor="${actor.id}"]`);
    if (!figure) {
      figure = document.createElement('div');
      figure.className = `dialogue-figure ${actor.side}`;
      figure.dataset.actor = actor.id; figure.dataset.speaker = actor.speaker;
      figure.setAttribute('aria-hidden', 'true');
      figure.innerHTML = '<div class="portrait-pose"><div class="portrait-surface"></div></div>';
      stage.append(figure);
    }
    const surface = figure.querySelector('.portrait-surface');
    surface.style.setProperty('--portrait-scale', actor.scale / 100);
    surface.style.setProperty('--portrait-offset', `${actor.offset}px`);
    surface.style.setProperty('--portrait-flip', actor.flip ? '-1' : '1');
    if (surface.dataset.src !== actor.src || surface.dataset.mode !== actor.mode) {
      surface.dataset.src = actor.src; surface.dataset.mode = actor.mode;
      const visual = document.createElement('div'); visual.className = 'portrait-visual'; surface.replaceChildren(visual);
      if (actor.mode === 'lines') {
        const mask = document.createElement('div'); mask.className = 'portrait-lines';
        mask.style.setProperty('--portrait-url', `url(${JSON.stringify(actor.src)})`);
        visual.append(mask);
      } else {
        const image = new Image(); image.src = actor.src; image.alt = ''; image.draggable = false;
        visual.append(image);
      }
    }
    renderPortraitLook(surface, actor);
  }
}

export function mountPortraitEditor(root, onChange) {
  root.innerHTML = '<summary>替换人物画像 <span>本地选图 · 缩放 · 镜像</span></summary><p class="portrait-help">选择图片或填写项目相对路径。推荐透明 PNG / WebP；不自动去除背景。图片仅用于当前预览，刷新后恢复默认。</p><div class="portrait-editors"></div>';
  for (const actor of cast) {
    const card = document.createElement('fieldset'); card.className = 'portrait-editor'; card.dataset.actorEditor = actor.id;
    card.innerHTML = `<legend>${actor.side === 'left' ? '左侧' : '右侧'}人物</legend>
      <div class="portrait-editor-top"><div class="portrait-thumb"></div><div class="portrait-file-actions"><label class="portrait-file-button">选择图片<input type="file" accept="image/png,image/jpeg,image/webp,image/avif" aria-label="替换${actor.speaker}画像"></label><button type="button" data-reset>恢复默认</button><small data-file-name>Characters 默认画像</small></div></div>
      <label>显示姓名<input data-name type="text" maxlength="12" value="${actor.name}" aria-label="${actor.speaker}显示姓名"></label>
      <div class="portrait-path"><label>图片地址<input data-path type="text" placeholder="项目相对路径或 https://…" aria-label="${actor.speaker}图片地址"></label><button type="button" data-load>应用</button></div>
      <label>显示方式<select data-mode aria-label="${actor.speaker}显示方式"><option value="lines">描线蒙版 · 黑底白线</option><option value="original">原图 · 保留颜色和透明度</option></select></label>
      <label class="portrait-range">画幅缩放<input data-scale type="range" min="60" max="150" value="100" aria-label="${actor.speaker}画像缩放"><output data-scale-value>100%</output></label>
      <label class="portrait-range">上下位置<input data-offset type="range" min="-120" max="120" value="0" aria-label="${actor.speaker}画像上下位置"><output data-offset-value>0</output></label>
      <label class="portrait-flip"><input data-flip type="checkbox" aria-label="镜像${actor.speaker}画像">左右镜像</label><p class="portrait-error" role="status" data-error></p>`;
    root.querySelector('.portrait-editors').append(card);
    const field = selector => card.querySelector(selector);
    const sync = () => {
      field('[data-mode]').value = actor.mode;
      field('[data-name]').value = actor.name;
      field('[data-scale]').value = actor.scale; field('[data-scale-value]').textContent = `${actor.scale}%`;
      field('[data-offset]').value = actor.offset; field('[data-offset-value]').textContent = actor.offset;
      field('[data-flip]').checked = actor.flip;
      const thumb = field('.portrait-thumb'); thumb.replaceChildren();
      const image = new Image(); image.src = actor.src; image.alt = `${actor.name}画像预览`; thumb.append(image);
      onChange();
    };
    async function load(src, { local = false, label = src } = {}) {
      const revision = ++actor.revision;
      card.setAttribute('aria-busy', 'true'); field('[data-error]').textContent = '';
      let timeout;
      try {
        const image = new Image(); image.src = src;
        await Promise.race([image.decode(), new Promise((_, reject) => { timeout = setTimeout(() => reject(new Error("图片读取超时")), 12000); })]);
        if (actor.revision !== revision) { if (local) URL.revokeObjectURL(src); return; }
        if (actor.objectURL) URL.revokeObjectURL(actor.objectURL);
        actor.src = src; actor.objectURL = local ? src : null; actor.mode = 'original';
        field('[data-file-name]').textContent = label; sync();
      } catch {
        if (local) URL.revokeObjectURL(src);
        if (actor.revision === revision) field('[data-error]').textContent = '图片无法读取。请确认格式和地址，当前画像已保留。';
      } finally { clearTimeout(timeout); if (actor.revision === revision) card.removeAttribute('aria-busy'); }
    }
    field('input[type=file]').onchange = event => {
      const file = event.target.files[0]; if (!file) return;
      if (file.size > 20 * 1024 * 1024) { field('[data-error]').textContent = '请选择 20 MB 以内的图片。'; return; }
      load(URL.createObjectURL(file), { local: true, label: file.name }); event.target.value = '';
    };
    field('[data-load]').onclick = () => {
      const value = field('[data-path]').value.trim();
      try {
        if (!value) throw new Error();
        const url = new URL(value, document.baseURI);
        if (!['http:', 'https:'].includes(url.protocol)) throw new Error();
        load(url.href, { label: value });
      } catch { field('[data-error]').textContent = '请输入图片地址；电脑中的文件请使用“选择图片”。'; }
    };
    field('[data-path]').onkeydown = event => { if (event.key === 'Enter') { event.preventDefault(); field('[data-load]').click(); } };
    field('[data-name]').oninput = event => { actor.name = event.target.value.trim() || actor.speaker; onChange(); };
    field('[data-mode]').onchange = event => { actor.mode = event.target.value; sync(); };
    field('[data-scale]').oninput = event => { actor.scale = Number(event.target.value); field('[data-scale-value]').textContent = `${actor.scale}%`; onChange(); };
    field('[data-offset]').oninput = event => { actor.offset = Number(event.target.value); field('[data-offset-value]').textContent = actor.offset; onChange(); };
    field('[data-flip]').onchange = event => { actor.flip = event.target.checked; onChange(); };
    field('[data-reset]').onclick = () => {
      actor.revision++; if (actor.objectURL) URL.revokeObjectURL(actor.objectURL);
      const initial = defaults.find(item => item.id === actor.id);
      Object.assign(actor, { ...initial, mode: initial.mode || 'original', scale: 100, offset: 0, flip: false, objectURL: null, look: { ...neutralLook }, compareOriginal: false });
      field('[data-path]').value = ''; field('[data-file-name]').textContent = 'Characters 默认画像'; field('[data-error]').textContent = ''; card.removeAttribute('aria-busy'); sync();
    };
    sync();
  }
  window.addEventListener('pagehide', () => cast.forEach(actor => { if (actor.objectURL) URL.revokeObjectURL(actor.objectURL); }));
}
