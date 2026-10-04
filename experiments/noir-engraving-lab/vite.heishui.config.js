import {defineConfig} from 'vite';
import {fileURLToPath} from 'node:url';
export default defineConfig({build:{outDir:'dist-heishui',rollupOptions:{input:fileURLToPath(new URL('./heishui.html',import.meta.url))}}});
