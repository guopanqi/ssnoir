// Deliberately ES5, dependency-free: establish whether the host runs game.js.
console.log('[SSNoir TapTap host probe v1] entry reached');
var host = typeof wx !== 'undefined' ? wx : typeof tap !== 'undefined' ? tap : null;
if (!host) throw new Error('Neither wx nor tap host API is available');
if (typeof host.showModal === 'function') {
  host.showModal({ title: 'SSNoir 宿主探针 v1', content: '入口已执行，未加载游戏底座。', showCancel: false });
}
var canvas = host.createCanvas();
var info = host.getSystemInfoSync();
canvas.width = info.windowWidth || 640;
canvas.height = info.windowHeight || 360;
var context = canvas.getContext('2d');
if (!context) throw new Error('Host probe cannot create a 2D canvas');
context.fillStyle = '#15212b';
context.fillRect(0, 0, canvas.width, canvas.height);
context.fillStyle = '#ffffff';
context.font = '24px sans-serif';
context.fillText('SSNoir host probe v1: entry executed', 20, 60);
console.log('[SSNoir TapTap host probe v1] canvas drawn');
