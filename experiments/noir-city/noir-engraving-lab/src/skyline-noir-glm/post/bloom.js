import {
  AdditiveBlending,
  HalfFloatType,
  NoBlending,
  ShaderMaterial,
  Vector2,
  Vector3,
  WebGLRenderTarget,
} from 'three';
import { Pass, FullScreenQuad } from 'three/addons/postprocessing/Pass.js';

// NoirBloom：参照 UnrealBloomPass 的架构（亮度高通 -> 逐级降分辨率 separable blur -> 加权合成）
// 自写的标准契约版本：所有 pass 都写 writeBuffer、needsSwap = true，
// 不做"就地加法回写 readBuffer"——那是 UnrealBloomPass 在 headless 环境下把缓冲写空的坑。
// 逐级染色（紧芯偏冷、大晕偏暖）是 UnrealBloomPass bloomTintColors 的思想。

const VERT = /* glsl */ `
varying vec2 vUv;
void main() {
  vUv = uv;
  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
}
`;

// 亮度高通（软膝，UE4 式）：阈值附近的亮部平滑进入 bloom
const THRESHOLD_FRAG = /* glsl */ `
uniform sampler2D tDiffuse;
uniform float uThreshold;
uniform float uKnee;
varying vec2 vUv;
void main() {
  vec3 c = texture2D(tDiffuse, vUv).rgb;
  float br = max(c.r, max(c.g, c.b));
  float knee = uThreshold * uKnee + 1e-4;
  float soft = clamp(br - uThreshold + knee, 0.0, 2.0 * knee);
  soft = soft * soft / (4.0 * knee);
  float contrib = max(soft, br - uThreshold) / max(br, 1e-4);
  gl_FragColor = vec4(c * contrib, 1.0);
}
`;

// separable 9-tap gaussian；uTexel 是本级 RT 的 texel 尺寸
const BLUR_FRAG = /* glsl */ `
uniform sampler2D tDiffuse;
uniform vec2 uDirection;
varying vec2 vUv;
void main() {
  vec3 sum = texture2D(tDiffuse, vUv).rgb * 0.227027;
  vec2 off1 = uDirection * 1.3846153846;
  vec2 off2 = uDirection * 3.2307692308;
  sum += texture2D(tDiffuse, vUv + off1).rgb * 0.3162162162;
  sum += texture2D(tDiffuse, vUv - off1).rgb * 0.3162162162;
  sum += texture2D(tDiffuse, vUv + off2).rgb * 0.0702702703;
  sum += texture2D(tDiffuse, vUv - off2).rgb * 0.0702702703;
  gl_FragColor = vec4(sum, 1.0);
}
`;

// 合成：3 级 mip 各自染色加权
const COMPOSITE_FRAG = /* glsl */ `
uniform sampler2D tMip0;
uniform sampler2D tMip1;
uniform sampler2D tMip2;
uniform vec3 uTint0;
uniform vec3 uTint1;
uniform vec3 uTint2;
varying vec2 vUv;
void main() {
  vec3 c = texture2D(tMip0, vUv).rgb * uTint0 * 1.0
         + texture2D(tMip1, vUv).rgb * uTint1 * 0.85
         + texture2D(tMip2, vUv).rgb * uTint2 * 0.65;
  gl_FragColor = vec4(c, 1.0);
}
`;

// 终回写：场景 + bloom，NoBlending 全量写入 writeBuffer
const ADD_FRAG = /* glsl */ `
uniform sampler2D tScene;
uniform sampler2D tBloom;
uniform float uStrength;
varying vec2 vUv;
void main() {
  vec3 scene = texture2D(tScene, vUv).rgb;
  vec3 bloom = texture2D(tBloom, vUv).rgb;
  gl_FragColor = vec4(scene + bloom * uStrength, 1.0);
}
`;

function makeRT(w, h) {
  return new WebGLRenderTarget(Math.max(w, 1), Math.max(h, 1), {
    type: HalfFloatType,
    depthBuffer: false,
    stencilBuffer: false,
  });
}

export class NoirBloomPass extends Pass {
  constructor(resolution, params) {
    super();
    this.needsSwap = true;
    this.strength = params.strength;

    const res = resolution.clone();
    const half = new Vector2(Math.floor(res.x / 2), Math.floor(res.y / 2));

    this.rtBright = makeRT(half.x, half.y);
    this.rtMip = [];
    for (let i = 0; i < 3; i++) {
      const w = Math.max(Math.floor(half.x / (2 ** i)), 1);
      const h = Math.max(Math.floor(half.y / (2 ** i)), 1);
      this.rtMip.push({ h: makeRT(w, h), v: makeRT(w, h) });
    }
    this.rtComposite = makeRT(half.x, half.y);

    const mat = (frag, uniforms) => new ShaderMaterial({
      vertexShader: VERT, fragmentShader: frag, uniforms, depthTest: false, depthWrite: false, blending: NoBlending,
    });

    this.thresholdMat = mat(THRESHOLD_FRAG, {
      tDiffuse: { value: null },
      uThreshold: { value: params.threshold },
      uKnee: { value: 0.6 },
    });
    this.blurMat = mat(BLUR_FRAG, {
      tDiffuse: { value: null },
      uDirection: { value: new Vector2() },
    });
    this.compositeMat = mat(COMPOSITE_FRAG, {
      tMip0: { value: null }, tMip1: { value: null }, tMip2: { value: null },
      uTint0: { value: new Vector3(...params.tintCore) },
      uTint1: { value: new Vector3(...params.tintMid) },
      uTint2: { value: new Vector3(...params.tintWide) },
    });
    this.addMat = mat(ADD_FRAG, {
      tScene: { value: null },
      tBloom: { value: null },
      uStrength: { value: params.strength },
    });

    this.fsQuad = new FullScreenQuad(null);
  }

  setSize(w, h) {
    const halfX = Math.max(Math.floor(w / 2), 1);
    const halfY = Math.max(Math.floor(h / 2), 1);
    this.rtBright.setSize(halfX, halfY);
    for (let i = 0; i < 3; i++) {
      const mw = Math.max(Math.floor(halfX / (2 ** i)), 1);
      const mh = Math.max(Math.floor(halfY / (2 ** i)), 1);
      this.rtMip[i].h.setSize(mw, mh);
      this.rtMip[i].v.setSize(mw, mh);
    }
    this.rtComposite.setSize(halfX, halfY);
  }

  blur(renderer, sourceRT, targetRT, dirX, dirY) {
    this.blurMat.uniforms.tDiffuse.value = sourceRT.texture;
    this.blurMat.uniforms.uDirection.value.set(
      dirX / targetRT.width,
      dirY / targetRT.height,
    );
    renderer.setRenderTarget(targetRT);
    this.fsQuad.material = this.blurMat;
    this.fsQuad.render(renderer);
  }

  render(renderer, writeBuffer, readBuffer) {
    // 高通：场景 -> 半分辨率 bright
    this.thresholdMat.uniforms.tDiffuse.value = readBuffer.texture;
    renderer.setRenderTarget(this.rtBright);
    this.fsQuad.material = this.thresholdMat;
    this.fsQuad.render(renderer);

    // 逐级横向/纵向模糊；上一级的纵模糊结果作为下一级输入（分辨率自然减半）
    let input = this.rtBright;
    for (let i = 0; i < 3; i++) {
      const mip = this.rtMip[i];
      this.blur(renderer, input, mip.h, 1, 0);
      this.blur(renderer, mip.h, mip.v, 0, 1);
      input = mip.v;
    }

    // 合成
    this.compositeMat.uniforms.tMip0.value = this.rtMip[0].v.texture;
    this.compositeMat.uniforms.tMip1.value = this.rtMip[1].v.texture;
    this.compositeMat.uniforms.tMip2.value = this.rtMip[2].v.texture;
    renderer.setRenderTarget(this.rtComposite);
    this.fsQuad.material = this.compositeMat;
    this.fsQuad.render(renderer);

    // 场景 + bloom 全量写回 writeBuffer
    this.addMat.uniforms.tScene.value = readBuffer.texture;
    this.addMat.uniforms.tBloom.value = this.rtComposite.texture;
    this.addMat.uniforms.uStrength.value = this.strength;
    renderer.setRenderTarget(this.renderToScreen ? null : writeBuffer);
    this.fsQuad.material = this.addMat;
    this.fsQuad.render(renderer);
  }

  dispose() {
    this.rtBright.dispose();
    this.rtComposite.dispose();
    for (const m of this.rtMip) { m.h.dispose(); m.v.dispose(); }
    this.fsQuad.dispose();
  }
}
