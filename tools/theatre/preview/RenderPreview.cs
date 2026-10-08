using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using SSNoir.Theatre;
using SSNoir.UnityTheatre;
using UnityEditor;
using UnityEngine;

public static class RenderPreview
{
    [Serializable] public sealed class Request
    {
        public string ScenePath = "", Output = "";
        public int Width, Height, Fps;
        public float MaxSeconds, ManualWait;
        public bool Linear;
    }
    public static void Run()
    {
        var request = JsonConvert.DeserializeObject<Request>(File.ReadAllText(Path.Combine(Application.dataPath, "../request.json")));
        try
        {
            PlayerSettings.colorSpace = request.Linear ? ColorSpace.Linear : ColorSpace.Gamma;
            var scene = JsonConvert.DeserializeObject<TheatreScene>(File.ReadAllText(request.ScenePath));
            var settings = TheatreSettings.Active();
            foreach (var cue in scene.Commands)
                if (cue.Kind == TheatreCommandKind.Sound && Resources.Load<AudioClip>(cue.Asset) == null)
                    throw new Exception("missing sound: " + cue.Asset);
            var session = new TheatreSession(scene, settings.EnterSeconds, settings.ExitSeconds);
            session.Start();
            using var surface = new TheatreSurface(scene);
            var shader = Resources.Load<Shader>("Preview/Composite");
            if (ShaderUtil.ShaderHasError(shader) || ShaderUtil.ShaderHasError(Resources.Load<Shader>("Theatre/LineTheatre")))
                throw new Exception("preview shader compilation failed");
            var composite = new Material(shader);
            var background = new Color(scene.Background.R, scene.Background.G, scene.Background.B);
            composite.SetColor("_PreviewBackground", request.Linear ? background.linear : background);
            var target = new RenderTexture(request.Width, request.Height, 0, RenderTextureFormat.ARGB32);
            target.Create();
            var frames = new List<object>();
            float nextFrame = 0;
            while (!session.IsComplete)
            {
                if (session.Time >= request.MaxSeconds) throw new Exception("演出超过 --max-seconds，未截断输出，请检查脚本或提高上限");
                if (session.Line != null)
                {
                    if (request.ManualWait <= 0) throw new Exception("演出等待点击；请显式指定 --manual-wait 秒数以模拟玩家推进");
                    if (session.CaptionTime >= request.ManualWait)
                    {
                        var line = session.Line;
                        session.Advance();
                        if (session.Line == line) session.Advance();
                    }
                }
                if (session.IsComplete) break;
                if (session.Time + .0001f >= nextFrame)
                {
                    surface.Render(session, request.Width, request.Height, settings.ContentScale, settings.FocusStrength, settings.Dither / 255);
                    composite.SetFloat("_PreviewFade", session.Fade);
                    Graphics.Blit(surface.Texture, target, composite);
                    var previous = RenderTexture.active;
                    RenderTexture.active = target;
                    var image = new Texture2D(request.Width, request.Height, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, request.Width, request.Height), 0, 0); image.Apply();
                    RenderTexture.active = previous;
                    string file = "frames/" + frames.Count.ToString("D5") + ".png";
                    File.WriteAllBytes(Path.Combine(request.Output, file), image.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(image);
                    frames.Add(new { file, time = session.Time, phase = session.Phase.ToString(), speaker = session.Caption?.Target, caption = session.Caption?.Text });
                    nextFrame += 1f / request.Fps;
                }
                session.Tick(1f / 60);
            }
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(composite);
            File.WriteAllText(Path.Combine(request.Output, "manifest.json"), JsonConvert.SerializeObject(new
            { width = request.Width, height = request.Height, fps = request.Fps, seconds = session.Time, manual_wait_seconds = request.ManualWait, frames }, Formatting.Indented));
            File.WriteAllText(Path.Combine(request.Output, "result.json"), "{\"success\":true}");
            Debug.Log("[TheatrePreview] PASS " + frames.Count + " frames: " + request.Output);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            File.WriteAllText(Path.Combine(request.Output, "result.json"), JsonConvert.SerializeObject(new { success = false, error = error.ToString() }));
            EditorApplication.Exit(1);
        }
    }
}
