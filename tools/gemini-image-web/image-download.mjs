import { createHash } from 'node:crypto';
import { readFile, writeFile, unlink, rename } from 'node:fs/promises';
import { resolve } from 'node:path';

// 完整尺寸按钮存在时必须下载原文件；失败不能静默替换成低分辨率预览。
export async function downloadImage(page, image, outputDir) {
  try {
    await image.hover();
    const response = image.locator('xpath=ancestor::model-response');
    const button = response.getByRole('button', { name: /Download full size image|下载完整尺寸|下载原图/i }).last();
    let bytes, extraction;
    if (await button.count()) {
      console.error("[gemini-image-web] 正在下载完整尺寸图片。");
      const pending = page.waitForEvent('download', { timeout: 30_000 }).then(download => ({ download }), error => ({ error }));
      await button.click({ timeout: 10_000 });
      const { download, error } = await pending;
      if (error) throw error;
      const temporary = resolve(outputDir, 'original-download.tmp');
      try {
        await download.saveAs(temporary);
        bytes = await readFile(temporary);
      } finally { await unlink(temporary).catch(() => {}); }
      extraction = 'download-button';
    } else {
      // 页面没有原图入口时，明确标记为预览；不冒充原图。
      const data = await image.evaluate(async element => {
        const canvas = document.createElement('canvas');
        canvas.width = element.naturalWidth; canvas.height = element.naturalHeight;
        canvas.getContext('2d').drawImage(element, 0, 0);
        return canvas.toDataURL('image/png').split(',')[1];
      });
      bytes = Buffer.from(data, 'base64'); extraction = 'preview-canvas';
      console.error('[gemini-image-web] 当前回复没有完整尺寸下载入口，只保存网页预览图（manifest 已标记）。');
    }
    const dimensions = await page.evaluate(async base64 => {
      const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
      const bitmap = await createImageBitmap(new Blob([bytes]));
      const size = { width: bitmap.width, height: bitmap.height }; bitmap.close(); return size;
    }, bytes.toString('base64'));
    let mime_type, extension;
    if (bytes.subarray(0, 8).equals(Buffer.from([137,80,78,71,13,10,26,10]))) { mime_type = 'image/png'; extension = '.png'; }
    else if (bytes[0] === 255 && bytes[1] === 216) { mime_type = 'image/jpeg'; extension = '.jpeg'; }
    else if (bytes.toString('ascii', 0, 4) === 'RIFF' && bytes.toString('ascii', 8, 12) === 'WEBP') { mime_type = 'image/webp'; extension = '.webp'; }
    else throw new Error('下载内容不是支持的 PNG、JPEG 或 WebP 图片');
    const path = resolve(outputDir, `image${extension}`);
    const temporary = `${path}.tmp`;
    await writeFile(temporary, bytes);
    await rename(temporary, path);
    return { path, mime_type, ...dimensions, extraction, original: extraction === 'download-button', sha256: createHash('sha256').update(bytes).digest('hex') };
  } catch (cause) {
    const error = new Error(`生成图下载失败：${cause.message}。请用 resume 恢复此会话，不必重新生成。`);
    error.response_kind = 'download-error'; error.retryable = true; throw error;
  }
}
