import { defineConfig } from "vite";
import { pixiIntlGuard } from "./adapters/wechat/build/pixi-intl-guard";
import { bundledLipsStandardLibrary } from "./vite.lips-stdlib";

export default defineConfig({
  plugins: [bundledLipsStandardLibrary(), pixiIntlGuard()],
  base: "./",
  server: { host: "0.0.0.0", fs: { allow: [".."] } },
  build: { target: "es2022" }
});
