export const RENDER_LAYERS = Object.freeze({
  DEFAULT: 0,
  CONTEXT: 1,
  WAREHOUSE: 2,
  ALLEY: 3,
});

export function enableLayerRecursive(root, layer) {
  root.traverse((object) => {
    object.layers.enable(layer);
  });
  return root;
}

export function setLayerRecursive(root, layer) {
  root.traverse((object) => {
    object.layers.set(layer);
  });
  return root;
}
