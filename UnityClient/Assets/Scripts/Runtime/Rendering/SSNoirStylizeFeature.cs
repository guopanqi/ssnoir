#nullable enable
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SSNoir.Rendering
{
    /// <summary>
    /// 过场视频那条路（IMGUI）取涂装材质的地方。
    ///
    /// Renderer Feature 是资产、不在场里，拿不到场上的对象，场上的对象也拿不到它。中间需要一个
    /// 交汇点，就是这里。参数只有一份、只由 Feature 写，视频那边只读——两条路必须共用同一份
    /// 参数，各调各的就等于没统一。
    /// </summary>
    public static class SSNoirStylizeMaterial
    {
        /// <summary>Feature 每帧写入；没装 Feature 或被关掉时为 null，调用方要能退回原样绘制。</summary>
        public static Material? Shared { get; internal set; }

        /// <summary>GUI 那一路用的 pass 序号，见 SSNoirStylize.shader。</summary>
        public const int GuiPass = 1;

        /// <summary>
        /// 正在出过场首帧图的那台相机。涂装跳过它。
        ///
        /// <b>首帧图必须不带涂装。</b>这一条和直觉相反，值得写清楚：抖动网纹是<b>屏幕</b>的属性，
        /// 不是画面内容的属性。把它烘进首帧喂给模型，模型就会把网点当成内容去学——而它学不好，
        /// 生成出来的是会爬、会糊、会漂的假网点。等视频播放时，这层烘死的假网点上面又要盖一层
        /// 实时的真网点，两层叠出摩尔纹，比不涂装难看得多。
        ///
        /// 后处理的其余部分（Bloom、tonemapping）则必须留着——那些是内容级的，视频需要它们
        /// 烘进去才能和世界接得上。所以只摘掉涂装这一层，不是整个关掉后处理。
        ///
        /// 按相机实例认，不按 cameraType 认：运行时那条截图路径用的是普通 Game 相机，和主相机
        /// 同帧渲染，靠全局开关会把玩家正看着的那一帧也一起摘掉。
        /// </summary>
        public static Camera? CaptureCamera { get; set; }
    }

    /// <summary>
    /// 把统一涂装插进 URP：<b>后处理之后</b>。
    ///
    /// 位置是有讲究的，不能往前挪。放在后处理之前的话，Bloom 会糊在抖动网纹上，量化出来的
    /// 硬边被辉光抹开，第二味药就废了。而且过场视频的 Bloom 是烘死在像素里的（首帧图是带
    /// 后处理截的，模型照着它生成），世界那边再实时算一次——只有都排在 Bloom 之后，两边
    /// 拿到的才是同一种画面。
    ///
    /// UI 不受影响：IMGUI 在整条管线跑完之后才画，天然在涂装外面。影幕的黑边也一样——那是
    /// 取景框，不该被抖动网纹爬满。
    /// </summary>
    [DisallowMultipleRendererFeature("SSNoir Stylize")]
    public class SSNoirStylizeFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("0 = 原样，1 = 全涂装。调参时拉着它来回比。")]
            [Range(0f, 1f)]
            public float Intensity = 1f;

            [Header("色阶曲线（显示值）")]
            [Tooltip("最暗端。")]
            public Color RampA = new Color(0.035f, 0.045f, 0.065f);

            [Tooltip("暗部主调，取自场景背景色。")]
            public Color RampB = new Color(0.112f, 0.143f, 0.196f);

            [Tooltip("中间调，取自描线的蓝灰。")]
            public Color RampC = new Color(0.350f, 0.420f, 0.550f);

            [Tooltip("最亮端，描线的冷白。")]
            public Color RampD = new Color(0.950f, 0.970f, 1.000f);

            [Range(0.05f, 0.95f)] public float RampBPos = 0.30f;
            [Range(0.05f, 0.95f)] public float RampCPos = 0.65f;

            [Header("输入色阶")]
            [Tooltip("输入黑点。")]
            [Range(0f, 0.3f)]
            public float InBlack = 0.02f;

            [Tooltip("输入白点。压得比两个来源的亮度上限都低，是让视频和 3D 亮端对齐的关键；"
                   + "也把档位从画面用不到的亮部收回来。往上调 = 整体变暗。")]
            [Range(0.1f, 1f)]
            public float InWhite = 0.45f;

            [Tooltip("归一化之后的曲线。小于 1 提亮暗部；这套画面九成是暗部，动它很敏感。")]
            [Range(0.3f, 2f)]
            public float LumaGamma = 1f;

            [Header("量化与抖动")]
            [Tooltip("色阶档数。2 就是奥伯拉丁那种 1-bit，6~8 保得住夜景层次。")]
            [Range(2, 16)]
            public int Levels = 6;

            [Tooltip("一个抖动格子的大小，以 720p 为基准的像素数（对齐视频的原生分辨率）。"
                   + "实际像素按当前分辨率等比放大，所以换窗口、换设备，网点看上去一样粗。\n"
                   + "1 = 一个视频像素一格，最细；2~3 是明显的网点质感。")]
            [Range(1, 6)]
            public int DitherScale = 2;

            [Tooltip("0 = 硬色带（先关掉抖动看分档对不对），1 = 全抖动。")]
            [Range(0f, 1f)]
            public float DitherStrength = 1f;

            [Header("过场视频·传递曲线")]
            [Tooltip("视频读进来时的提亮指数。1.0 = 原样，2.2 ≈ 按 sRGB 转一次。\n"
                   + "2.2 时暗部能对上世界，但线条边缘的中间调会被抬过 In White、吸成纯白，"
                   + "白线看着胖一圈——往下调到白线粗细和世界一致为止。\n"
                   + "pow 保端点，拨它不动纯黑和纯白，只动中间调。")]
            [Range(1f, 2.4f)]
            public float VideoInputGamma = 2.2f;

            [Tooltip("视频写回屏幕时的指数，抵消 IMGUI 目标那头的编码。\n"
                   + "1.0 = 原样写出（整体偏亮），2.2 ≈ 转回线性（整体偏黑）。先把这个定下来，"
                   + "再调上面那个。")]
            [Range(1f, 2.4f)]
            public float VideoOutputGamma = 2.2f;

            [Header("范围")]
            [Tooltip("也涂 Scene 视图。找机位时通常关掉更好使。")]
            public bool AffectSceneView = false;
        }

        [SerializeField]
        [Tooltip("留空则按名字找 SSNoir/Stylize。打包时最好在这里显式指上，否则要自己进 "
               + "Always Included Shaders。")]
        private Shader? _shader;

        public Settings settings = new Settings();

        private Material? _material;
        private StylizePass? _pass;

        public override void Create()
        {
            Shader? shader = _shader != null ? _shader : Shader.Find("SSNoir/Stylize");
            if (shader == null)
            {
                Debug.LogError("[SSNoirStylize] 找不到 SSNoir/Stylize，涂装不生效。");
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new StylizePass(_material)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing,
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || _material == null)
                return;

            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;
            if (cameraType == CameraType.SceneView && !settings.AffectSceneView)
                return;

            // 出首帧图的相机不涂装，理由见 SSNoirStylizeMaterial.CaptureCamera。
            if (SSNoirStylizeMaterial.CaptureCamera != null
                && ReferenceEquals(renderingData.cameraData.camera, SSNoirStylizeMaterial.CaptureCamera))
                return;

            // 每帧灌参数，所以 Play 模式下改 Feature 的滑杆是立刻生效的——这一版就是拿来调的。
            ApplySettings(_material);

            SSNoirStylizeMaterial.Shared = _material;

            renderer.EnqueuePass(_pass);
        }

        private void ApplySettings(Material material)
        {
            material.SetColor("_RampA", settings.RampA);
            material.SetColor("_RampB", settings.RampB);
            material.SetColor("_RampC", settings.RampC);
            material.SetColor("_RampD", settings.RampD);

            // 两个中间锚点不能越位，越位了 ramp 的分段插值会算出负数区间。
            float bPos = Mathf.Clamp(settings.RampBPos, 0.05f, 0.9f);
            float cPos = Mathf.Clamp(settings.RampCPos, bPos + 0.05f, 0.95f);
            material.SetFloat("_RampBPos", bPos);
            material.SetFloat("_RampCPos", cPos);

            // 白点不能低到贴上黑点，否则归一化会炸成除零后的满屏纯白。
            material.SetFloat("_InBlack", settings.InBlack);
            material.SetFloat("_InWhite", Mathf.Max(settings.InWhite, settings.InBlack + 0.02f));
            material.SetFloat("_LumaGamma", settings.LumaGamma);

            material.SetFloat("_GuiInputGamma", settings.VideoInputGamma);
            material.SetFloat("_GuiOutputGamma", settings.VideoOutputGamma);

            material.SetFloat("_Levels", settings.Levels);
            material.SetFloat("_DitherScale", settings.DitherScale);
            material.SetFloat("_DitherStrength", settings.DitherStrength);
            material.SetFloat("_Intensity", settings.Intensity);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            _pass = null;

            if (ReferenceEquals(SSNoirStylizeMaterial.Shared, _material))
                SSNoirStylizeMaterial.Shared = null;

            CoreUtils.Destroy(_material);
            _material = null;
        }

        /// <summary>
        /// 全屏涂装一趟。写法照抄 URP 自带的 FullScreenPassRendererFeature——这不是风格问题，
        /// 是这几条各自都会让整个 pass 悄无声息地不生效：
        ///
        /// 1. <b>相机颜色句柄必须在 Execute 里现取</b>，不能在 SetupRenderPasses 里缓存。后处理
        ///    跑完之后当前颜色已经换了一张贴图，缓存下来的那个句柄指的是后处理之前那张——涂装
        ///    照样算，只是算完写进了一张马上被丢掉的贴图里。画面毫无变化、也不报错。
        /// 2. <b>OnCameraSetup 里要 ResetTarget()</b>，告诉 renderer 这个 pass 自己管渲染目标。
        /// 3. <b>主绘制走 DrawProcedural 三顶点</b>（配合 Blit.hlsl 的 Vert），不是 Blitter 来回搬。
        ///    源贴图和 _BlitScaleBias 通过 MaterialPropertyBlock 喂进去。
        /// </summary>
        private class StylizePass : ScriptableRenderPass
        {
            private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
            private static readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();

            private readonly Material _material;
            private RTHandle? _copy;

            public StylizePass(Material material)
            {
                _material = material;
                profilingSampler = new ProfilingSampler("SSNoir Stylize");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                ResetTarget();

                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = (int)DepthBits.None;
                desc.msaaSamples = 1;

                RenderingUtils.ReAllocateIfNeeded(ref _copy, desc, name: "_SSNoirStylizeCopy");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_copy == null)
                    return;

                var renderer = renderingData.cameraData.renderer;

                // renderingData.commandBuffer 在 14.0.12 里是 internal，自己取一条。
                var cmd = CommandBufferPool.Get();

                using (new ProfilingScope(cmd, profilingSampler))
                {
                    // 不能原地读写同一张贴图，先原样拷一份出来当输入。
                    CoreUtils.SetRenderTarget(cmd, _copy);
                    Blitter.BlitTexture(cmd, renderer.cameraColorTargetHandle,
                        new Vector4(1f, 1f, 0f, 0f), 0f, false);

                    CoreUtils.SetRenderTarget(cmd, renderer.cameraColorTargetHandle);

                    Properties.Clear();
                    Properties.SetTexture(BlitTextureId, _copy);
                    Properties.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                    cmd.DrawProcedural(
                        Matrix4x4.identity, _material, 0, MeshTopology.Triangles, 3, 1, Properties);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                _copy?.Release();
                _copy = null;
            }
        }
    }
}
