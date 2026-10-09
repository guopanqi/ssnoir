import { defineConfig } from "vite";

export default defineConfig({
  build: {
    target: "es2020",
    outDir: "dist/wechat",
    emptyOutDir: true,
    minify: false,
    sourcemap: true,
    rollupOptions: {
      plugins: [{
        name: "patch-lips-iife-doc-metadata",
        generateBundle(_options, bundle) {
          for (const output of Object.values(bundle)) {
            if (output.type !== "chunk" || !output.fileName.endsWith(".js")) continue;
            const needle = 'return e.split("\\n").map((e) => {';
            if (!output.code.includes(needle)) throw new Error("LIPS diagnostic site moved");
            output.code = output.code.replace(needle,
              '// A LIPS metadata normalizer may receive a function value in the Rolldown IIFE.\\n' +
              'if (typeof e !== "string") return e;\\n' + needle);
          }
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
