#nullable enable
using Cinemachine;
using UnityEditor;
using UnityEngine;

namespace SSNoir.Editor
{
    /// <summary>
    /// Scene 视图和虚拟相机之间的双向对位。
    ///
    /// 不用 <c>GameObject/Align View to Selected</c>：那条菜单命令是"把视图转过去看着这个
    /// 物体"，它自己会保留一个取景距离，从来不是把 Scene 相机搬到目标机位上——方向看着对，
    /// 距离永远差一截。这里直接摆 SceneView 的 pivot 和 rotation，让 Scene 相机真的落在
    /// 虚拟相机的位置上。
    ///
    /// 光摆位置还不够，镜头也得一起搬。Scene 视图默认是 60° 透视，虚拟相机是自己那套 FOV，
    /// 有时候还是正交；两边镜头不一样，就算机位分毫不差，取景范围也对不上，看上去就是
    /// "远近不对"。所以对位永远是<b>先同步镜头、再摆机位</b>——Scene 相机的取景距离是从
    /// size 和 FOV 反推的，镜头没定下来之前读到的距离不作数。
    ///
    /// 剩下一处对不齐是没法消掉的：Scene 视图窗口是什么宽高比就按什么宽高比取景，而成片锁死
    /// 16:9。纵向范围两边一致，横向不一定。定构图以预览面板为准，那张才是 16:9。
    /// </summary>
    public static class SceneViewCameraSync
    {
        // 警告只显示到 0.1°；比较也用同一精度，避免 26.96° / 27.04° 都显示 27.0°
        // 却仍然报「不一致」的假警报。
        private const float LensMismatchTolerance = 0.1f;

        /// <summary>把 Scene 视图搬到虚拟相机的机位和镜头上（预览面板的 <c>get</c>）。</summary>
        public static void PullFromVirtualCamera(CinemachineVirtualCamera vcam)
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null)
            {
                Debug.LogWarning("[SSNoir] 没有打开的 Scene 视图，无法对位。");
                return;
            }

            bool orthographic = vcam.m_Lens.Orthographic;
            view.orthographic = orthographic;

            if (orthographic)
            {
                view.size = vcam.m_Lens.OrthographicSize;
            }
            else
            {
                view.cameraSettings.fieldOfView = vcam.m_Lens.FieldOfView;
            }

            // 镜头定完才能问距离。SceneView 是绕着 pivot 转的，相机自己落在 pivot 后面
            // cameraDistance 处，所以把 pivot 放到机位正前方那个距离上，相机就正好站在机位上。
            float distance = view.cameraDistance;
            Vector3 pivot = vcam.transform.position + vcam.transform.forward * distance;

            // instant，不要过渡动画——对位是个瞬时操作，飞过去反而看不清落点。
            view.LookAt(pivot, vcam.transform.rotation, view.size, orthographic, instant: true);
            view.Repaint();
        }

        /// <summary>把当前 Scene 视图的机位写进虚拟相机（预览面板的 <c>set!</c>）。</summary>
        public static void PushToVirtualCamera(CinemachineVirtualCamera vcam)
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null)
            {
                Debug.LogWarning("[SSNoir] 没有打开的 Scene 视图，无法对位。");
                return;
            }

            // 镜头不一致时只报一声，不顺手改 vcam 的 lens：FOV 往往是整套镜头语言里定好的，
            // 不该因为 Scene 视图恰好是 60° 就被悄悄改掉。要改就去 Inspector 里明着改。
            WarnOnLensMismatch(view, vcam);

            var camera = view.camera;
            Undo.RecordObject(vcam.transform, "Align Virtual Camera With Scene View");
            vcam.transform.SetPositionAndRotation(
                camera.transform.position, camera.transform.rotation);
            EditorUtility.SetDirty(vcam.gameObject);
        }

        private static void WarnOnLensMismatch(SceneView view, CinemachineVirtualCamera vcam)
        {
            bool orthographic = vcam.m_Lens.Orthographic;

            if (view.orthographic != orthographic)
            {
                Debug.LogWarning(
                    $"[SSNoir] Scene 视图是{(view.orthographic ? "正交" : "透视")}，" +
                    $"而 '{vcam.name}' 是{(orthographic ? "正交" : "透视")}——机位写进去了，" +
                    "但取景对不上。先点 get 把镜头校准，再找构图。", vcam);
                return;
            }

            if (!orthographic
                && Mathf.Abs(view.cameraSettings.fieldOfView - vcam.m_Lens.FieldOfView) > LensMismatchTolerance)
            {
                Debug.LogWarning(
                    $"[SSNoir] Scene 视图 FOV 是 {view.cameraSettings.fieldOfView:F1}°，" +
                    $"而 '{vcam.name}' 是 {vcam.m_Lens.FieldOfView:F1}°——机位写进去了，" +
                    "但取景范围会不一样。先点 get 把镜头校准，再找构图。", vcam);
            }
        }
    }
}
