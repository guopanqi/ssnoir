/** Owns one pending frame. Hosts supply visibility; rendering stays shared. */
export function createFrameLoop(
  render: () => void,
  request: (callback: FrameRequestCallback) => number,
  cancel: (handle: number) => void
) {
  let active = false;
  let disposed = false;
  let pending: number | undefined;
  let generation = 0;
  function tick(expectedGeneration: number): void {
    if (expectedGeneration !== generation) return;
    pending = undefined;
    if (!active || disposed) return;
    try {
      render();
      if (active && !disposed) pending = request(() => tick(expectedGeneration));
    } catch (error) {
      active = false;
      throw error;
    }
  }
  function pause(): void {
    active = false;
    generation++;
    if (pending !== undefined) cancel(pending);
    pending = undefined;
  }
  return {
    pause,
    resume(): void {
      if (active || disposed) return;
      active = true;
      tick(generation);
    },
    dispose(): void { pause(); disposed = true; }
  };
}
