/** Portable binary decoder; Mini Game environments need not provide atob or Buffer. */
export function base64Bytes(source: string): Uint8Array {
  if (source.length % 4 !== 0) throw new Error("Invalid base64 length");
  const alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
  const padding = source.endsWith("==") ? 2 : source.endsWith("=") ? 1 : 0;
  const output = new Uint8Array(source.length / 4 * 3 - padding);
  let accumulator = 0;
  let bits = 0;
  let cursor = 0;
  for (const char of source) {
    if (char === "=") break;
    const digit = alphabet.indexOf(char);
    if (digit < 0) throw new Error("Invalid base64 character");
    accumulator = (accumulator << 6) | digit;
    bits += 6;
    if (bits >= 8) {
      bits -= 8;
      output[cursor++] = (accumulator >>> bits) & 255;
    }
  }
  if (cursor !== output.length) throw new Error("Invalid base64 payload");
  return output;
}
