import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { spawn, execFileSync } from "node:child_process";
import { setTimeout as delay } from "node:timers/promises";

const cli = "node_modules/wrangler/bin/wrangler.js";
const run = (...args) => execFileSync(process.execPath, [cli, ...args], { encoding: "utf8" });
run("d1", "migrations", "apply", "ssnoir-feedback", "--local");
const server = spawn(process.execPath, [cli, "dev", "--ip", "127.0.0.1", "--port", "8792"], {
  stdio: ["ignore", "pipe", "pipe"],
});
let logs = "";
server.stdout.on("data", data => { logs += data; });
server.stderr.on("data", data => { logs += data; });
const url = "http://127.0.0.1:8792/feedback";
const id = randomUUID().replaceAll("-", "");
const payload = { id, text: "接口集成检查：中文反馈与换行\n第二行", version: "test", location: "码头",
  inScenario: true, worldDay: 3 };
const send = value => fetch(url, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(value) });
try {
  let ready = false;
  for (let i = 0; i < 100; i++) {
    if (server.exitCode !== null) throw new Error(logs);
    try { ready = (await fetch(url)).status === 405; } catch {}
    if (ready) break;
    await delay(200);
  }
  assert.ok(ready, logs);
  const preflight = await fetch(url, { method: "OPTIONS", headers: { Origin: "https://game.example" } });
  assert.equal(preflight.status, 204);
  assert.equal(preflight.headers.get("Access-Control-Allow-Origin"), "*");
  assert.equal((await fetch(url)).status, 405);
  assert.equal((await fetch(url.replace("/feedback", "/"))).status, 404);
  assert.equal((await send({ ...payload, text: "  \n" })).status, 400);
  assert.equal((await send({ ...payload, text: "字".repeat(2001) })).status, 400);
  assert.equal((await send({ ...payload, worldDay: 1.5 })).status, 400);
  assert.equal((await send({ ...payload, id: "invalid" })).status, 400);
  assert.equal((await fetch(url, { method: "POST", body: "not json" })).status, 415);
  assert.equal((await fetch(url, { method: "POST", headers: { "Content-Type": "application/json" }, body: "{" })).status, 400);
  assert.equal((await send({ ...payload, text: "x".repeat(17000) })).status, 413);
  const saved = await send(payload);
  assert.equal(saved.status, 200);
  assert.deepEqual(await saved.json(), { ok: true, id });
  assert.equal((await send(payload)).status, 200);
  assert.equal((await send({ ...payload, text: "同一 ID 的其他内容" })).status, 409);
  const result = JSON.parse(run("d1", "execute", "ssnoir-feedback", "--local", "--json", "--command",
    `SELECT * FROM feedback WHERE id = '${id}'`));
  const rows = result[0].results;
  assert.equal(rows.length, 1);
  assert.equal(rows[0].text, payload.text);
  assert.equal(rows[0].in_scenario, 1);
  assert.equal(rows[0].world_day, 3);
  assert.ok(rows[0].created_at.endsWith("Z"));
  let limited = false;
  for (let i = 0; i < 65; i++) {
    if ((await send(payload)).status === 429) { limited = true; break; }
  }
  assert.ok(limited, "submission flood must be rate limited");
  console.log("通过：中文入库、CORS、拒绝公开读取、输入约束、重试去重、ID 冲突与限流。");
} finally {
  if (server.exitCode === null) {
    const exited = new Promise(resolve => server.once("exit", resolve));
    server.kill("SIGTERM");
    await exited;
  }
  run("d1", "execute", "ssnoir-feedback", "--local", "--command", `DELETE FROM feedback WHERE id = '${id}'`);
}
