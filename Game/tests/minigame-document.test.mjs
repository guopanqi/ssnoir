import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";
import ts from "typescript";

const exports = {};
runInNewContext(ts.transpileModule(readFileSync("adapters/wechat/src/document.ts", "utf8"), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020 }
}).outputText, { exports });
const facade = {
  createElement: () => ({}), createElementNS: () => ({}),
  querySelectorAll: () => [], getElementsByTagName: () => [], readyState: "complete"
};
test("partial Mini Game document gains LIPS script-scan capability without replacing host methods", () => {
  const createElement = () => ({ native: true });
  const document = { createElement, querySelectorAll: undefined };
  const global = { document };
  exports.installMiniGameDocument(global, facade);
  assert.equal(global.document, document);
  assert.equal(document.createElement, createElement);
  assert.deepEqual(document.querySelectorAll("script"), []);
  assert.equal(document.readyState, "complete");
});
test("missing Mini Game document is installed as a complete declared facade", () => {
  const global = {};
  exports.installMiniGameDocument(global, facade);
  assert.equal(global.document, facade);
});
test("partial document receives prototype event methods bound to the initialized facade", () => {
  const events = [];
  const eventFacade = Object.assign(Object.create({
    addEventListener(type) { assert.equal(this, eventFacade); events.push(type); },
    removeEventListener() {}, dispatchEvent() {}
  }), facade);
  const global = { document: {} };
  exports.installMiniGameDocument(global, eventFacade);
  global.document.addEventListener("webglcontextlost");
  assert.deepEqual(events, ["webglcontextlost"]);
});

test("screen membership includes only the body and the first host canvas", () => {
  const body = {};
  const screen = {};
  const document = { body };
  exports.installScreenCanvasMembership(document, screen);
  assert.equal(body.contains(body), true);
  assert.equal(body.contains(screen), true);
  assert.equal(body.contains({}), false);
  assert.equal(body.contains(null), false);
});
test("native body membership is preserved", () => {
  const contains = () => false;
  const document = { body: { contains } };
  exports.installScreenCanvasMembership(document, {});
  assert.equal(document.body.contains, contains);
});
