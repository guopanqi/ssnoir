import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import type { Plugin } from "vite";

const virtualId = "virtual:ssnoir-lips-stdlib";
const resolvedId = "\0" + virtualId;

/**
 * Package LIPS's official precompiled std.xcb INTO the JS bundle.
 * This avoids local-file/fetch APIs in WeChat/TapTap/Electron.
 * The binary comes from the pinned npm package, not a copied asset.
 */
export function bundledLipsStandardLibrary(): Plugin {
  return {
    name: "ssnoir-inline-compiled-lips-stdlib",
    enforce: "pre",
    resolveId(id) { if (id === virtualId) return resolvedId; },
    load(id) {
      if (id !== resolvedId) return;
      const bytes = readFileSync(resolve("node_modules/lips/dist/std.xcb"));
      if (bytes.subarray(0, 4).toString("ascii") !== "LIPS") {
        throw new Error("Unexpected LIPS std.xcb binary format");
      }
      return "export default " + JSON.stringify(bytes.toString("base64")) + ";";
    }
  };
}
