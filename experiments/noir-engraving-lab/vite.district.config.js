import {defineConfig} from 'vite';
import {fileURLToPath} from 'node:url';
export default defineConfig({build:{outDir:'dist-district',rollupOptions:{input:fileURLToPath(new URL('./district.html',import.meta.url))}}});
