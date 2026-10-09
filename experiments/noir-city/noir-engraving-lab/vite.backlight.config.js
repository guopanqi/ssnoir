import {defineConfig} from 'vite';
import {fileURLToPath} from 'node:url';
export default defineConfig({build:{outDir:'dist-backlight',rollupOptions:{input:fileURLToPath(new URL('./backlight.html',import.meta.url))}}});
