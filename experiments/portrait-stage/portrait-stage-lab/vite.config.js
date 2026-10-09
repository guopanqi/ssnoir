import {defineConfig} from 'vite';
import {fileURLToPath} from 'node:url';
export default defineConfig({build:{rollupOptions:{input:{worldStage:fileURLToPath(new URL('./world-stage.html',import.meta.url)),main:fileURLToPath(new URL('./index.html',import.meta.url)),third:fileURLToPath(new URL('./third.html',import.meta.url)),baselines:fileURLToPath(new URL('./baselines.html',import.meta.url))}}}});
