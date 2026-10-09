import { evaluateScheme } from "../scheme/evaluate";

/**
 * WeChat HOST PROBE, not yet the full Three.js/PixiJS port.
 * Uses neither window nor document. Build success is NOT device compatibility.
 */
declare const wx: {
  createCanvas(): {
    width: number;
    height: number;
    getContext(kind: "2d"): {
      fillStyle: string;
      font: string;
      fillRect(x: number, y: number, w: number, h: number): void;
      fillText(text: string, x: number, y: number): void;
    } | null;
  };
  getSystemInfoSync(): { windowWidth: number; windowHeight: number };
};

const canvas = wx.createCanvas();
const info = wx.getSystemInfoSync();
canvas.width = info.windowWidth;
canvas.height = info.windowHeight;
const ctx = canvas.getContext("2d");
if (!ctx) throw new Error("WeChat canvas 2D context unavailable");
ctx.fillStyle = "#111217";
ctx.fillRect(0, 0, canvas.width, canvas.height);
ctx.font = "18px sans-serif";
ctx.fillStyle = "#f0dec2";
ctx.fillText("SSNoir · WeChat host probe", 24, 55);
const value = evaluateScheme("(+ 19 23)");
ctx.fillText("Scheme result: " + String(value), 24, 96);
if (value !== 42) throw new Error("Scheme runtime incompatible in WeChat host");
