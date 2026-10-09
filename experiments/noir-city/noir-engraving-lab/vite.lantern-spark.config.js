import {defineConfig} from 'vite';
import {fileURLToPath} from 'node:url';
export default defineConfig({build:{outDir:'dist-lantern-spark',rollupOptions:{input:fileURLToPath(new URL('./lantern-spark.html',import.meta.url))}}});
