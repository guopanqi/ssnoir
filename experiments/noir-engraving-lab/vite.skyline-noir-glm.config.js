import {defineConfig} from 'vite';
import {fileURLToPath} from 'node:url';
export default defineConfig({build:{outDir:'dist-skyline-noir-glm',rollupOptions:{input:fileURLToPath(new URL('./skyline-noir-glm.html',import.meta.url))}}});
