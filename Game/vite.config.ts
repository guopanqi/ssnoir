import { defineConfig } from "vite";
import { pixiIntlGuard } from "./build/pixi-intl-guard";

export default defineConfig({
  plugins: [pixiIntlGuard()],
  base: "./",
  server: { host: "0.0.0.0", fs: { allow: [".."] } },
  build: { target: "es2022" }
});
