import { defineConfig } from "vite";

export default defineConfig({
  base: "./",
  server: { host: "0.0.0.0", fs: { allow: [".."] } },
  build: { target: "es2022" }
});
