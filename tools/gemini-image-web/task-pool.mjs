import { sep } from "node:path";
// 小规模并发：不隐藏排队、不自动重试；恢复和诊断独占浏览器。
export class TaskPool {
  constructor(capacity = 6) {
    if (!Number.isInteger(capacity) || capacity < 1 || capacity > 6) throw new Error("并发容量必须是 1 到 6 的整数。");
    this.capacity = capacity;
  }
  active = new Map();
  blocked = null;

  status() { return { capacity: this.capacity, active: this.active.size, stopping: this.stopping, blocked: this.blocked }; }
  stopping = false;

  async run(keys, exclusive, task) {
    if (this.stopping) throw new Error('后台正在停止，不再接受任务。');
    if (this.blocked && !exclusive) throw new Error(`后台暂停接收生成任务：${this.blocked}。处理后 stop，再重新启动。`);
    if (this.active.size >= this.capacity || (exclusive && this.active.size) || [...this.active.values()].some(job => job.exclusive)) {
      throw new Error(`后台忙：最多同时运行 ${this.capacity} 个 generate；resume 和 doctor 需要独占。请等待当前任务结束后重试。`);
    }
    if ([...this.active.values()].some(job => job.keys.some(key => keys.some(other => other === key || other.startsWith(key + sep) || key.startsWith(other + sep))))) {
      throw new Error('任务输出与正在执行的任务冲突，请使用不同的 session/name 和 --out。');
    }
    const token = Symbol();
    let finish;
    const done = new Promise(resolve => { finish = resolve; });
    this.active.set(token, { keys, exclusive, done });
    try {
      const result = await task();
      if (exclusive && this.blocked === "human-verification") this.blocked = null;
      return result;
    }
    catch (error) {
      if (["quota", "auth-error", "human-verification"].includes(error.response_kind)) this.blocked = error.response_kind;
      throw error;
    }
    finally { this.active.delete(token); finish(); }
  }

  async stop() {
    this.stopping = true;
    await Promise.all([...this.active.values()].map(job => job.done));
  }
}
