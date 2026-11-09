import test from 'node:test';
import assert from 'node:assert/strict';
import { TaskPool } from './task-pool.mjs';

const deferred = () => { let resolve; const promise = new Promise(r => { resolve = r; }); return { promise, resolve }; };

test('两个任务并发，拒绝冲突、第三个任务和独占操作；失败释放容量', async () => {
  const pool = new TaskPool(2), gate = deferred();
  const first = pool.run(['a'], false, () => gate.promise);
  await assert.rejects(pool.run(['a'], false, () => {}), /冲突/);
  await assert.rejects(pool.run([], true, () => {}), /后台忙/);
  const second = pool.run(['b'], false, () => gate.promise);
  assert.equal(pool.active.size, 2);
  await assert.rejects(pool.run(['c'], false, () => {}), /后台忙/);
  gate.resolve();
  await Promise.all([first, second]);
  await assert.rejects(pool.run(['a'], false, () => { throw Error('failed'); }), /failed/);
  await pool.run([], true, async () => {
    await assert.rejects(pool.run(['b'], false, () => {}), /后台忙/);
  });
  assert.equal(pool.active.size, 0);
});

test('停止等待所有活动任务，且拒绝新任务', async () => {
  const pool = new TaskPool(), a = deferred(), b = deferred();
  const first = pool.run(['a'], false, () => a.promise);
  const second = pool.run(['b'], false, () => b.promise);
  let stopped = false;
  const stopping = pool.stop().then(() => { stopped = true; });
  await assert.rejects(pool.run(['c'], false, () => {}), /正在停止/);
  a.resolve(); await first;
  assert.equal(stopped, false);
  b.resolve(); await second; await stopping;
  assert.equal(stopped, true);
});


test('默认六个任务同时执行，第七个明确报忙', async () => {
  const pool = new TaskPool(), gate = deferred();
  assert.equal(pool.capacity, 6);
  const jobs = Array.from({ length: 6 }, (_, i) => pool.run([String(i)], false, () => gate.promise));
  assert.equal(pool.active.size, 6);
  try { await assert.rejects(pool.run(['seventh'], false, () => {}), /后台忙/); }
  finally { gate.resolve(); await Promise.all(jobs); }
  assert.equal(pool.active.size, 0);
});
