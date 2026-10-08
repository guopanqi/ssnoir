#nullable enable
using System;
using System.IO;
using System.Linq;
using SSNoir.Core;
using SSNoir.Scripting;
using SSNoir.Theatre;
using SSNoir.UnityTheatre;
using UnityEditor;
using UnityEngine;

namespace SSNoir.EditorTools
{
    // Offscreen GPU check. Does not load Main, enter Play Mode or alter scene objects.
    public static class TheatreRenderCheck
    {
        public static void Run()
        {
            try
            {
                var state = new GameState(); var interpreter = new SchemeInterpreter(state, new UnityScriptLoader());
                TheatreScene? scene = null; state.DialogueCenter.OnTheatreRequested += s => scene = s;
                interpreter.LoadFile("scripts/theatre/路灯下.scm"); interpreter.Eval("(路灯下-试演!)");
                if (scene == null) throw new Exception("sample did not create theatre");
                foreach (var beat in scene.Beats)
                    foreach (var sound in beat.Commands.Where(c => c.Kind == TheatreCommandKind.Sound))
                        if (Resources.Load<AudioClip>(sound.Asset) == null) throw new Exception("missing sound: " + sound.Asset);
                var session = new TheatreSession(scene); session.Start();
                using var surface = new TheatreSurface(scene);
                bool found = false;
                for (int i = 0; i < scene.Beats.Count * 3; i++)
                {
                    var beat = scene.Beats[session.BeatIndex];
                    if (session.Objects["尼尔"][TheatreProperty.Opacity] > .99f && beat.Commands.Any(c => c.Target == "路灯" && c.Keys.Length > 2))
                    { found = true; break; }
                    if (session.Line != null && session.Line.Seconds == 0f) { session.Advance(); session.Advance(); }
                    else session.Tick(beat.Duration - session.BeatTime);
                    if (session.IsComplete) break;
                }
                if (!found) throw new Exception("could not reach lamp flicker with actors on stage");
                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.cache/theatre-check")); Directory.CreateDirectory(output);
                session.Tick(.07f);
                surface.Render(session, 1600, 900); var dim = Read(surface.Texture);
                File.WriteAllBytes(Path.Combine(output, "lamp-dim.png"), dim.EncodeToPNG());
                session.Tick(.06f);
                surface.Render(session, 1600, 900); var bright = Read(surface.Texture);
                File.WriteAllBytes(Path.Combine(output, "lamp-bright.png"), bright.EncodeToPNG());
                // Lamp, ground pool and actor region must respond to the same curve.
                CheckRegion(bright, dim, 780, 610, 40, 40, "lamp glow");
                CheckRegion(bright, dim, 650, 100, 300, 40, "ground pool");
                // Check the image shader in isolation so a brightening backdrop cannot create a false pass.
                var isolated = TheatreParser.Parse(interpreter.Eval(
                    "(theatre-scene 1600 900 \"#000000\" (list " +
                    "(theatre-light \"l\" \"\" \"#F3D08A\" 800 550 620 0.15) " +
                    "(theatre-image \"a\" \"\" \"Portraits/Neon/尼尔_抱臂\" 540 790 450 550 \"l\")))"),
                    interpreter.Eval("(list (theatre-tween \"l\" 'brightness 1 0.1))"));
                var isolatedSession = new TheatreSession(isolated); isolatedSession.Start();
                using (var isolatedSurface = new TheatreSurface(isolated))
                {
                    isolatedSurface.Render(isolatedSession, 1600, 900); var actorDim = Read(isolatedSurface.Texture);
                    isolatedSession.Tick(.1f);
                    isolatedSurface.Render(isolatedSession, 1600, 900); var actorBright = Read(isolatedSurface.Texture);
                    CheckRegion(actorBright, actorDim, 340, 170, 400, 420, "isolated actor lighting");
                    UnityEngine.Object.DestroyImmediate(actorDim); UnityEngine.Object.DestroyImmediate(actorBright);
                }
                var background = bright.GetPixel(10, 890);
                if (background.a > .05f) throw new Exception("canvas must stay transparent where nothing is drawn");
                var shader = Resources.Load<Shader>("Theatre/LineTheatre");
                if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new Exception("theatre shader compilation failed");
                foreach (var size in new[] { new Vector2Int(1200, 900), new Vector2Int(2000, 900) })
                {
                    surface.Render(session, size.x, size.y); var frame = Read(surface.Texture);
                    File.WriteAllBytes(Path.Combine(output, "aspect-" + size.x + ".png"), frame.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(frame);
                }
                UnityEngine.Object.DestroyImmediate(dim); UnityEngine.Object.DestroyImmediate(bright);
                Debug.Log("[TheatreRenderCheck] PASS: Scheme import, image/audio loading, GPU draw, lamp/pool/actor response, 4:3 and 20:9 surfaces. " + output);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }
        private static Texture2D Read(RenderTexture target)
        {
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                return image;
            }
            finally { RenderTexture.active = previous; }
        }
        private static void CheckRegion(Texture2D bright, Texture2D dim, int x, int y, int width, int height, string label)
        {
            float difference = 0f;
            for (int row = y; row < y + height; row += 4)
                for (int col = x; col < x + width; col += 4)
                    difference += bright.GetPixel(col, row).grayscale - dim.GetPixel(col, row).grayscale;
            if (difference < 0.1f) throw new Exception(label + " did not respond to lamp intensity: " + difference);
            Debug.Log("[TheatreRenderCheck] " + label + " luminance delta: " + difference);
        }
    }
}
