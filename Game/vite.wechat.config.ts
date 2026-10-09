import { defineConfig } from "vite";

export default defineConfig({
  build: {
    target: "es2020",
    outDir: "dist/wechat",
    emptyOutDir: true,
    minify: false,
    lib: {
      entry: "src/wechat/probe.ts",
      name: "SSNoirWeChatProbe",
      formats: ["iife"],
      fileName: () => "game.js"
    }
  }
});
