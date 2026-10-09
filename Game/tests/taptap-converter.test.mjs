import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { gunzipSync } from "node:zlib";
import { createHash } from "node:crypto";

test("vendored user-supplied TapTap converter v2.0.5 unchanged", () => {
  const source = gunzipSync(readFileSync("vendor/taptap-converter/wx_converter.py.gz"));
  assert.equal(createHash("sha256").update(source).digest("hex"),
    "19020e1b26ce360d156da07676326885957a4b98a4624457e328518b97974354");
  assert.match(source.toString(), /CONVERTER_VERSION = "2\.0\.5"/);
});
