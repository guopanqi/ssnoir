// Keep this entry independent of renderer imports so early failures are visible.
function reportStartupFailure(error) {
  // JavaScriptCore stacks may contain only frames, without name/message.
  var reason = error && (error.message || error.errMsg) || String(error);
  var message = String(error && error.name || 'Error') + ': ' + String(reason);
  if (error && error.stack) message += '\n' + String(error.stack);
  console.error('[SSNoir TapTap startup]', message);
  if (typeof wx !== 'undefined' && typeof wx.showModal === 'function') {
    wx.showModal({
      title: 'SSNoir 启动失败',
      content: message.slice(0, 650),
      showCancel: false
    });
  }
}
console.log('[SSNoir TapTap startup] entry reached');
try {
  if (typeof wx === 'undefined') throw new Error('TapTap WeChat compatibility API wx is unavailable');
  if (typeof wx.onError === 'function') wx.onError(reportStartupFailure);
  require('./foundation.js');
  console.log('[SSNoir TapTap startup] foundation loaded');
} catch (error) {
  reportStartupFailure(error);
}
