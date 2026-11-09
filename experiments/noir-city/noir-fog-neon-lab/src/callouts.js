import * as THREE from 'three';

/* 世界视角的地标标注（callout）：
   角括号（取景标记）→ 制图引线（发丝，属"灯"的家族）→ 案卷标签（打字机小卡）。
   全部是场景内对象：自动吃雾、辉光和调色，不另开 HTML 层。

   尺寸契约：**屏幕恒定**。标签宽度、引线肘部、角括号臂长都按像素定义
   （相对视口高度），每帧在锚点距离处反投影回世界——所以全局视角和聚焦
   视角下 UI 的视觉分量一致，点击面积恒定。锚点本身（角括号的位置、
   引线的起点）仍钉在建筑的真实顶角上。

   交互：悬停 = 灯亮（琥珀）；点击 = 对焦飞行（曝光微降再回来，标签打字机显字 + 距离读数）；
   选中 = 全屏唯一一处红。 */

const PAPER = '#cfc8b6';
const PAPER_DIM = 'rgba(122,116,100,0.8)';
const AMBER = '#ffb35c';
const RED = '#ff5c6a';
const INK_GOLD = '#8a7440';

const CURATED = ['老街酒馆', '码头', '警察局', '剧院', '报社', '格兰德酒店', '公园', '货运公司'];

/* 屏幕设计尺寸（相对视口高的比例） */
const UI = {
  chipW: 0.19,      // 标签宽 ≈ 19% 视口高
  chipUp: 0.105,    // 标签底边在锚点上方
  stub: 0.038,      // 引线竖直小段
  bracketArm: 0.016, // 角括号臂长
  bracketLift: 0.004, // 角括号浮在顶面之上
};

/* ---------- 案卷标签贴图（四主题画师） ---------- */
const FONT_TITLE = '600 46px "Songti SC","STSong",serif';
const FONT_IDX = '500 30px Menlo,monospace';
const FONT_FT = '500 27px Menlo,monospace';
const BRASS = '#d4b877', CREAM = '#e3dac4', CRIMSON = '#d16a5f';

function chipCanvas(title, index, state, footer, theme = 'case') {
  const W = 512, H = footer ? 178 : 122;
  const c = document.createElement('canvas');
  c.width = W; c.height = H;
  const ctx = c.getContext('2d');
  if (theme === 'disco') {
    // 铅华：无方框——上下双线 + 菱形花饰，书页卷宗的语言
    const col = state === 'hover' ? BRASS : state === 'selected' ? CRIMSON : CREAM;
    const rule = state === 'idle' ? 'rgba(201,168,106,0.75)' : col;
    ctx.fillStyle = 'rgba(9,12,14,0.82)';
    ctx.fillRect(0, 0, W, H);
    if (state !== 'idle') {
      ctx.shadowColor = state === 'selected' ? 'rgba(209,106,95,0.7)' : 'rgba(212,184,119,0.6)';
      ctx.shadowBlur = 12;
    }
    ctx.fillStyle = rule;
    ctx.fillRect(10, 10, W - 20, 2);
    ctx.fillRect(10, 14, W - 20, 1);
    ctx.fillRect(10, H - 14, W - 20, 2);
    ctx.fillRect(10, H - 17, W - 20, 1);
    ctx.shadowBlur = 0;
    ctx.save();
    ctx.translate(30, H / 2 - (footer ? 14 : 0));
    ctx.rotate(Math.PI / 4);
    ctx.fillStyle = rule;
    ctx.fillRect(-4, -4, 8, 8);
    ctx.restore();
    ctx.fillStyle = state === 'idle' ? 'rgba(201,168,106,0.9)' : col;
    ctx.font = FONT_IDX;
    ctx.fillText(index, 52, H / 2 + 10 - (footer ? 14 : 0));
    ctx.fillStyle = col;
    ctx.font = FONT_TITLE;
    ctx.fillText(title, 110, H / 2 + 16 - (footer ? 14 : 0), W - 150);
    if (footer) {
      ctx.fillStyle = state === 'idle' ? 'rgba(201,168,106,0.85)' : col;
      ctx.font = FONT_FT;
      ctx.textAlign = 'center';
      ctx.fillText(footer, W / 2, H - 28);
      ctx.textAlign = 'left';
    }
    return c;
  }
  if (theme === 'press') {
    // 号外：钉在夜城上的纸片剪报（微倾斜由 material.rotation 处理）
    const ink = state === 'idle' ? '#1a1610' : '#8c2f24';
    ctx.fillStyle = '#ded7c6';
    ctx.fillRect(0, 0, W, H);
    ctx.strokeStyle = 'rgba(26,22,16,0.35)';
    ctx.lineWidth = 1;
    ctx.strokeRect(4.5, 4.5, W - 9, H - 9);
    ctx.strokeStyle = 'rgba(26,22,16,0.85)';
    ctx.lineWidth = 2;
    ctx.strokeRect(1, 1, W - 2, H - 2);
    ctx.fillStyle = '#8c2f24';
    ctx.font = FONT_IDX;
    ctx.fillText('No.' + index, 26, 44);
    ctx.fillStyle = ink;
    ctx.font = FONT_TITLE;
    ctx.fillText(title, 26, 92, W - 60);
    if (footer) {
      ctx.fillStyle = '#8c2f24';
      ctx.font = FONT_FT;
      ctx.textAlign = 'center';
      ctx.fillText(footer, W / 2, H - 26);
      ctx.textAlign = 'left';
    }
    return c;
  }
  if (theme === 'film') {
    // 胶片：带片孔的黑条，帧计数读法
    const col = state === 'idle' ? '#ded6c2' : '#e8b45c';
    ctx.fillStyle = 'rgba(8,10,13,0.93)';
    ctx.fillRect(0, 0, W, H);
    ctx.fillStyle = '#020306';
    for (let i = 0; i < 6; i++) {
      const hx = 22 + i * ((W - 60) / 5);
      ctx.fillRect(hx, 8, 12, 6); ctx.fillRect(hx, H - 14, 12, 6);
    }
    ctx.fillStyle = 'rgba(222,214,194,0.25)';
    ctx.fillRect(10, 20, W - 20, 1);
    ctx.fillRect(10, H - 26, W - 20, 1);
    ctx.fillStyle = state === 'idle' ? 'rgba(185,160,106,0.95)' : col;
    ctx.font = FONT_IDX;
    ctx.fillText('FR ' + index, 26, H / 2 + 10 - (footer ? 14 : 0));
    ctx.fillStyle = col;
    ctx.font = FONT_TITLE;
    ctx.fillText(title, 116, H / 2 + 16 - (footer ? 14 : 0), W - 160);
    if (footer) {
      ctx.fillStyle = '#e8b45c';
      ctx.font = FONT_FT;
      ctx.textAlign = 'center';
      ctx.fillText(footer, W / 2, H - 34);
      ctx.textAlign = 'left';
    }
    return c;
  }
  // 案卷（默认）：档案纸语言
  const col = state === 'hover' ? AMBER : state === 'selected' ? RED : PAPER;
  ctx.fillStyle = 'rgba(7,9,14,0.88)';
  ctx.fillRect(0, 0, W, H);
  if (state !== 'idle') {
    ctx.shadowColor = state === 'selected' ? 'rgba(255,60,80,0.8)' : 'rgba(255,170,90,0.75)';
    ctx.shadowBlur = 14;
  }
  ctx.strokeStyle = state === 'idle' ? PAPER_DIM : col;
  ctx.lineWidth = 3;
  ctx.strokeRect(6, 6, W - 12, H - 12);
  ctx.shadowBlur = 0;
  ctx.fillStyle = state === 'idle' ? 'rgba(122,116,100,0.5)' : col;
  ctx.fillRect(6, 6, 14, 4);
  ctx.fillRect(6, 6, 4, 14);
  ctx.fillStyle = state === 'idle' ? INK_GOLD : col;
  ctx.font = FONT_IDX;
  ctx.fillText(index, 30, 62);
  ctx.fillStyle = col;
  ctx.font = FONT_TITLE;
  ctx.fillText(title, 92, 66, W - 130);
  if (footer) {
    ctx.fillStyle = state === 'idle' ? 'rgba(200,190,165,0.75)' : col;
    ctx.font = FONT_FT;
    ctx.textAlign = 'center';
    ctx.fillText(footer, W / 2, 140);
    ctx.textAlign = 'left';
  }
  return c;
}

function chipTexture(canvas) {
  const tex = new THREE.CanvasTexture(canvas);
  tex.colorSpace = THREE.SRGBColorSpace;
  tex.anisotropy = 4;
  tex.minFilter = THREE.LinearFilter;
  return tex;
}

/* ---------- 标注组件 ---------- */
export function buildCallouts(anchors) {
  const group = new THREE.Group();
  group.name = '标注';
  const rigs = [];
  const byName = new Map(anchors.map((a) => [a.name, a]));

  CURATED.forEach((name, i) => {
    const a = byName.get(name);
    if (!a) return;
    // Blender (x,y,z) → three (x, z, -y)
    const pos = new THREE.Vector3(a.pos[0], a.pos[2], -a.pos[1]);
    const hx = Math.min(a.hx, 36), hy = Math.min(a.hy, 36);
    const top = a.top;
    const cx = a.cx, cy = -a.cy;
    const topCenter = new THREE.Vector3(cx, top, cy);

    // 角括号：顶面矩形四角，各两段短臂（臂长每帧按屏幕尺寸重排）
    const arm = Math.max(3.5, Math.min(hx, hy) * 0.16);
    const corners = [];
    for (const sx of [-1, 1]) for (const sz of [-1, 1])
      corners.push(new THREE.Vector3(cx + sx * hx, top, cy + sz * hy));
    const bGeo = new THREE.BufferGeometry();
    bGeo.setAttribute('position', new THREE.Float32BufferAttribute(new Float32Array(24), 3));
    const bracket = new THREE.LineSegments(bGeo, new THREE.LineBasicMaterial({
      color: PAPER, transparent: true, opacity: 0.85,
    }));

    // 屏幕偏移方向：沿用"背离市中心甩进夜空"的世界方向
    const away = new THREE.Vector3(pos.x + 160, 0, pos.z - 20);
    if (away.lengthSq() < 1) away.set(1, 0, 0);
    away.normalize();

    const idx = String(i + 1).padStart(2, '0');
    const texIdle = chipTexture(chipCanvas(name, idx, 'idle', null, 'case'));
    const texHot = chipTexture(chipCanvas(name, idx, 'hover', null, 'case'));
    const mat = new THREE.SpriteMaterial({
      map: texIdle, transparent: true, depthTest: false, depthWrite: false,
      color: new THREE.Color(0.98, 0.97, 0.94), // 比描线暗一级，别抢主体
    });
    const sprite = new THREE.Sprite(mat);
    sprite.center.set(0.5, 0); // 底边中心挂在锚定点上
    sprite.renderOrder = 5;

    const leader = new THREE.Line(
      new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(), new THREE.Vector3(), new THREE.Vector3()]),
      new THREE.LineBasicMaterial({ color: PAPER, transparent: true, opacity: 0.55 }),
    );

    const rig = {
      name, index: idx,
      pos, top, topCenter, away, corners,
      sprite, bracket, leader, mat, texIdle, texHot, texSel: null,
      liftPx: 0, clearSec: 0, lastArm: -1, state: 'idle',
    };
    sprite.userData.rig = rig;
    group.add(bracket, leader, sprite);
    rigs.push(rig);
  });

  let hovered = null, selected = null, flight = null, focusFx = 0;
  let mode = 'world';           // world | focusing | focused | exiting
  let savedPose = null;         // 离开世界视角时的机位，返回时拉回
  let onArrive = null;          // (kind: 'focus' | 'exit') => void

  function setTex(rig, state) {
    if (rig.state === state) return;
    rig.state = state;
    if (state === 'selected' && !rig.texSel) {
      rig.texSel = chipTexture(chipCanvas(rig.name, rig.index, 'selected', null, theme));
    }
    rig.mat.map = state === 'idle' ? rig.texIdle : state === 'hover' ? rig.texHot : rig.texSel;
    rig.mat.needsUpdate = true;
  }

  /* 对焦飞行 */
  function focus(rig, camera, controls) {
    const startPos = camera.position.clone();
    const startTgt = controls.target.clone();
    const destTgt = new THREE.Vector3(rig.pos.x, rig.top * 0.55, rig.pos.z);
    const flat = new THREE.Vector3(startPos.x - destTgt.x, 0, startPos.z - destTgt.z);
    if (flat.lengthSq() < 1) flat.set(1, 0, 1);
    flat.normalize();
    const dist = THREE.MathUtils.clamp(startPos.distanceTo(destTgt) * 0.11, 55, 115);
    const destPos = new THREE.Vector3(
      destTgt.x + flat.x * dist, rig.top * 0.85 + 8, destTgt.z + flat.z * dist,
    );
    const travel = startPos.distanceTo(destPos);
    flight = {
      t: 0,
      dur: THREE.MathUtils.clamp(travel * 0.0012, 0.9, 1.5),
      p0: startPos, p1: destPos, t0: startTgt, t1: destTgt,
      h: travel * 0.08, rig,
      ft: Math.round(startPos.distanceTo(rig.pos) * 3.281 / 5) * 5,
    };
    if (mode === 'world')
      savedPose = { pos: camera.position.clone(), tgt: controls.target.clone() };
    selected = rig;
    mode = 'focusing';
    rigs.forEach((r) => setTex(r, r === rig ? 'selected' : 'idle'));
    rig.sprite.visible = false; // 到站后打字机再显字
  }

  /* 退出聚焦：反向飞回离开时的世界机位，到站恢复全部地标 */
  function exit(camera, controls) {
    if (mode !== 'focused' || !savedPose || flight) return;
    const startPos = camera.position.clone();
    const startTgt = controls.target.clone();
    const travel = startPos.distanceTo(savedPose.pos);
    flight = {
      t: 0,
      dur: THREE.MathUtils.clamp(travel * 0.0012, 0.9, 1.5),
      p0: startPos, p1: savedPose.pos.clone(), t0: startTgt, t1: savedPose.tgt.clone(),
      h: travel * 0.05, rig: selected, exiting: true,
    };
    mode = 'exiting';
  }

  function deselect() {
    if (!selected) return;
    setTex(selected, 'idle');
    selected.sprite.visible = true;
    selected = null;
  }

  /* 打字机显字 + 距离读数 */
  let typer = null;
  function startTypewriter(rig, ft) {
    if (typer) clearInterval(typer);
    const footer = `— ${ft} FT —`;
    let n = 0;
    rig.sprite.visible = true;
    typer = setInterval(() => {
      n += 1;
      const shown = rig.name.slice(0, n);
      const done = n >= rig.name.length;
      rig.texSel = chipTexture(chipCanvas(shown, rig.index, 'selected', done ? footer : null, theme));
      rig.mat.map = rig.texSel;
      rig.mat.needsUpdate = true;
      if (done) { clearInterval(typer); typer = null; }
    }, 70);
  }

  /* RAF 被节流时（后台标签页/截图管线）把未完成的飞行与打字机同步推完 */
  function finishPending(camera) {
    if (typer) { clearInterval(typer); typer = null; }
    if (flight) {
      const rig = flight.rig, ft = flight.ft, exiting = flight.exiting;
      camera.position.copy(flight.p1);
      controls_setTarget(flight.t1);
      flight = null;
      focusFx = 0;
      if (exiting) {
        mode = 'world';
        savedPose = null;
        deselect();
        if (onArrive) onArrive('exit');
      } else {
        mode = 'focused';
        rig.texSel = chipTexture(chipCanvas(rig.name, rig.index, 'selected', `— ${ft} FT —`, theme));
        rig.mat.map = rig.texSel;
        rig.mat.needsUpdate = true;
        rig.sprite.visible = true;
        if (onArrive) onArrive('focus');
      }
    }
  }

  /* 指针：悬停与点击（main.js 传 NDC） */
  const raycaster = new THREE.Raycaster();
  raycaster.params.Sprite = { threshold: 0 };
  function pick(camera, ndc) {
    raycaster.setFromCamera(ndc, camera);
    const hits = raycaster.intersectObjects(rigs.filter((r) => r.sprite.visible).map((r) => r.sprite), false);
    return hits.length ? hits[0].object.userData.rig : null;
  }
  function onPointer(camera, ndc, type) {
    if (flight) return { cursor: 'default' };
    if (type === 'move') {
      const hit = pick(camera, ndc);
      if (hit !== hovered) {
        if (hovered && hovered !== selected) setTex(hovered, 'idle');
        hovered = hit;
        if (hovered && hovered !== selected) setTex(hovered, 'hover');
      }
      return { cursor: hovered ? 'pointer' : 'default' };
    }
    if (type === 'click') {
      const hit = pick(camera, ndc);
      if (hit) return { focus: hit };
    }
    return {};
  }

  /* ---------- 每帧布局：屏幕设计 → 世界渲染 ---------- */
  const proj = new THREE.Vector3(), proj2 = new THREE.Vector3();

  function pxToWorld(camera, px, py, dist, out, viewport) {
    const nx = (px / viewport.w) * 2 - 1;
    const ny = -(py / viewport.h) * 2 + 1;
    proj2.set(nx, ny, 0.5).unproject(camera);
    out.copy(proj2).sub(camera.position).normalize().multiplyScalar(dist).add(camera.position);
    return out;
  }

  function layout(camera, viewport, dt) {
    camera.updateMatrixWorld(); // 反投影依赖最新相机位姿（截图路径下可能尚未渲染过）
    const vFOV = (camera.fov * Math.PI) / 180;
    const W = viewport.w, H = viewport.h;
    // HUD 四角占用区（dir: +1 = 上移让开（下角区），-1 = 下移让开（上角区））
    const hudZones = [
      { x: 0, y: 0, w: 400, h: 210, dir: -1 },            // 左上：案卷 + 返回
      { x: W - 260, y: 0, w: 260, h: 90, dir: -1 },       // 右上：功能菜单
      { x: 0, y: H - 330, w: 340, h: 330, dir: 1 },       // 左下：人物
      { x: W - 390, y: H - 330, w: 390, h: 330, dir: 1 }, // 右下：物品
    ];
    const placed = hudZones;
    for (const r of rigs) {
      const dist = camera.position.distanceTo(r.pos);
      const isSel = r === selected;
      r.screenRect = null;
      r.sprite.visible = (dist > 75 || isSel) && (mode !== 'focused' || isSel);
      r.bracket.visible = r.leader.visible = r.sprite.visible;
      if (!r.sprite.visible) continue;

      const wpp = (2 * dist * Math.tan(vFOV / 2)) / viewport.h; // 锚点距离处 1px = 多少米
      const chipW = Math.max(140, Math.min(220, UI.chipW * viewport.h)); // 屏幕像素

      // 锚点（楼顶中心）投影
      proj.copy(r.topCenter).project(camera);
      if (proj.z >= 1 || Math.abs(proj.x) > 1.15 || Math.abs(proj.y) > 1.15) {
        r.sprite.visible = r.bracket.visible = r.leader.visible = false;
        continue;
      }
      const ax = (proj.x * 0.5 + 0.5) * viewport.w;
      const ay = (-proj.y * 0.5 + 0.5) * viewport.h;

      // 屏幕偏移方向：away 方向的屏幕投影
      proj2.copy(r.topCenter).addScaledVector(r.away, 10).project(camera);
      let dx = (proj2.x - proj.x) * viewport.w, dy = -(proj2.y - proj.y) * viewport.h;
      const dl = Math.hypot(dx, dy) || 1;
      dx /= dl; dy /= dl;

      // 标签底边中心（屏幕 px）+ 避让（带粘性回落）
      const tex = r.mat.map;
      const ratio = tex.image.height / tex.image.width;
      const chipH = chipW * ratio;
      let chipX = ax + dx * (chipW * 0.5 + UI.stub * viewport.h * 1.2);
      let chipY = ay - UI.chipUp * viewport.h - r.liftPx;
      chipY = Math.max(chipH + 16, Math.min(viewport.h - 24, chipY));
      chipX = Math.max(chipW / 2 + 10, Math.min(viewport.w - chipW / 2 - 10, chipX));
      const rect = { x: chipX - chipW / 2, y: chipY - chipH, w: chipW, h: chipH };
      r.screenRect = { ...rect, rig: r };
      let hit = false;
      for (const q of placed) {
        if (rect.x < q.x + q.w && rect.x + rect.w > q.x &&
            rect.y < q.y + q.h && rect.y + rect.h > q.y) { hit = true; break; }
      }
      if (hit) {
        const zone = hudZones.find((z) => rect.x < z.x + z.w && rect.x + rect.w > z.x && rect.y < z.y + z.h && rect.y + rect.h > z.y);
        const dir = zone ? zone.dir : 1;
        r.liftPx = THREE.MathUtils.clamp(r.liftPx + dir * dt * 300, -H * 0.3, H * 0.3);
        r.clearSec = 0;
      } else if (r.liftPx !== 0) {
        r.clearSec += dt;
        if (r.clearSec > 2.5) r.liftPx -= Math.sign(r.liftPx) * Math.min(Math.abs(r.liftPx), dt * 60);
      }
      placed.push(rect);

      // 反投影回世界（深度 = 锚点距离，和建筑同雾）
      pxToWorld(camera, chipX, chipY, dist, r.sprite.position, viewport);
      r.sprite.scale.set(chipW * wpp, chipH * wpp, 1);

      // 引线：锚点 → 屏幕竖直小段 → 标签底边中心（肘部形状屏幕恒定）
      const stub = UI.stub * viewport.h;
      const w0 = new THREE.Vector3(), w1 = new THREE.Vector3(), w2 = new THREE.Vector3();
      pxToWorld(camera, ax, ay, dist, w0, viewport);
      pxToWorld(camera, ax, ay - stub, dist, w1, viewport);
      pxToWorld(camera, chipX, chipY - 2, dist, w2, viewport);
      const lp = r.leader.geometry.attributes.position;
      lp.setXYZ(0, w0.x, w0.y, w0.z);
      lp.setXYZ(1, w1.x, w1.y, w1.z);
      lp.setXYZ(2, w2.x, w2.y, w2.z);
      lp.needsUpdate = true;

      // 角括号：钉在世界顶角上，臂长按屏幕尺寸
      const arm = UI.bracketArm * viewport.h * wpp;
      if (Math.abs(arm - r.lastArm) / Math.max(arm, 1e-4) > 0.06) {
        r.lastArm = arm;
        const lift = UI.bracketLift * viewport.h * wpp;
        const arr = r.bracket.geometry.attributes.position.array;
        let k = 0;
        for (const c of r.corners) {
          const dirX = Math.sign(c.x - r.topCenter.x), dirZ = Math.sign(c.z - r.topCenter.z);
          arr[k++] = c.x; arr[k++] = c.y + lift; arr[k++] = c.z;
          arr[k++] = c.x - dirX * arm; arr[k++] = c.y + lift; arr[k++] = c.z;
          arr[k++] = c.x; arr[k++] = c.y + lift; arr[k++] = c.z;
          arr[k++] = c.x; arr[k++] = c.y + lift; arr[k++] = c.z - dirZ * arm;
        }
        r.bracket.geometry.attributes.position.needsUpdate = true;
      }
    }
  }

  /* 主题切换：场景内标注（标签纹理、引线、角括号）一并换装 */
  const THEME_LINE = { case: 0xcfc8b6, disco: 0xcbb083, press: 0xd8d2c2, film: 0xded6c2 };
  let theme = 'case';
  function setTheme(t) {
    if (theme === t) return;
    theme = t;
    const lineCol = THEME_LINE[t] ?? 0xcfc8b6;
    for (const r of rigs) {
      r.texIdle = chipTexture(chipCanvas(r.name, r.index, 'idle', null, t));
      r.texHot = chipTexture(chipCanvas(r.name, r.index, 'hover', null, t));
      r.texSel = null;
      r.state = '__reset';
      setTex(r, r === selected ? 'selected' : 'idle');
      r.leader.material.color.set(lineCol);
      r.bracket.material.color.set(lineCol);
      // 号外：纸片被别针别住的微倾斜（每张固定角度）
      const seed = (parseInt(r.index, 10) * 137) % 100 / 100;
      r.mat.rotation = t === 'press' ? (seed - 0.5) * 0.09 : 0;
    }
  }

  let _controls = null;
  function controls_setTarget(t) { if (_controls) { _controls.target.copy(t); _controls.update(); } }
  function bindControls(controls) { _controls = controls; }

  function tick(camera, dt, viewport) {
    focusFx = 0;
    if (flight) {
      flight.t += dt / flight.dur;
      const t = Math.min(flight.t, 1);
      const e = t * t * t * (t * (t * 6 - 15) + 10); // smootherstep
      camera.position.lerpVectors(flight.p0, flight.p1, e);
      camera.position.y += Math.sin(Math.PI * e) * flight.h;
      camera.lookAt(flight.t0.clone().lerp(flight.t1, e));
      focusFx = Math.sin(Math.PI * e);
      if (t >= 1) {
        const rig = flight.rig;
        controls_setTarget(flight.t1);
        if (flight.exiting) {
          mode = 'world';
          savedPose = null;
          deselect();
          if (onArrive) onArrive('exit');
        } else {
          mode = 'focused';
          startTypewriter(rig, flight.ft);
          if (onArrive) onArrive('focus');
        }
        flight = null;
      }
    }
    if (group.visible) layout(camera, viewport, dt);
  }

  return {
    group, rigs, tick, onPointer, focus, exit, deselect, bindControls, finishPending, setTheme,
    get focusFx() { return focusFx; },
    get selected() { return selected; },
    get mode() { return mode; },
    set onArrive(fn) { onArrive = fn; },
    previewDrop(rig, on) { if (rig && rig !== selected) setTex(rig, on ? 'hover' : 'idle'); },
  };
}
