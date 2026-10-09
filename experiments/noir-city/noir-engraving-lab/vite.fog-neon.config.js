import {defineConfig} from 'vite';
import {fileURLToPath} from 'node:url';
export default defineConfig({build:{outDir:'dist-fog-neon',rollupOptions:{input:fileURLToPath(new URL('./fog-neon.html',import.meta.url))}}});
