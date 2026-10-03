import * as THREE from 'three';

const textureCache = new Map();

function colorStyle(value, fallback) {
  if (value?.isColor) return '#' + value.getHexString();
  return fallback;
}

function pathFill(ctx, draw, color) {
  ctx.beginPath();
  draw(ctx);
  ctx.closePath();
  ctx.fillStyle = color;
  ctx.fill();
}

function drawHeroMask(ctx, color) {
  // Legs.
  pathFill(ctx, (p) => {
    p.moveTo(226, 714);
    p.bezierCurveTo(220, 780, 218, 856, 216, 907);
    p.lineTo(184, 924);
    p.bezierCurveTo(176, 931, 181, 939, 199, 939);
    p.lineTo(239, 934);
    p.bezierCurveTo(246, 866, 249, 789, 252, 716);
  }, color);

  pathFill(ctx, (p) => {
    p.moveTo(262, 716);
    p.bezierCurveTo(265, 786, 270, 859, 279, 906);
    p.lineTo(319, 920);
    p.bezierCurveTo(327, 926, 319, 935, 300, 934);
    p.lineTo(264, 930);
    p.bezierCurveTo(258, 864, 257, 788, 256, 716);
  }, color);

  // Arms, tucked slightly behind the torso.
  pathFill(ctx, (p) => {
    p.moveTo(191, 360);
    p.bezierCurveTo(167, 377, 155, 411, 149, 454);
    p.bezierCurveTo(142, 511, 133, 574, 124, 635);
    p.bezierCurveTo(122, 650, 130, 659, 142, 654);
    p.bezierCurveTo(158, 592, 170, 532, 183, 476);
    p.bezierCurveTo(193, 430, 200, 393, 203, 369);
  }, color);

  pathFill(ctx, (p) => {
    p.moveTo(312, 360);
    p.bezierCurveTo(337, 381, 349, 414, 358, 455);
    p.bezierCurveTo(368, 508, 379, 565, 391, 621);
    p.bezierCurveTo(394, 637, 387, 647, 375, 643);
    p.bezierCurveTo(359, 585, 344, 529, 329, 476);
    p.bezierCurveTo(318, 433, 311, 394, 302, 369);
  }, color);

  // Coat.
  pathFill(ctx, (p) => {
    p.moveTo(213, 346);
    p.bezierCurveTo(191, 360, 181, 388, 179, 425);
    p.bezierCurveTo(177, 491, 183, 548, 178, 608);
    p.lineTo(157, 750);
    p.bezierCurveTo(184, 770, 219, 782, 252, 779);
    p.bezierCurveTo(287, 778, 319, 764, 345, 744);
    p.lineTo(329, 608);
    p.bezierCurveTo(324, 548, 330, 489, 327, 424);
    p.bezierCurveTo(325, 389, 317, 361, 299, 345);
    p.bezierCurveTo(277, 355, 238, 356, 213, 346);
  }, color);

  // Neck and head overlap so the silhouette is continuous.
  pathFill(ctx, (p) => {
    p.moveTo(218, 370);
    p.lineTo(226, 270);
    p.lineTo(294, 270);
    p.lineTo(296, 365);
  }, color);

  pathFill(ctx, (p) => {
    p.moveTo(244, 282);
    p.bezierCurveTo(232, 253, 235, 222, 247, 202);
    p.bezierCurveTo(260, 181, 283, 176, 301, 184);
    p.bezierCurveTo(314, 191, 321, 203, 323, 215);
    p.lineTo(342, 222);
    p.lineTo(328, 231);
    p.bezierCurveTo(309, 251, 299, 269, 281, 282);
    p.bezierCurveTo(262, 294, 240, 291, 228, 270);
  }, color);

  // Hat brim + crown overlap the head.
  pathFill(ctx, (p) => {
    p.moveTo(204, 175);
    p.bezierCurveTo(231, 168, 261, 166, 292, 168);
    p.bezierCurveTo(322, 168, 347, 172, 365, 179);
    p.bezierCurveTo(345, 187, 316, 190, 284, 189);
    p.bezierCurveTo(250, 189, 221, 185, 204, 175);
  }, color);

  pathFill(ctx, (p) => {
    p.moveTo(236, 171);
    p.lineTo(243, 119);
    p.bezierCurveTo(260, 108, 290, 107, 313, 116);
    p.lineTo(324, 173);
  }, color);
}

function drawCrowdMask(ctx, color, variant, pose) {
  const tall = variant % 4 === 0;
  const noHat = variant % 4 === 3;
  const slim = variant % 3 === 1;
  const step = pose === 'walk' ? 12 : 2;
  const gesture = pose === 'gesture' ? 24 : 0;
  const left = slim ? 126 : 114;
  const right = slim ? 258 : 270;
  const shoulderY = tall ? 256 : 274;
  const hemY = tall ? 604 : 618;

  pathFill(ctx, (p) => {
    p.moveTo(169, hemY - 10);
    p.lineTo(186, hemY - 10);
    p.lineTo(178 - step, 699);
    p.lineTo(146 - step, 709);
    p.lineTo(145 - step, 718);
    p.lineTo(186 - step, 716);
    p.lineTo(198, hemY - 9);
  }, color);

  pathFill(ctx, (p) => {
    p.moveTo(201, hemY - 9);
    p.lineTo(216, hemY - 9);
    p.lineTo(226 + step, 701);
    p.lineTo(257 + step, 710);
    p.lineTo(257 + step, 719);
    p.lineTo(217 + step, 716);
    p.lineTo(188, hemY - 9);
  }, color);

  pathFill(ctx, (p) => {
    p.moveTo(left + 20, shoulderY + 8);
    p.bezierCurveTo(left + 2, shoulderY + 36, left - 3, 396, left - 12 - gesture, 493);
    p.lineTo(left + 6 - gesture, 500);
    p.bezierCurveTo(left + 23, 426, left + 35, 349, left + 42, shoulderY + 41);
  }, color);

  pathFill(ctx, (p) => {
    p.moveTo(right - 20, shoulderY + 8);
    p.bezierCurveTo(right - 2, shoulderY + 36, right + 3, 396, right + 12, 493);
    p.lineTo(right - 6, 500);
    p.bezierCurveTo(right - 23, 426, right - 35, 349, right - 42, shoulderY + 41);
  }, color);

  pathFill(ctx, (p) => {
    p.moveTo(left + 20, shoulderY);
    p.bezierCurveTo(left + 6, shoulderY + 45, left + 7, 396, left + 10, 450);
    p.lineTo(left - (slim ? 0 : 12), hemY);
    p.bezierCurveTo(154, hemY + 9, 230, hemY + 9, right + (slim ? 0 : 12), hemY);
    p.lineTo(right - 10, 450);
    p.bezierCurveTo(right - 7, 396, right - 6, shoulderY + 45, right - 20, shoulderY);
    p.bezierCurveTo(218, shoulderY - 13, 166, shoulderY - 13, left + 20, shoulderY);
  }, color);

  pathFill(ctx, (p) => {
    p.moveTo(180, shoulderY + 4);
    p.lineTo(181, shoulderY - 44);
    p.lineTo(207, shoulderY - 44);
    p.lineTo(209, shoulderY + 4);
  }, color);

  ctx.beginPath();
  ctx.ellipse(194, shoulderY - 83, 32, 40, 0, 0, Math.PI * 2);
  ctx.fillStyle = color;
  ctx.fill();

  if (!noHat) {
    pathFill(ctx, (p) => {
      p.moveTo(149, shoulderY - 125);
      p.bezierCurveTo(173, shoulderY - 132, 216, shoulderY - 132, 240, shoulderY - 125);
      p.bezierCurveTo(222, shoulderY - 118, 170, shoulderY - 118, 149, shoulderY - 125);
    }, color);
    pathFill(ctx, (p) => {
      p.moveTo(171, shoulderY - 129);
      p.lineTo(176, shoulderY - 168);
      p.lineTo(215, shoulderY - 168);
      p.lineTo(220, shoulderY - 129);
    }, color);
  }
}

function buildCutout(width, height, drawMask, fillColor, outlineColor, detailColor, detailDraw, outlineRadius) {
  const fillCanvas = document.createElement('canvas');
  fillCanvas.width = width;
  fillCanvas.height = height;
  const fillCtx = fillCanvas.getContext('2d');
  drawMask(fillCtx, fillColor);

  const outlineCanvas = document.createElement('canvas');
  outlineCanvas.width = width;
  outlineCanvas.height = height;
  const outlineCtx = outlineCanvas.getContext('2d');
  drawMask(outlineCtx, outlineColor);

  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  const ctx = canvas.getContext('2d');
  ctx.clearRect(0, 0, width, height);

  const samples = 16;
  for (let i = 0; i < samples; i++) {
    const a = (i / samples) * Math.PI * 2;
    ctx.drawImage(
      outlineCanvas,
      Math.cos(a) * outlineRadius,
      Math.sin(a) * outlineRadius,
    );
  }

  // Black center erases every internal seam between body components.
  ctx.drawImage(fillCanvas, 0, 0);

  if (detailDraw) detailDraw(ctx, detailColor);
  return canvas;
}

function heroCanvas(fill, stroke, detail) {
  return buildCutout(
    512,
    1024,
    (ctx, color) => drawHeroMask(ctx, color),
    fill,
    stroke,
    detail,
    (ctx, color) => {
      ctx.strokeStyle = color;
      ctx.lineJoin = 'round';
      ctx.lineCap = 'round';
      ctx.globalAlpha = 0.66;
      ctx.lineWidth = 3.1;

      ctx.beginPath();
      ctx.moveTo(226, 364);
      ctx.lineTo(252, 430);
      ctx.lineTo(277, 364);
      ctx.stroke();

      ctx.globalAlpha = 0.34;
      ctx.beginPath();
      ctx.moveTo(302, 438);
      ctx.bezierCurveTo(307, 486, 311, 525, 319, 557);
      ctx.stroke();

      ctx.beginPath();
      ctx.moveTo(199, 444);
      ctx.bezierCurveTo(193, 491, 189, 529, 184, 560);
      ctx.stroke();

      // Hat band.
      ctx.globalAlpha = 0.42;
      ctx.lineWidth = 2.4;
      ctx.beginPath();
      ctx.moveTo(228, 155);
      ctx.bezierCurveTo(250, 158, 282, 158, 305, 155);
      ctx.stroke();
      ctx.globalAlpha = 1;
    },
    3.2,
  );
}

function crowdCanvas(variant, pose, fill, stroke, detail) {
  return buildCutout(
    384,
    768,
    (ctx, color) => drawCrowdMask(ctx, color, variant, pose),
    fill,
    stroke,
    detail,
    (ctx, color) => {
      ctx.strokeStyle = color;
      ctx.globalAlpha = 0.28;
      ctx.lineWidth = 2.1;
      ctx.beginPath();
      const shoulderY = variant % 4 === 0 ? 256 : 274;
      ctx.moveTo(180, shoulderY + 30);
      ctx.lineTo(194, shoulderY + 68);
      ctx.lineTo(207, shoulderY + 30);
      ctx.stroke();
      ctx.globalAlpha = 1;
    },
    2.3,
  );
}

function getTexture(kind, variant, pose, materials) {
  const fill = colorStyle(materials.character?.color, '#010204');
  const mainLine = materials.characterLineColor ?? '#e8e5dc';
  const dimLine = materials.characterDimLineColor ?? '#aeb5c1';
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
    alphaTest: 0.012,
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
    sprite.scale.set(2.68 * scale, 6.18 * scale, 1);
  } else {
    const height = (variant % 4 === 0 ? 4.48 : 4.18) * scale;
    sprite.scale.set(height * 0.50, height, 1);
  }

  sprite.position.z += variant * 0.006;
  parent.add(sprite);
  return sprite;
}
