export const SHOTS = [
  {
    name: '01-city-overlook',
    label: 'CITY / OVERLOOK',
    scene: 'city',
    position: [55, 52, 64],
    target: [0, 5, -3],
    fov: 37,
  },
  {
    name: '02-city-landmark',
    label: 'CITY / LANDMARK DEPTH',
    scene: 'city',
    position: [44, 17, 58],
    target: [-5, 8, -8],
    fov: 35,
  },
  {
    name: '03-alley-mouth',
    label: 'ALLEY / MOUTH',
    scene: 'alley',
    position: [13, 7.5, 23],
    target: [0, 4.2, -4],
    fov: 39,
  },
  {
    name: '04-alley-figure',
    label: 'ALLEY / FIGURE + DOOR',
    scene: 'alley',
    position: [8.4, 5.1, 7.8],
    target: [-0.4, 3.5, -5.8],
    fov: 33,
  },
];

export function applyShot(camera, controls, index, setActiveScene) {
  const shot = SHOTS[index] ?? SHOTS[0];
  setActiveScene(shot.scene);
  camera.fov = shot.fov;
  camera.position.fromArray(shot.position);
  controls.target.fromArray(shot.target);
  camera.updateProjectionMatrix();
  controls.update();
  return shot;
}
