export const RENDER_LAYERS = Object.freeze({
  DEFAULT: 0,
  WAREHOUSE: 2,
  ALLEY: 3,
});

export function enableLayerRecursive(root, layer) {
  root.traverse((object) => {
    object.layers.enable(layer);
  });
  return root;
}
