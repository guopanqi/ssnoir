// 仅用于实验舞台的显示调节，不修改源图片。
export const neutralLook = { brightness: 100, contrast: 100, saturation: 100, temperature: 0, edge: 0, fade: 0 };
const presets = [
  { id: 'original', label: '原始颜色', values: neutralLook },
  { id: 'night', label: '夜景融合', values: { brightness: 76, contrast: 104, saturation: 72, temperature: 45, edge: 2, fade: 12 } },
  { id: 'cold', label: '偏暗冷光', values: { brightness: 62, contrast: 112, saturation: 48, temperature: 70, edge: 3, fade: 18 } },
  { id: 'warm', label: '酒馆暖光', values: { brightness: 84, contrast: 100, saturation: 82, temperature: -35, edge: 2, fade: 10 } },
];
const sliders = [
  { key: 'brightness', label: '画像亮度', min: 20, max: 140, unit: '%' },
  { key: 'contrast', label: '画像对比度', min: 50, max: 160, unit: '%' },
  { key: 'saturation', label: '画像饱和度', min: 0, max: 160, unit: '%' },
  { key: 'temperature', label: '画像冷暖', min: -100, max: 100, unit: '' },
  { key: 'edge', label: '边缘柔化', min: 0, max: 15, unit: 'px' },
  { key: 'fade', label: '底部淡出', min: 0, max: 50, unit: '%' },
];

export function renderPortraitLook(surface, actor) {
  const look = actor.compareOriginal ? neutralLook : actor.look;
  const id = `portrait-temperature-${actor.id}`;
  let matrix = document.getElementById(`${id}-matrix`);
  if (!matrix) {
    const ns = 'http://www.w3.org/2000/svg';
    const svg = document.createElementNS(ns, 'svg'); svg.setAttribute('width', '0'); svg.setAttribute('height', '0'); svg.setAttribute('aria-hidden', 'true'); svg.classList.add('portrait-filter-definitions');
    const filter = document.createElementNS(ns, 'filter'); filter.setAttribute('id', id); filter.setAttribute('color-interpolation-filters', 'sRGB');
    matrix = document.createElementNS(ns, 'feColorMatrix'); matrix.setAttribute('id', `${id}-matrix`); matrix.setAttribute('type', 'matrix');
    filter.append(matrix); svg.append(filter); document.body.append(svg);
  }
  const cold = Math.max(0, look.temperature) / 100, warm = Math.max(0, -look.temperature) / 100;
  const red = 1 - cold * .32, green = 1 - cold * .14 - warm * .08, blue = 1 - warm * .32;
  matrix.setAttribute('values', `${red} 0 0 0 0  0 ${green} 0 0 0  0 0 ${blue} 0 0  0 0 0 1 0`);
  const visual = surface.querySelector('.portrait-visual');
  visual.style.filter = `brightness(${look.brightness / 100}) contrast(${look.contrast / 100}) saturate(${look.saturation / 100}) url("#${id}") blur(${look.edge / 10}px)`;
  visual.style.maskImage = look.fade ? `linear-gradient(to bottom, #000 0%, #000 ${100 - look.fade}%, transparent 100%)` : 'none';
  surface.dataset.look = actor.compareOriginal ? 'comparison' : presets.find(preset => Object.keys(neutralLook).every(key => actor.look[key] === preset.values[key]))?.id || 'custom';
}

export function mountPortraitEffects(root, cast, onChange) {
  let selected = cast[0].id, reference = 'city';
  root.innerHTML = `<div class="portrait-look-bar"><strong>人物与场景</strong><label>调整对象<select data-look-target aria-label="调色对象"></select></label><div class="portrait-look-presets" role="group" aria-label="画像调色预设">${presets.map(preset => `<button type="button" data-look-preset="${preset.id}" aria-pressed="false">${preset.label}</button>`).join('')}</div><button type="button" data-look-compare aria-label="原图对比" aria-pressed="false">原图对比</button></div>
    <div class="portrait-look-subbar"><span data-look-status role="status"></span><label>背景参照<select data-look-reference aria-label="背景参照"><option value="city">城市场景</option><option value="navy">深蓝底检查</option><option value="gray">中灰底检查</option></select></label><small>调亮度保留实心色块；原图对比不丢失调色参数。</small></div>
    <details class="portrait-look-details"><summary>精调明暗、冷暖与边缘</summary><div class="portrait-look-ranges">${sliders.map(slider => `<label>${slider.label}<input type="range" data-look-range="${slider.key}" min="${slider.min}" max="${slider.max}" step="1" aria-label="${slider.label}"><output data-look-output="${slider.key}"></output></label>`).join('')}</div><p>冷暖只调颜色，不改变透明度；底部淡出按图片画布高度计算。调节仅用于当前预览。</p></details>`;
  const target = root.querySelector('[data-look-target]');
  for (const actor of cast) { const option = document.createElement('option'); option.value = actor.id; target.append(option); }
  const current = () => cast.find(actor => actor.id === selected);
  function sync() {
    for (const actor of cast) target.querySelector(`option[value="${actor.id}"]`).textContent = `${actor.side === 'left' ? '左侧' : '右侧'} · ${actor.name}`;
    target.value = selected;
    const actor = current();
    for (const slider of sliders) {
      const value = actor.look[slider.key];
      root.querySelector(`[data-look-range="${slider.key}"]`).value = value;
      root.querySelector(`[data-look-output="${slider.key}"]`).textContent = slider.key === 'edge' ? `${(value / 10).toFixed(1)}px` : slider.key === 'temperature' ? value > 0 ? `冷 ${value}` : value < 0 ? `暖 ${-value}` : '中性' : `${value}${slider.unit}`;
    }
    const preset = presets.find(item => Object.keys(neutralLook).every(key => actor.look[key] === item.values[key]));
    root.querySelectorAll('[data-look-preset]').forEach(button => button.setAttribute('aria-pressed', String(button.dataset.lookPreset === preset?.id)));
    const compare = root.querySelector('[data-look-compare]'); compare.setAttribute('aria-pressed', String(!!actor.compareOriginal)); compare.textContent = actor.compareOriginal ? '返回调色' : '原图对比';
    root.querySelector('[data-look-status]').textContent = `${actor.name} · ${actor.compareOriginal ? '正在对照原图' : preset?.label || '自定义调色'}`;
  }
  target.onchange = () => { selected = target.value; sync(); };
  root.querySelectorAll('[data-look-preset]').forEach(button => { button.onclick = () => {
    current().look = { ...presets.find(preset => preset.id === button.dataset.lookPreset).values };
    current().compareOriginal = false; sync(); onChange();
  }; });
  root.querySelector('[data-look-compare]').onclick = () => { current().compareOriginal = !current().compareOriginal; sync(); onChange(); };
  root.querySelectorAll('[data-look-range]').forEach(input => { input.oninput = () => {
    current().look[input.dataset.lookRange] = Number(input.value); current().compareOriginal = false; sync(); onChange();
  }; });
  root.querySelector('[data-look-reference]').onchange = event => { reference = event.target.value; onChange(); };
  sync();
  return { sync, applyStage(stage) { stage.style.setProperty('--portrait-reference-bg', reference === 'gray' ? '#808080' : reference === 'navy' ? '#0c1420' : 'transparent'); stage.dataset.reference = reference; } };
}
