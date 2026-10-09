const { app, BrowserWindow } = require("electron");
const path = require("node:path");

const smoke = process.env.SSNOIR_DESKTOP_SMOKE === "1";
if (smoke) {
  app.commandLine.appendSwitch("enable-webgl");
  app.commandLine.appendSwitch("enable-unsafe-swiftshader");
  app.commandLine.appendSwitch("use-angle", "swiftshader");
}

function createWindow() {
  const win = new BrowserWindow({
    width: 1280,
    height: 760,
    backgroundColor: "#111217",
    autoHideMenuBar: true,
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true
    }
  });
  win.loadFile(path.join(__dirname, "../dist/web/index.html"));

  if (smoke) {
    const timeout = setTimeout(() => {
      console.error("[desktop-smoke] Timed out waiting for Scheme and WebGL");
      app.exit(1);
    }, 25000);
    win.webContents.on("did-fail-load", (_e, code, message) => {
      clearTimeout(timeout);
      console.error("[desktop-smoke] Page load failed:", code, message);
      app.exit(1);
    });
    win.webContents.once("did-finish-load", async () => {
      for (let attempt = 0; attempt < 70; attempt++) {
        try {
          const value = await win.webContents.executeJavaScript(
            "window.__SSNOIR_FOUNDATION__?.getSchemeValue()"
          );
          if (value === 1) {
            clearTimeout(timeout);
            console.log("[desktop-smoke] PASS: same browser bundle rendered and Scheme returned 1");
            app.exit(0);
            return;
          }
        } catch (_error) {
          // Initialization may not yet have completed; bounded by deadline.
        }
        await new Promise(resolve => setTimeout(resolve, 300));
      }
    });
  }
}
app.whenReady().then(() => {
  createWindow();
  app.on("activate", () => {
    if (!smoke && BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});
app.on("window-all-closed", () => {
  if (process.platform !== "darwin") app.quit();
});
