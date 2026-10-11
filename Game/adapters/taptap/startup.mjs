/** Produce a small ES5 entry that reports errors before dependencies initialize. */
export function createTapTapEntry(modulePath, { title = "Mini Game 启动失败", tag = "MiniGame startup" } = {}) {
  if (!/^\.\/[^\r\n]+\.js$/.test(modulePath)) throw new Error("Entry module must be a local .js path");
  return `function reportStartupFailure(error) {
  var reason = error && (error.message || error.errMsg) || String(error);
  var message = String(error && error.name || 'Error') + ': ' + String(reason);
  if (error && error.stack) message += '\\n' + String(error.stack);
  console.error(${JSON.stringify("[" + tag + "]")}, message);
  if (typeof wx !== 'undefined' && typeof wx.showModal === 'function') {
    wx.showModal({title:${JSON.stringify(title)}, content:message.slice(0,650), showCancel:false});
  }
}
console.log(${JSON.stringify("[" + tag + "] entry reached")});
try {
  if (typeof wx === 'undefined') throw new Error('TapTap WeChat compatibility API wx is unavailable');
  if (typeof wx.onError === 'function') wx.onError(reportStartupFailure);
  require(${JSON.stringify(modulePath)});
  console.log(${JSON.stringify("[" + tag + "] module loaded")});
} catch (error) { reportStartupFailure(error); }
`;
}
