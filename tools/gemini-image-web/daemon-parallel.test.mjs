import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, rm, symlink } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { serve, connectDaemon, validateOptions } from './gemini-image-web.mjs';
import { TaskPool } from './task-pool.mjs';
const defer = () => { let resolve; const promise = new Promise(r => { resolve = r; }); return { promise, resolve }; };

test('真实 socket 并发日志隔离、状态、忙拒绝和停止排空', async () => {
  const profile = await mkdtemp(join(tmpdir(), 'gemini-pool-'));
  const gates = { a: defer(), b: defer() }, entered = { a: defer(), b: defer() };
  let closed = false;
  const serving = serve({ profile, concurrency: 2 }, { close: async () => { closed = true; } }, {
    generate: async job => {
      console.log(`start-${job.name}`);
      entered[job.name].resolve();
      await gates[job.name].promise;
      console.error(`end-${job.name}`);
    },
  });
  const request = (command, extra = {}, events = []) => connectDaemon({ command, profile, ...extra }, event => events.push(event));
  try {
    // 等 socket 监听就绪，不启动任何浏览器。
    let ready;
    for (let i = 0; i < 100; i++) {
      try { ready = await request('status'); break; }
      catch (e) { if (!['ENOENT', 'ECONNREFUSED'].includes(e.code)) throw e; await new Promise(r => setTimeout(r, 10)); }
    }
    assert.ok(ready?.ok);
    assert.equal((await request('start', { concurrency: 2 })).ok, true);
    assert.match((await request('start', { concurrency: 3 })).error, /后台容量/);
    const ea = [], eb = [];
    const common = { output_root: profile, session: 's' };
    const a = request('generate', { ...common, name: 'a' }, ea);
    await entered.a.promise;
    await symlink(profile, join(profile, 'alias'));
    assert.match((await request('generate', { ...common, output_root: join(profile, 'alias'), name: 'a!' })).error, /冲突/);
    const b = request('generate', { ...common, name: 'b' }, eb);
    await Promise.all([entered.a.promise, entered.b.promise]);
    const state = await request('status');
    assert.equal(JSON.parse(state.stdout[0]).active, 2);
    assert.equal((await request('generate', { ...common, name: 'c' })).ok, false);
    const stopping = request('stop');
    gates.a.resolve(); await a;
    assert.equal(closed, false);
    gates.b.resolve(); await b; await stopping; await serving;
    assert.deepEqual(ea.map(e => e.line), ['start-a', 'end-a']);
    assert.deepEqual(eb.map(e => e.line), ['start-b', 'end-b']);
    assert.equal(closed, true);
  } finally {
    gates.a.resolve(); gates.b.resolve();
    if (!closed) await request('stop').catch(() => {});
    await serving;
    await rm(profile, { recursive: true, force: true });
  }
});

test('容量配置与额度熔断；不影响已提交任务，恢复允许独占执行', async () => {
  for (const capacity of [0, 7, 1.5, NaN]) assert.throws(() => new TaskPool(capacity));
  const pool = new TaskPool(4), gate = defer();
  const jobs = Array.from({ length: 3 }, (_, i) => pool.run([String(i)], false, () => gate.promise));
  await assert.rejects(pool.run(['quota'], false, async () => { throw Object.assign(new Error('quota'), { response_kind: 'quota' }); }), /quota/);
  await assert.rejects(pool.run(['new'], false, () => {}), /暂停/);
  assert.equal(pool.status().active, 3);
  gate.resolve(); await Promise.all(jobs);
  await pool.run([], true, () => {});
  for (const concurrency of ['0', '7', '1.5']) await assert.rejects(validateOptions({ command: 'serve', concurrency }), /concurrency/);
});

test('客户端断开不取消已接收的任务；任务仍可结束并释放容量', async () => {
  const { createConnection } = await import('node:net');
  const profile = await mkdtemp(join(tmpdir(), 'gemini-disconnect-'));
  const entered = defer(), finish = defer(), completed = defer();
  const serving = serve({ profile }, { close: async () => {} }, {
    generate: async () => { entered.resolve(); await finish.promise; console.log('saved'); completed.resolve(); },
  });
  const status = () => connectDaemon({ command: 'status', profile }, () => {});
  try {
    for (let i = 0; i < 100; i++) {
      try { await status(); break; }
      catch (e) { if (!['ENOENT', 'ECONNREFUSED'].includes(e.code)) throw e; await new Promise(r => setTimeout(r, 10)); }
    }
    const socket = createConnection(join(profile, '.gemini-image-web.sock'));
    await new Promise((resolve, reject) => { socket.once('connect', resolve); socket.once('error', reject); });
    socket.write(JSON.stringify({ command: 'generate', profile, output_root: profile, session: 's', name: 'a' }) + '\n');
    await entered.promise;
    socket.destroy();
    assert.equal(JSON.parse((await status()).stdout[0]).active, 1);
    finish.resolve(); await completed.promise;
    // 状态请求经过另一个 socket，处理时任务的 finally 已完成。
    assert.equal(JSON.parse((await status()).stdout[0]).active, 0);
  } finally {
    finish.resolve();
    await connectDaemon({ command: 'stop', profile }, () => {});
    await serving;
    await rm(profile, { recursive: true, force: true });
  }
});

test('登录错误也暂停新生成；普通生成失败不暂停服务', async () => {
  const pool = new TaskPool();
  await assert.rejects(pool.run([], false, async () => { throw new Error('ordinary'); }), /ordinary/);
  assert.equal(pool.status().blocked, null);
  await assert.rejects(pool.run([], false, async () => { throw Object.assign(new Error('login'), { response_kind: 'auth-error' }); }), /login/);
  assert.equal(pool.status().blocked, 'auth-error');
  await assert.rejects(pool.run([], false, async () => {}), /暂停/);
});
