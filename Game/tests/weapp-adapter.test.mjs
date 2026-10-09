import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createHash } from "node:crypto";

const upstream = {
  "LICENSE": "c1391085c85075a049caf3b87994d715e86db580",
  "src/Canvas.js": "916b720636394fe54a45e35d10ebee6f3867d447",
  "src/Element.js": "cf2233bd67324aeb024fe981daa142dd51c331db",
  "src/EventTarget.js": "bd96a6557c5563d1f64ad145e851456ab16d67ec",
  "src/HTMLElement.js": "bbcee1b158d9cd663a57b33bba32bf65788682c3",
  "src/Node.js": "874712f8e5f72d1f6f3bc999824dfa5fd79bdd03",
  "src/WindowProperties.js": "5df9438177517a12e804c867207846a3a54f5e72",
  "src/performance.js": "3c5975427807c02319b52667c12be7739300ad2f",
  "src/util/index.js": "177804c7aba909945e5a7a1f7f40ffd6f7cda81e",
  "src/util/mixin.js": "3d5e5a7380f4616acb7e8b37e6d440d6e24cf09b"
};

test("MIT mini-game adapter upstream source bytes remain unmodified", () => {
  for (const [path, expectedSha] of Object.entries(upstream)) {
    const bytes = readFileSync("vendor/weapp-adapter/" + path);
    const actualSha = createHash("sha1")
      .update(`blob ${bytes.length}\0`).update(bytes).digest("hex");
    assert.equal(actualSha, expectedSha, "upstream blob drift: " + path);
  }
});
