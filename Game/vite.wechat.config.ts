import { defineConfig } from "vite";

export default defineConfig({
  build: {
    target: "es2020",
    outDir: "dist/wechat",
    // DevTools writes local AppID and private settings here; rebuild must preserve them.
    emptyOutDir: false,
    minify: true,
    sourcemap: false,
    rollupOptions: {
      plugins: [{
        name: "patch-lips-iife-doc-metadata",
        renderChunk(code) {
          // Guard a LIPS documentation-only helper before the IIFE minifier runs.
          // Fail closed if the upstream function changes; never silently patch unknown code.
          const needle = 'return e.split("\\n").map((e) => {';
          if (!code.includes(needle)) throw new Error("LIPS IIFE doc metadata patch site moved");
          return { code: code.replace(needle, 'if (typeof e !== "string") return e; ' + needle), map: null };
        }
      }]
    },
    lib: {
      entry: "src/wechat/probe.ts",
      name: "SSNoirWeChatFoundation",
      formats: ["iife"],
      fileName: () => "game.js"
    }
  }
});
