#nullable enable
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SSNoir
{
    /// <summary>
    /// 过场首帧截图：把当前机位单独渲一张 16:9 的 PNG 存到磁盘，画面里没有任何 UI。
    ///
    /// 拿去喂图生视频模型的首帧必须和引擎画面像素级同源，否则生成的视频接回实时渲染时会跳。
    /// 所以这里不抓屏，理由和 <see cref="ViewCrossfade"/> 那边一样——后台缓冲里是已经编码成
    /// 显示值的图，线性工程里再走一遍输出就整张发白；抓屏还会把 UI 一起抓进去、在某些图形 API
    /// 上下颠倒。改成另开一台相机照着当前机位渲进 RenderTexture，颜色空间、朝向、后处理全按
    /// 正常管线走，UI 压根不在这条路径上，于是截图时界面开着也无所谓，不用先把它藏起来。
    ///
    /// 画幅锁死 16:9：纵向视野和屏幕上看到的一致，横向按 16:9 重算。窗口本身不是 16:9 时，
    /// 截出来的左右范围会和屏幕差一点——那是画幅换算的必然结果，不是取景取错了。
    ///
    /// 分辨率直接原生渲染，不做超采样后再缩。缩图会把描边的线宽一起缩掉，而线宽正是这套
    /// 白描风格最吃紧的东西，宁可让下游平台自己去缩。
    /// </summary>
    public static class CinematicCapture
    {
        private const string CaptureCameraName = "SSNoir.CinematicCapture.Camera";

        /// <summary>一次截图跨一帧完成，跑的过程中再按不重入。</summary>
        public static bool IsCapturing { get; private set; }

        /// <summary>
        /// 按当前机位截一张 16:9 的 PNG。<paramref name="height"/> 是纵向像素，宽度由画幅算出。
        /// </summary>
        public static void Capture(MonoBehaviour runner, int height = 1080)
        {
            if (IsCapturing)
                return;

            // Cinemachine 的 brain 挂在 Camera.main 上，它才是真正被渲染的那台；虚拟相机只是
            // 在给它喂机位，照虚拟相机截会截到还没被 brain 混合过的位置。
            var source = Camera.main;
            if (source == null)
            {
                Debug.LogWarning("[SSNoir] 过场截图失败：场景里没有主相机。");
                return;
            }

            runner.StartCoroutine(CaptureRoutine(source, height));
        }

        /// <summary>
        /// 开一张 16:9 的渲染目标。<paramref name="height"/> 是纵向像素，宽度由画幅算出。
        /// </summary>
        public static RenderTexture CreateTarget(int height)
        {
            int h = Mathf.Max(180, height);
            int w = h * 16 / 9;

            // 深度位必须给够，接这张图的相机要正经渲一遍世界，不是拷贝一张现成的图。
            return new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
            {
                name = "SSNoir.CinematicCapture",
                antiAliasing = Mathf.Max(1, (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset)?.msaaSampleCount ?? QualitySettings.antiAliasing),
            };
        }

        /// <summary>
        /// 把 <paramref name="source"/> 的渲染设置搬到截图相机上：剔除、清除方式、以及 URP
        /// 那份附加数据。<b>不含机位和镜头</b>——那两样由调用方决定（运行时照搬主相机，编辑器
        /// 下照搬虚拟相机），所以这里只管"画得一不一样"，不管"从哪儿看"。
        /// </summary>
        public static void ApplyRenderSettings(Camera capture, Camera source)
        {
            // CopyFrom 带走镜头、剔除、清除方式这些，但不带 URP 那份附加数据——后处理没跟
            // 过来的话，截图会和实时画面亮度对不上。
            capture.CopyFrom(source);

            var sourceData = source.GetUniversalAdditionalCameraData();
            var captureData = capture.GetUniversalAdditionalCameraData();
            captureData.renderPostProcessing = sourceData.renderPostProcessing;
            captureData.antialiasing = sourceData.antialiasing;
            captureData.antialiasingQuality = sourceData.antialiasingQuality;
            captureData.volumeLayerMask = sourceData.volumeLayerMask;
            captureData.renderShadows = sourceData.renderShadows;
        }

        private static IEnumerator CaptureRoutine(Camera source, int height)
        {
            IsCapturing = true;

            var target = CreateTarget(height);

            var go = new GameObject(CaptureCameraName) { hideFlags = HideFlags.DontSave };
            var capture = go.AddComponent<Camera>();
            capture.enabled = false;

            ApplyRenderSettings(capture, source);
            capture.transform.SetPositionAndRotation(
                source.transform.position, source.transform.rotation);

            capture.targetTexture = target;
            // aspect 要在 targetTexture 之后设：接上贴图会把画幅重置成贴图的比例，而这里要的是
            // 「纵向视野照搬屏幕、横向按 16:9 重算」，所以得自己再按回来。
            capture.aspect = 16f / 9f;

            // 登记这台相机，让统一涂装跳过它——首帧图要带后处理但不带抖动网纹，理由见
            // SSNoirStylizeMaterial.CaptureCamera。按相机实例认而不是按帧开关，因为这一帧
            // 主相机也在渲，玩家看着的画面不能跟着一起被摘掉涂装。
            Rendering.SSNoirStylizeMaterial.CaptureCamera = capture;
            capture.enabled = true;

            // 相机在帧末才渲，读像素得等它渲完。
            yield return new WaitForEndOfFrame();

            capture.enabled = false;
            Rendering.SSNoirStylizeMaterial.CaptureCamera = null;
            capture.targetTexture = null;

            SaveTarget(target, "play");

            UnityEngine.Object.Destroy(go);
            target.Release();
            UnityEngine.Object.Destroy(target);

            IsCapturing = false;
        }

        /// <summary>
        /// 把渲染目标读回来存成 PNG，返回落盘路径；失败时返回 null 并已经报过错。
        /// 只读不销毁，<paramref name="target"/> 归调用方处理。
        /// </summary>
        public static string? SaveTarget(RenderTexture target, string label)
        {
            int w = target.width;
            int h = target.height;

            var previousActive = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(w, h, TextureFormat.RGB24, mipChain: false);
            image.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            image.Apply();
            RenderTexture.active = previousActive;

            byte[] png = image.EncodeToPNG();

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(image);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(image);
            }

            try
            {
                string dir = OutputDirectory();
                Directory.CreateDirectory(dir);
                string path = Path.Combine(
                    dir, $"ssnoir_{label}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
                File.WriteAllBytes(path, png);
                Debug.Log($"[SSNoir] 过场首帧已保存（{w}×{h}）：{path}");
                return path;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SSNoir] 过场截图写盘失败：{e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 存到用户的下载目录：截图是拿去喂线上模型的，落在顺手能拖进浏览器的地方最省事，
        /// 也顺带绕开了 Assets 目录（写在里面 Unity 会把它当资源导进来，还得连带管 .meta）。
        /// </summary>
        public static string OutputDirectory()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home))
            {
                return Application.persistentDataPath;
            }

            string downloads = Path.Combine(home, "Downloads");
            return Directory.Exists(downloads) ? downloads : home;
        }
    }
}
