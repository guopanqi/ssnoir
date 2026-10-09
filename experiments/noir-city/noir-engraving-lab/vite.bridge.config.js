import {defineConfig} from 'vite';
import {fileURLToPath} from 'node:url';
export default defineConfig({build:{outDir:'dist-bridge',rollupOptions:{input:fileURLToPath(new URL('./bridge.html',import.meta.url))}}});
