import * as THREE from 'three';

export function createNoirShader(profile) {
  const p = profile.print;

  return {
    uniforms: {
      tDiffuse: { value: null },
      uEnabled: { value: 1.0 },
      uLevels: { value: p.levels },
      uDither: { value: p.dither },
      uDitherScale: { value: p.ditherScale },
      uInBlack: { value: p.inBlack },
      uInWhite: { value: p.inWhite },
      uGamma: { value: p.gamma },
      uVignette: { value: p.vignette },
      uResolution: { value: new THREE.Vector2(1280, 720) },
      uTime: { value: 0 },
    },

    vertexShader: /* glsl */`
      varying vec2 vUv;
      void main() {
        vUv = uv;
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }
    `,

    fragmentShader: /* glsl */`
      uniform sampler2D tDiffuse;
      uniform float uEnabled;
      uniform float uLevels;
      uniform float uDither;
      uniform float uDitherScale;
      uniform float uInBlack;
      uniform float uInWhite;
      uniform float uGamma;
      uniform float uVignette;
      uniform vec2 uResolution;
      uniform float uTime;
      varying vec2 vUv;

      float bayer4(vec2 cell) {
        float x = mod(cell.x, 4.0);
        float y = mod(cell.y, 4.0);

        if (y < 1.0) {
          if (x < 1.0) return 0.0;
          if (x < 2.0) return 8.0;
          if (x < 3.0) return 2.0;
          return 10.0;
        }
        if (y < 2.0) {
          if (x < 1.0) return 12.0;
          if (x < 2.0) return 4.0;
          if (x < 3.0) return 14.0;
          return 6.0;
        }
        if (y < 3.0) {
          if (x < 1.0) return 3.0;
          if (x < 2.0) return 11.0;
          if (x < 3.0) return 1.0;
          return 9.0;
        }

        if (x < 1.0) return 15.0;
        if (x < 2.0) return 7.0;
        if (x < 3.0) return 13.0;
        return 5.0;
      }

      float luma(vec3 c) {
        return dot(c, vec3(0.2126, 0.7152, 0.0722));
      }

      vec3 ramp(float x) {
        vec3 a = vec3(0.020, 0.028, 0.045);
        vec3 b = vec3(0.070, 0.105, 0.165);
        vec3 c = vec3(0.285, 0.360, 0.500);
        vec3 d = vec3(0.930, 0.955, 1.000);

        vec3 r = mix(a, b, clamp(x / 0.30, 0.0, 1.0));
        r = mix(r, c, clamp((x - 0.30) / 0.36, 0.0, 1.0));
        r = mix(r, d, clamp((x - 0.66) / 0.34, 0.0, 1.0));
        return r;
      }

      void main() {
        vec4 src = texture2D(tDiffuse, vUv);
        if (uEnabled < 0.5) {
          gl_FragColor = src;
          return;
        }

        // OutputPass 已经完成 tone mapping 与 sRGB 输出变换。
        // 这里直接把输入当作“屏幕显示值”，与 Unity SSNoirStylize 的契约一致。
        vec3 display = clamp(src.rgb, 0.0, 1.0);
        float l = clamp(
          (luma(display) - uInBlack) / max(0.001, uInWhite - uInBlack),
          0.0,
          1.0
        );
        l = pow(l, uGamma);

        float scale = max(1.0, uDitherScale * uResolution.y / 720.0);
        vec2 cell = floor(gl_FragCoord.xy / scale);
        float bayer = (bayer4(cell) + 0.5) / 16.0;
        // 印刷噪声只存在于中间调；深黑和高光保持干净。
        float midtoneMask =
          smoothstep(0.055, 0.22, l) *
          (1.0 - smoothstep(0.78, 0.96, l));
        float effectiveDither = clamp(uDither, 0.0, 1.0) * midtoneMask;
        bayer = mix(0.5, bayer, effectiveDither);

        float steps = max(2.0, uLevels) - 1.0;
        float q = clamp(floor(l * steps + bayer) / steps, 0.0, 1.0);
        vec3 styled = ramp(q);

        float grain = fract(
          sin(dot(gl_FragCoord.xy + uTime * 17.0, vec2(12.9898, 78.233))) * 43758.5453
        ) - 0.5;
        styled = clamp(styled + grain * 0.003 * midtoneMask, 0.0, 1.0);

        // 暗角属于最终媒介，而不是场景本身；因此只存在于 Print 层。
        vec2 centered = vUv * 2.0 - 1.0;
        float edge = smoothstep(0.45, 1.45, dot(centered, centered));
        styled *= 1.0 - edge * uVignette;

        gl_FragColor = vec4(styled, src.a);
      }
    `,
  };
}
