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

const PAPER = '#d5d2c8';
const PAPER_DIM = 'rgba(122,116,100,0.8)';
const AMBER = '#cdb98d';
const RED = '#cdb98d';
const INK_GOLD = '#9aa2a3';

const CURATED = ['老街酒馆', '码头', '警察局', '剧院', '报社', '格兰德酒店', '公园', '货运公司'];

/* 屏幕设计尺寸（相对视口高的比例） */
const UI = {
  chipW: 0.19,      // 标签宽 ≈ 19% 视口高（167px @878p）
  chipUp: 0.105,    // 标签底边在锚点上方
  stub: 0.038,      // 引线竖直小段
  bracketArm: 0.016, // 角括号臂长
  bracketLift: 0.004, // 角括号浮在顶面之上
};

/* ---------- 案卷标签贴图 ---------- */
const FONT_TITLE = '600 46px "Songti SC","STSong",serif';
const FONT_IDX = '500 30px Menlo,monospace';
const FONT_FT = '500 27px Menlo,monospace';

const BRASS = '#d4b877', CREAM = '#e3dac4', CRIMSON = '#d16a5f';

function chipCanvas(title, index, state, footer, theme = 'case') {
  const W = 512, H = footer ? 178 : 122;
  const c = document.createElement('canvas');
  c.width = W; c.height = H;
  const ctx = c.getContext('2d');
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
    const corners = [];
    for (const sx of [-1, 1]) for (const sz of [-1, 1])
      corners.push(new THREE.Vector3(cx + sx * hx, top, cy + sz * hy));
    const bGeo = new THREE.BufferGeometry();
    bGeo.setAttribute('position', new THREE.Float32BufferAttribute(new Float32Array(24), 3));
    const bracket = new THREE.LineSegments(bGeo, new THREE.LineBasicMaterial({
      color: PAPER, transparent: true, opacity: 0.85,
    }));

    // 屏幕偏移方向：沿用"背离市中心甩进夜空"的世界方向，投到屏幕上取方向
    const away = new THREE.Vector3(pos.x + 160, 0, pos.z - 20);
    if (away.lengthSq() < 1) away.set(1, 0, 0);
    away.normalize();

    const idx2 = String(i + 1).padStart(2, '0');
    const texIdle = chipTexture(chipCanvas(name, idx2, 'idle', null));
    const texHot = chipTexture(chipCanvas(name, idx2, 'hover', null));
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
      name, index: String(i + 1).padStart(2, '0'),
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
      rig.texSel = chipTexture(chipCanvas(rig.name, rig.index, 'selected', null));
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
      rig.texSel = chipTexture(chipCanvas(shown, rig.index, 'selected', done ? footer : null));
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
        rig.texSel = chipTexture(chipCanvas(rig.name, rig.index, 'selected', `— ${ft} FT —`));
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

  /* ---------- 每帧布局：屏幕设计 → 世界渲染 ----------
     一切尺寸先在屏幕像素里算好，再沿"锚点距离"的射线反投影回世界。
     标签/引线/括号在任何一个缩放下占据同样的屏幕分量与点击面积。 */
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
    // dir: +1 = 标签上移让开（下角区），-1 = 下移让开（上角区）
    const hudZones = [
      { x: 0, y: 0, w: 400, h: 210, dir: -1 },        // 左上：案卷 + 返回
      { x: W - 260, y: 0, w: 260, h: 90, dir: -1 },   // 右上：功能菜单
      { x: 0, y: H - 330, w: 340, h: 330, dir: 1 },   // 左下：人物
      { x: W - 390, y: H - 330, w: 390, h: 330, dir: 1 }, // 右下：物品
    ];
    const scale=H/720;
    for(const zone of hudZones){zone.w*=scale;zone.h*=scale;zone.x=zone.x===0?0:W-zone.w;zone.y=zone.dir===1?H-zone.h:0;}
    const placed = [...hudZones];
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

      // 标签底边中心（屏幕 px）+ 避让（只上移，带粘性回落）
      const tex = r.mat.map;
      const ratio = tex.image.height / tex.image.width;
      const chipH = chipW * ratio;
      let chipX = ax + dx * (chipW * 0.5 + UI.stub * viewport.h * 1.2);
      // Keep the resolved offset while the camera is stationary. Reset only after
      // a meaningful anchor/viewport change, not merely because avoidance succeeded.
      if(!r.layoutAnchor||Math.hypot(ax-r.layoutAnchor.x,ay-r.layoutAnchor.y)>24||r.layoutAnchor.h!==H){
        r.liftPx=0;r.layoutAnchor={x:ax,y:ay,h:H};
      }
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
    group, rigs, tick, onPointer, focus, exit, deselect, bindControls, finishPending,
    get focusFx() { return focusFx; },
    get selected() { return selected; },
    get mode() { return mode; },
    set onArrive(fn) { onArrive = fn; },
    previewDrop(rig, on) { if (rig && rig !== selected) setTex(rig, on ? 'hover' : 'idle'); },
  };
}
