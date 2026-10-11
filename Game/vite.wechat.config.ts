import { defineConfig } from "vite";
import { lipsIifeMetadata } from "./adapters/wechat/build/lips-iife-metadata";
import { pixiIntlGuard } from "./adapters/wechat/build/pixi-intl-guard";

export default defineConfig({
  plugins: [pixiIntlGuard()],
  build: {
    target: "es2020",
    outDir: "dist/wechat",
    // DevTools writes local AppID and private settings here; rebuild must preserve them.
    emptyOutDir: false,
    minify: true,
    sourcemap: false,
    rollupOptions: {
      plugins: [lipsIifeMetadata()]
    },
    lib: {
      entry: "src/wechat/probe.ts",
      name: "SSNoirWeChatFoundation",
      formats: ["iife"],
      fileName: () => "game.js"
    }
  }
});
