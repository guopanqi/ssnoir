// 截图接口：window.__skylineNoirGlm。tools/capture-skyline-noir-glm.mjs 依赖此 API。
// mode:
//   final — 全部开启（空气、灯、探照灯、烟、Bloom、成片）
//   shape — 只剩黑体块 + 窗 + 天空（无雾无灯无后期），检查剪影组织本身

export function installCaptureApi({ applyShot, camera, controls, render, setMode, setTime }) {
  window.__skylineNoirGlm = {
    ready: true,
    setShot(index) {
      const shot = applyShot(index);
      render();
      return shot;
    },
    setMode(name) {
      setMode(name);
      render();
      return name;
    },
    freeze(time) {
      setTime(time);
      render();
      return time;
    },
    render,
    info() {
      return {
        experiment: 'skyline-noir-glm',
        camera: {
          position: camera.position.toArray(),
          target: controls ? controls.target.toArray() : null,
          fov: camera.fov,
        },
      };
    },
  };
}
