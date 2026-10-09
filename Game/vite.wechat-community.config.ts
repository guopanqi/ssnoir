import { defineConfig } from "vite";
import threePlatformAdapter from "@minisheep/three-platform-adapter/plugin";
export default defineConfig({
  plugins: [threePlatformAdapter()],
  build: {
    target: "es2020",
    outDir: "dist/wechat-community",
    emptyOutDir: true,
    sourcemap: false,
    minify: true,
    rollupOptions: {
      plugins: [{
        name: "patch-lips-iife-doc-metadata",
        renderChunk(code) {
          const needle = 'return e.split("\\n").map((e) => {';
          if (!code.includes(needle)) throw new Error("LIPS IIFE doc metadata patch site moved");
          return { code: code.replace(needle, 'if (typeof e !== "string") return e; ' + needle), map: null };
        }
      }]
    },
    lib: {
      entry: "src/wechat-community/probe.ts",
      name: "SSNoirWechatCommunityProbe",
      formats: ["iife"],
      fileName: () => "game.js"
    }
  }
});
