import {defineConfig} from 'vite';
import {fileURLToPath} from 'node:url';
export default defineConfig({build:{outDir:'dist-city-interface',rollupOptions:{input:fileURLToPath(new URL('./city-interface.html',import.meta.url))}}});
