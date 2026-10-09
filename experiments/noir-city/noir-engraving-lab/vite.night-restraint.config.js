import {defineConfig} from 'vite';
import {fileURLToPath} from 'node:url';
export default defineConfig({build:{outDir:'dist-night-restraint',rollupOptions:{input:fileURLToPath(new URL('./night-restraint.html',import.meta.url))}}});
