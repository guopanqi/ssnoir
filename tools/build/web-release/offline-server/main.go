package main

import (
	"flag"
	"fmt"
	"log"
	"mime"
	"net"
	"net/http"
	"net/url"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"
)

func main() {
	directory := flag.String("directory", executableDirectory(), "WebGL 包目录")
	port := flag.Int("port", 0, "本地 HTTP 端口；0 表示自动选择空闲端口")
	openBrowser := flag.Bool("open", false, "启动后打开默认浏览器")
	flag.Parse()

	root, err := filepath.Abs(*directory)
	if err != nil {
		log.Fatal(err)
	}
	if _, err := os.Stat(filepath.Join(root, "index.html")); err != nil {
		log.Fatalf("找不到 WebGL 入口文件: %s", filepath.Join(root, "index.html"))
	}

	address := fmt.Sprintf("127.0.0.1:%d", *port)
	server := &http.Server{Handler: webGLHandler(root)}
	listener, err := net.Listen("tcp", address)
	if err != nil {
		log.Fatalf("无法启动本地服务 %s: %v", address, err)
	}
	localURL := fmt.Sprintf("http://%s/", listener.Addr().String())
	if *openBrowser {
		if err := open(localURL); err != nil {
			log.Printf("无法自动打开浏览器，请手动访问 %s: %v", localURL, err)
		}
	}

	fmt.Printf("SSNoir Demo 已启动: %s\n", localURL)
	fmt.Println("保持此窗口打开；按 Ctrl-C 停止服务。")
	log.Fatal(server.Serve(listener))
}

func executableDirectory() string {
	executable, err := os.Executable()
	if err != nil {
		return "."
	}
	return filepath.Dir(executable)
}

func webGLHandler(root string) http.Handler {
	return http.HandlerFunc(func(writer http.ResponseWriter, request *http.Request) {
		if request.Method != http.MethodGet && request.Method != http.MethodHead {
			writer.Header().Set("Allow", "GET, HEAD")
			http.Error(writer, "Method not allowed", http.StatusMethodNotAllowed)
			return
		}

		requestPath, err := url.PathUnescape(request.URL.Path)
		if err != nil {
			http.Error(writer, "Invalid URL path", http.StatusBadRequest)
			return
		}
		if requestPath == "/" {
			requestPath = "/index.html"
		}
		relativePath := filepath.Clean(strings.TrimPrefix(requestPath, "/"))
		if relativePath == "." || strings.HasPrefix(relativePath, ".."+string(filepath.Separator)) {
			http.Error(writer, "Not found", http.StatusNotFound)
			return
		}

		path := filepath.Join(root, relativePath)
		file, err := os.Open(path)
		if err != nil {
			http.NotFound(writer, request)
			return
		}
		defer file.Close()
		info, err := file.Stat()
		if err != nil || info.IsDir() {
			http.NotFound(writer, request)
			return
		}

		setHeaders(writer.Header(), requestPath)
		http.ServeContent(writer, request, info.Name(), info.ModTime(), file)
	})
}

func setHeaders(headers http.Header, requestPath string) {
	contentPath := requestPath
	if strings.HasSuffix(contentPath, ".br") {
		headers.Set("Content-Encoding", "br")
		contentPath = strings.TrimSuffix(contentPath, ".br")
	}
	if strings.HasSuffix(contentPath, ".wasm") {
		headers.Set("Content-Type", "application/wasm")
	} else if contentType := mime.TypeByExtension(filepath.Ext(contentPath)); contentType != "" {
		headers.Set("Content-Type", contentType)
	}
	if contentPath == "/index.html" {
		headers.Set("Cache-Control", "no-store, no-cache, must-revalidate")
	} else {
		headers.Set("Cache-Control", "public, max-age=31536000, immutable")
	}
}

func open(target string) error {
	var command *exec.Cmd
	switch runtime.GOOS {
	case "darwin":
		command = exec.Command("open", target)
	case "windows":
		command = exec.Command("rundll32", "url.dll,FileProtocolHandler", target)
	default:
		command = exec.Command("xdg-open", target)
	}
	return command.Start()
}
