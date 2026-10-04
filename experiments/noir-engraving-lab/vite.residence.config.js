import {defineConfig} from 'vite';
import {fileURLToPath} from 'node:url';
export default defineConfig({build:{outDir:'dist-residence',rollupOptions:{input:fileURLToPath(new URL('./residence.html',import.meta.url))}}});
