import * as THREE from 'three';

const textureCache = new Map();

function colorStyle(value, fallback) {
  if (value?.isColor) return '#' + value.getHexString();
  return fallback;
}

function drawPath(ctx, draw, fill, stroke, width) {
  ctx.beginPath();
  draw(ctx);
  ctx.closePath();
  ctx.fillStyle = fill;
  ctx.fill();
  if (stroke && width > 0) {
    ctx.strokeStyle = stroke;
    ctx.lineWidth = width;
    ctx.lineJoin = 'round';
    ctx.lineCap = 'round';
    ctx.stroke();
  }
}

function heroCanvas(fill, stroke, detail) {
  const canvas = document.createElement('canvas');
  canvas.width = 512;
  canvas.height = 1024;
  const ctx = canvas.getContext('2d');
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.lineJoin = 'round';
  ctx.lineCap = 'round';

  // Legs are drawn first so the coat can hide their upper joins.
  drawPath(ctx, (p) => {
    p.moveTo(222, 718);
    p.bezierCurveTo(216, 775, 210, 848, 208, 906);
    p.lineTo(179, 925);
    p.bezierCurveTo(175, 932, 184, 938, 203, 937);
    p.lineTo(241, 932);
    p.bezierCurveTo(247, 869, 249, 795, 250, 722);
  }, fill, stroke, 5);

  drawPath(ctx, (p) => {
    p.moveTo(265, 718);
    p.bezierCurveTo(268, 784, 274, 852, 281, 905);
    p.lineTo(318, 919);
    p.bezierCurveTo(326, 925, 319, 934, 300, 934);
    p.lineTo(263, 930);
    p.bezierCurveTo(257, 865, 255, 790, 255, 721);
  }, fill, stroke, 5);

  // Arms behind the coat body.
  drawPath(ctx, (p) => {
    p.moveTo(178, 372);
    p.bezierCurveTo(154, 394, 148, 438, 142, 488);
    p.bezierCurveTo(135, 554, 126, 616, 118, 672);
    p.bezierCurveTo(117, 686, 126, 693, 137, 687);
    p.bezierCurveTo(152, 625, 166, 557, 182, 493);
    p.bezierCurveTo(193, 448, 199, 405, 196, 383);
  }, fill, stroke, 5);

  drawPath(ctx, (p) => {
    p.moveTo(326, 370);
    p.bezierCurveTo(352, 397, 362, 440, 371, 488);
    p.bezierCurveTo(382, 547, 393, 603, 405, 655);
    p.bezierCurveTo(408, 670, 401, 679, 389, 675);
    p.bezierCurveTo(373, 616, 358, 552, 340, 493);
    p.bezierCurveTo(328, 448, 318, 408, 307, 383);
  }, fill, stroke, 5);

  // Long coat. It is intentionally tapered at the waist and asymmetric at
  // the hem, closer to a drawn noir figure than a geometric cone.
  drawPath(ctx, (p) => {
    p.moveTo(205, 350);
    p.bezierCurveTo(184, 370, 176, 398, 174, 438);
    p.bezierCurveTo(172, 500, 181, 566, 173, 627);
    p.lineTo(153, 752);
    p.bezierCurveTo(182, 769, 212, 776, 244, 775);
    p.bezierCurveTo(278, 779, 314, 771, 348, 751);
    p.lineTo(333, 624);
    p.bezierCurveTo(325, 556, 334, 491, 329, 431);
    p.bezierCurveTo(326, 393, 316, 369, 294, 349);
    p.bezierCurveTo(270, 360, 230, 361, 205, 350);
  }, fill, stroke, 6);

  // Collar/neck.
  drawPath(ctx, (p) => {
    p.moveTo(230, 329);
    p.lineTo(234, 293);
    p.lineTo(276, 292);
    p.lineTo(278, 332);
    p.bezierCurveTo(264, 343, 243, 344, 230, 329);
  }, fill, stroke, 5);

  // Slight profile head: smaller than before, with a real brow/nose/chin.
  drawPath(ctx, (p) => {
    p.moveTo(221, 267);
    p.bezierCurveTo(214, 248, 217, 218, 229, 198);
    p.bezierCurveTo(242, 178, 267, 171, 288, 184);
    p.bezierCurveTo(300, 192, 307, 203, 309, 216);
    p.lineTo(325, 224);
    p.lineTo(311, 234);
    p.bezierCurveTo(308, 253, 298, 270, 281, 282);
    p.bezierCurveTo(258, 296, 234, 290, 221, 267);
  }, fill, stroke, 5);

  // Fedora: wide but thin brim, shallow crown.
  drawPath(ctx, (p) => {
    p.moveTo(190, 177);
    p.bezierCurveTo(217, 171, 246, 169, 277, 170);
    p.bezierCurveTo(307, 170, 331, 174, 348, 181);
    p.bezierCurveTo(330, 188, 302, 190, 270, 189);
    p.bezierCurveTo(236, 190, 208, 186, 190, 177);
  }, fill, stroke, 5);

  drawPath(ctx, (p) => {
    p.moveTo(225, 169);
    p.lineTo(231, 118);
    p.bezierCurveTo(247, 108, 276, 107, 299, 116);
    p.lineTo(309, 171);
    p.bezierCurveTo(282, 176, 250, 176, 225, 169);
  }, fill, stroke, 5);

  // Internal vector marks are deliberately sparse.
  ctx.strokeStyle = detail;
  ctx.lineWidth = 4;
  ctx.globalAlpha = 0.78;
  ctx.beginPath();
  ctx.moveTo(229, 366);
  ctx.lineTo(252, 432);
  ctx.lineTo(273, 366);
  ctx.stroke();

  ctx.globalAlpha = 0.48;
  ctx.lineWidth = 3;
  ctx.beginPath();
  ctx.moveTo(301, 438);
  ctx.bezierCurveTo(306, 487, 310, 527, 319, 564);
  ctx.stroke();

  ctx.beginPath();
  ctx.moveTo(199, 447);
  ctx.bezierCurveTo(192, 497, 189, 538, 184, 570);
  ctx.stroke();

  ctx.globalAlpha = 1;
  return canvas;
}

function crowdCanvas(variant, pose, fill, stroke, detail) {
  const canvas = document.createElement('canvas');
  canvas.width = 384;
  canvas.height = 768;
  const ctx = canvas.getContext('2d');
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.lineJoin = 'round';
  ctx.lineCap = 'round';

  const tall = variant % 4 === 0;
  const noHat = variant % 4 === 3;
  const slim = variant % 3 === 1;
  const step = pose === 'walk' ? 14 : 3;
  const gesture = pose === 'gesture' ? 28 : 0;
  const left = slim ? 124 : 112;
  const right = slim ? 260 : 272;
  const shoulderY = tall ? 258 : 278;
  const hemY = tall ? 610 : 622;

  // Legs.
  drawPath(ctx, (p) => {
    p.moveTo(168, hemY - 8);
    p.lineTo(185, hemY - 8);
    p.lineTo(177 - step, 701);
    p.lineTo(144 - step, 710);
    p.lineTo(143 - step, 719);
    p.lineTo(186 - step, 717);
    p.lineTo(198, hemY - 8);
  }, fill, stroke, 4);

  drawPath(ctx, (p) => {
    p.moveTo(201, hemY - 8);
    p.lineTo(216, hemY - 8);
    p.lineTo(226 + step, 702);
    p.lineTo(258 + step, 710);
    p.lineTo(258 + step, 719);
    p.lineTo(217 + step, 716);
    p.lineTo(188, hemY - 8);
  }, fill, stroke, 4);

  // Arms.
  drawPath(ctx, (p) => {
    p.moveTo(left + 18, shoulderY + 20);
    p.lineTo(left - 2, shoulderY + 42);
    p.lineTo(left - 12 - gesture, 500);
    p.lineTo(left + 5 - gesture, 505);
    p.lineTo(left + 36, shoulderY + 92);
  }, fill, stroke, 4);

  drawPath(ctx, (p) => {
    p.moveTo(right - 18, shoulderY + 20);
    p.lineTo(right + 2, shoulderY + 42);
    p.lineTo(right + 12, 500);
    p.lineTo(right - 5, 505);
    p.lineTo(right - 36, shoulderY + 92);
  }, fill, stroke, 4);

  // Body.
  drawPath(ctx, (p) => {
    p.moveTo(left + 20, shoulderY);
    p.bezierCurveTo(left + 4, shoulderY + 48, left + 4, 386, left + 8, 445);
    p.lineTo(left - (slim ? 2 : 14), hemY);
    p.bezierCurveTo(153, hemY + 10, 231, hemY + 10, right + (slim ? 2 : 14), hemY);
    p.lineTo(right - 8, 445);
    p.bezierCurveTo(right - 4, 386, right - 4, shoulderY + 48, right - 20, shoulderY);
    p.bezierCurveTo(216, shoulderY - 15, 168, shoulderY - 15, left + 20, shoulderY);
  }, fill, stroke, 4);

  // Neck/head.
  drawPath(ctx, (p) => {
    p.moveTo(178, shoulderY - 7);
    p.lineTo(180, shoulderY - 40);
    p.lineTo(208, shoulderY - 40);
    p.lineTo(210, shoulderY - 7);
  }, fill, stroke, 4);

  ctx.beginPath();
  ctx.ellipse(194, shoulderY - 83, 34, 42, 0, 0, Math.PI * 2);
  ctx.fillStyle = fill;
  ctx.fill();
  ctx.strokeStyle = stroke;
  ctx.lineWidth = 4;
  ctx.stroke();

  if (!noHat) {
    drawPath(ctx, (p) => {
      p.moveTo(148, shoulderY - 125);
      p.bezierCurveTo(173, shoulderY - 132, 217, shoulderY - 132, 241, shoulderY - 125);
      p.bezierCurveTo(223, shoulderY - 118, 169, shoulderY - 118, 148, shoulderY - 125);
    }, fill, stroke, 4);
    drawPath(ctx, (p) => {
      p.moveTo(171, shoulderY - 130);
      p.lineTo(176, shoulderY - 168);
      p.lineTo(216, shoulderY - 168);
      p.lineTo(221, shoulderY - 130);
    }, fill, stroke, 4);
  }

  ctx.strokeStyle = detail;
  ctx.globalAlpha = 0.45;
  ctx.lineWidth = 2.5;
  ctx.beginPath();
  ctx.moveTo(179, shoulderY + 32);
  ctx.lineTo(194, shoulderY + 74);
  ctx.lineTo(208, shoulderY + 32);
  ctx.stroke();
  ctx.globalAlpha = 1;
  return canvas;
}

function getTexture(kind, variant, pose, materials) {
  const fill = colorStyle(materials.character?.color, '#010204');
  const mainLine = materials.characterLineColor ?? '#e8e5dc';
  const dimLine = materials.characterDimLineColor ?? '#b8bec8';
  const stroke = kind === 'hero' ? mainLine : dimLine;
  const key = [kind, variant, pose, fill, stroke, dimLine].join(':');
  if (textureCache.has(key)) return textureCache.get(key);

  const canvas = kind === 'hero'
    ? heroCanvas(fill, stroke, dimLine)
    : crowdCanvas(variant, pose, fill, stroke, dimLine);

  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  texture.minFilter = THREE.LinearMipmapLinearFilter;
  texture.magFilter = THREE.LinearFilter;
  texture.generateMipmaps = true;
  texture.needsUpdate = true;
  textureCache.set(key, texture);
  return texture;
}

export function createCharacter({
  name,
  position,
  scale = 1,
  pose = 'neutral',
  kind = 'crowd',
  variant = 0,
  materials,
  parent,
}) {
  const texture = getTexture(kind, variant, pose, materials);
  const material = new THREE.SpriteMaterial({
    map: texture,
    transparent: true,
    alphaTest: 0.018,
    depthTest: true,
    depthWrite: true,
    fog: true,
    toneMapped: false,
  });

  const sprite = new THREE.Sprite(material);
  sprite.name = name;
  sprite.center.set(0.5, 0.0);
  sprite.position.fromArray(position);

  if (kind === 'hero') {
    sprite.scale.set(2.72 * scale, 5.45 * scale, 1);
  } else {
    const height = (variant % 4 === 0 ? 4.55 : 4.25) * scale;
    sprite.scale.set(height * 0.50, height, 1);
  }

  // Tiny z offsets prevent equal-depth crowd sprites from fighting.
  sprite.position.z += variant * 0.006;
  parent.add(sprite);
  return sprite;
}
