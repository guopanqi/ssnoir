#nullable enable
using Cinemachine;
using UnityEditor;
using UnityEngine;

namespace SSNoir.Editor
{
    /// <summary>
    /// 编辑器里直接从一台虚拟相机出一张 16:9 首帧 PNG，不进 Play 模式。
    ///
    /// 找机位这件事的循环是：Scene 视图飞到想要的角度 → 在预览面板点 <c>set!</c> 把机位按进
    /// 虚拟相机 → 点 <c>shot</c> 出图 → 拿去喂模型。一轮不用进 Play、不用等场景初始化，
    /// 这是"找视角"阶段唯一需要快的东西。
    ///
    /// 渲染和写盘那半边在运行时那份 <see cref="CinematicCapture"/> 里，两条路径共用——搬去
    /// 别的工程时它要跟着一起走，除此之外 CameraTools 只吃 Cinemachine 和 URP。
    ///
    /// 渲染设置从主相机搬（剔除、清除方式、URP 后处理），机位和镜头从虚拟相机搬。分开搬是
    /// 因为虚拟相机只描述"从哪儿看、用什么镜头"，"画成什么样"始终在 brain 那台真相机上。
    /// 两边不合并，截出来的图就会和运行时同机位的画面对不上——而这张图正是要拿去和游戏画面
    /// 无缝衔接的，对不上就白截了。
    /// </summary>
    public static class CinematicShotCapture
    {
        public static void CaptureFromVirtualCamera(CinemachineVirtualCamera vcam, int height)
        {
            using var lighting = CityOutlineEditorPreview.BeginLightingPreview(vcam.transform);
            var target = CinematicCapture.CreateTarget(height);
            var go = new GameObject("~SSNoirShotCaptureCamera")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            try
            {
                var capture = go.AddComponent<Camera>();
                capture.enabled = false;

                // 主相机不一定拿得到（场景还没配好、或 tag 掉了）。拿不到就用相机的出厂设置渲，
                // 构图仍然是对的，只是后处理和剔除可能和运行时有出入——这时候报一声，别让
                // 一张悄悄不一致的图混进素材里。
                var source = Camera.main;
                if (source != null)
                {
                    CinematicCapture.ApplyRenderSettings(capture, source);
                }
                else
                {
                    Debug.LogWarning(
                        "[SSNoir] 场景里没有主相机，这张首帧用的是相机默认渲染设置，" +
                        "后处理和剔除可能和运行时对不上。");
                }

                // 编辑器里只要 Gizmos 开关是开的，URP 就会往任何手动渲的相机上加一道 gizmo
                // pass，锚点连线、orbit pivot 那些全会进图。标成 Preview 就不画了——Unity
                // 自己那个相机预览小窗避开 gizmos 用的也是这招。CopyFrom 会把 cameraType 一起
                // 覆盖掉，所以这行必须排在它后面。
                capture.cameraType = CameraType.Preview;

                // 镜头和机位来自虚拟相机，覆盖掉刚从主相机搬过来的那一份。
                capture.transform.SetPositionAndRotation(
                    vcam.transform.position, vcam.transform.rotation);
                capture.fieldOfView = vcam.m_Lens.FieldOfView;
                capture.orthographic = vcam.m_Lens.Orthographic;
                capture.orthographicSize = vcam.m_Lens.OrthographicSize;
                float clipScale = !Application.isPlaying ? (vcam.GetComponent<SSNoirVirtualCameraConfig>()?.modelRoot?.lossyScale.x ?? 1) : 1;
                capture.nearClipPlane = vcam.m_Lens.NearClipPlane * clipScale;
                capture.farClipPlane = vcam.m_Lens.FarClipPlane * clipScale;

                capture.targetTexture = target;
                // aspect 要在 targetTexture 之后设，接上贴图会把画幅重置成贴图的比例。
                capture.aspect = 16f / 9f;

                // 编辑器下没有帧循环可等，直接渲。预览面板一直是这么渲的，这条路走得通。
                //
                // 登记一下，让统一涂装跳过这台相机——首帧图要带后处理但不带抖动网纹，
                // 理由见 SSNoirStylizeMaterial.CaptureCamera。
                SSNoir.Rendering.SSNoirStylizeMaterial.CaptureCamera = capture;
                try
                {
                    capture.Render();
                }
                finally
                {
                    SSNoir.Rendering.SSNoirStylizeMaterial.CaptureCamera = null;
                }

                capture.targetTexture = null;

                CinematicCapture.SaveTarget(target, SanitizeLabel(vcam.name));
            }
            finally
            {
                Object.DestroyImmediate(go);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>虚拟相机名会进文件名，先把路径分隔符之类的挑出去。</summary>
        private static string SanitizeLabel(string name)
        {
            foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(name) ? "shot" : name;
        }
    }
}
