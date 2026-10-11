/**
 * Stage 2: platform-independent state ownership.
 * Derived from Engine/Runtime/Core/GameState.cs and InventoryState.cs.
 * A snapshot here is NOT yet a full world save (Scheme closures/dice remain outside).
 */
export type GlobalValue = string | number | boolean;
export interface RuntimeSnapshot {
  globals: Record<string, GlobalValue>;
  inventory: Record<string, number>;
  growthLevel: number;
  restBlockers: { id: string; reason: string; location: string; node: string }[];
  notifications: string[];
  failure: { title: string; description: string } | null;
}

export class GameRuntimeState {
  private globals = new Map<string, GlobalValue>();
  private inventory = new Map<string, { name: string; count: number }>();
  private restBlockers = new Map<string, RuntimeSnapshot["restBlockers"][number]>();
  private notifications: string[] = [];
  private failure: RuntimeSnapshot["failure"] = null;
  private growthLevel = 0;

  constructor() { this.reset(); }

  reset(): void {
    this.globals = new Map<string, GlobalValue>([["location","world"],["chapter",0]]);
    this.inventory.clear();
    for (const [name, count] of [["金钱",15],["情报",0],["药品",1],["酒",0],["香烟",2]] as const) {
      this.setItemCount(name, count);
    }
    this.restBlockers.clear();
    this.notifications = [];
    this.failure = null;
    this.growthLevel = 0;
  }

  private static id(value: string): string {
    if (!value || !value.trim()) throw new Error("id must be a non-empty string");
    return value.toLowerCase(); // Unity InventoryState uses OrdinalIgnoreCase.
  }

  getGlobal(key: string): GlobalValue | false {
    if (!key) throw new Error("global key must be non-empty");
    return this.globals.get(key) ?? false; // C#: Get<object>(key) ?? false
  }

  setGlobal(key: string, value: GlobalValue): void {
    if (!key) throw new Error("global key must be non-empty");
    if (typeof value !== "string" && typeof value !== "number" && typeof value !== "boolean") {
      throw new Error("unsupported global type for portable serialization");
    }
    if (typeof value === "number" && !Number.isFinite(value)) throw new Error("global number must be finite");
    this.globals.set(key, value);
  }

  getItemCount(name: string): number { return this.inventory.get(GameRuntimeState.id(name))?.count ?? 0; }

  setItemCount(name: string, count: number): void {
    const key = GameRuntimeState.id(name);
    if (!Number.isSafeInteger(count) || count < 0) throw new Error("item count must be a non-negative integer");
    this.inventory.set(key, { name: this.inventory.get(key)?.name ?? name, count });
  }

  getGrowthLevel(): number { return this.growthLevel; }
  setGrowthLevel(value: number): void {
    if (!Number.isSafeInteger(value) || value < 0) throw new Error("growth level must be non-negative integer");
    this.growthLevel = value;
  }

  registerRestBlocker(id: string, reason: string, location: string, node: string): void {
    for (const value of [id, reason, location, node]) GameRuntimeState.id(value);
    this.restBlockers.set(id, { id, reason, location, node });
  }
  releaseRestBlocker(id: string): void { this.restBlockers.delete(id); }
  clearRestBlockers(): void { this.restBlockers.clear(); }
  hasRestBlockers(): boolean { return this.restBlockers.size > 0; }
  notify(text: string): void { this.notifications.push(text); }
  failGame(title: string, description: string): void {
    if (!title.trim() || !description.trim()) throw new Error("failure title/description must be non-empty");
    this.failure ??= { title, description };
  }

  snapshot(): RuntimeSnapshot {
    return {
      globals: Object.fromEntries(this.globals),
      inventory: Object.fromEntries([...this.inventory.values()].map(x => [x.name, x.count])),
      growthLevel: this.growthLevel,
      restBlockers: [...this.restBlockers.values()].map(x => ({ ...x })),
      notifications: [...this.notifications],
      failure: this.failure ? { ...this.failure } : null
    };
  }

  restore(data: RuntimeSnapshot): void {
    // Rebuild into a fresh state and commit only after validation.
    const candidate = new GameRuntimeState();
    candidate.globals.clear();
    for (const [key, value] of Object.entries(data.globals)) candidate.setGlobal(key, value);
    candidate.inventory.clear();
    for (const [name, count] of Object.entries(data.inventory)) candidate.setItemCount(name, count);
    candidate.setGrowthLevel(data.growthLevel);
    for (const entry of data.restBlockers) candidate.registerRestBlocker(entry.id, entry.reason, entry.location, entry.node);
    if (!Array.isArray(data.notifications) || data.notifications.some(x => typeof x !== "string")) {
      throw new Error("invalid notifications");
    }
    candidate.notifications = [...data.notifications];
    if (data.failure !== null) {
      if (!data.failure || typeof data.failure.title !== "string" || typeof data.failure.description !== "string") {
        throw new Error("invalid failure");
      }
      candidate.failGame(data.failure.title, data.failure.description);
    }
    this.globals = candidate.globals;
    this.inventory = candidate.inventory;
    this.growthLevel = candidate.growthLevel;
    this.restBlockers = candidate.restBlockers;
    this.notifications = candidate.notifications;
    this.failure = candidate.failure;
  }
}
