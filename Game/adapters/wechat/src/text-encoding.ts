import { hadNativeTextEncoder } from "./global-scope";
import "../vendor/fast-text-encoding/text.min.js";

// TextEncoder takes a USVString: isolated UTF-16 surrogates become U+FFFD.
// The vendored UTF-8 encoder needs this normalization at its host boundary.
if (!hadNativeTextEncoder) {
  const Utf8Encoder = globalThis.TextEncoder;
  globalThis.TextEncoder = class extends Utf8Encoder {
    encode(input = "") {
      const text = String(input);
      let normalized = "";
      for (let index = 0; index < text.length; index++) {
        const code = text.charCodeAt(index);
        if (code >= 0xd800 && code <= 0xdbff) {
          const next = text.charCodeAt(index + 1);
          if (next >= 0xdc00 && next <= 0xdfff) normalized += text[index] + text[++index];
          else normalized += "\ufffd";
        } else normalized += code >= 0xdc00 && code <= 0xdfff ? "\ufffd" : text[index];
      }
      return super.encode(normalized);
    }
  };
}
