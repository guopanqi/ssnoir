import assert from "node:assert/strict";
import { mkdtemp, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { parseArgs, validateOptions } from "./gemini-image-web.mjs";

test("parseArgs 支持重复参考图和 headed", () => {
  assert.deepEqual(
    parseArgs(["generate", "--session", "电影院", "--prompt", "test", "--reference", "a.png", "--reference", "b.jpg", "--headed"]),
    { command: "generate", session: "电影院", prompt: "test", reference: ["a.png", "b.jpg"], headed: true },
  );
});

test("validateOptions 从文件读取提示词并规范化路径", async () => {
  const root = await mkdtemp(join(tmpdir(), "gemini-image-web-test-"));
  const prompt = join(root, "prompt.txt");
  const reference = join(root, "reference.png");
  await writeFile(prompt, "  make an image  \n");
  await writeFile(reference, "fake image bytes");
  const options = await validateOptions({
    command: "generate",
    session: "test",
    name: "candidate-a",
    prompt_file: prompt,
    reference: [reference],
    output_root: root,
  });
  assert.equal(options.prompt, "make an image");
  assert.equal(options.reference[0], reference);
  assert.equal(options.timeout_ms, 90_000);
});

test("validateOptions 拒绝已移除的对话模型选项", async () => {
  await assert.rejects(
    validateOptions({ command: "doctor", mode: "pro", reference: [] }),
    /--mode 已移除/,
  );
});

test("生成等待最多 90 秒", async () => {
  await assert.rejects(
    validateOptions({ command: "doctor", timeout_ms: "90001", reference: [] }),
    /不允许超过 90 秒/,
  );
});

test("--out 推出 session/name，扩展名交给实际结果", async () => {
  const root = await mkdtemp(join(tmpdir(), "gemini-image-web-test-"));
  const o1 = await validateOptions({ command: "generate", prompt: "x", reference: [], out: join(root, "人形", "03-faceted.jpg") });
  assert.equal(o1.session, "人形");
  assert.equal(o1.name, "03-faceted");
  assert.equal(o1.out_is_dir, false);
  const o2 = await validateOptions({ command: "generate", prompt: "x", reference: [], out: join(root, "人形", "03-faceted") });
  assert.equal(o2.name, "03-faceted");
  const o3 = await validateOptions({ command: "generate", prompt: "x", reference: [], out: join(root, "人形") + "/" });
  assert.equal(o3.session, "人形");
  assert.equal(o3.out_is_dir, true);
  assert.match(o3.name, /^\d{8}-\d{6}$/);
  // 显式 --session/--name 仍优先
  const o4 = await validateOptions({ command: "generate", prompt: "x", reference: [], session: "s", name: "n", out: join(root, "人形", "a.png") });
  assert.equal(o4.session, "s"); assert.equal(o4.name, "n");
});

test("没有 --out 也没有 --session 时报错", async () => {
  await assert.rejects(validateOptions({ command: "generate", prompt: "x", reference: [] }), /--session/);
});
