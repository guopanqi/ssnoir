#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using SSNoir.Theatre;

namespace SSNoir.Scripting
{
    // The only Scheme -> theatre boundary. Positional wire format stays private to scripts/theatre.scm.
    public static class TheatreParser
    {
        public static TheatreScene Parse(object rawScene, object rawProgram)
        {
            var scene = List(rawScene, 4, "scene");
            float width = Positive(scene[0]), height = Positive(scene[1]);
            var nodes = new List<TheatreNode>();
            var byId = new Dictionary<string, TheatreNode>(StringComparer.Ordinal);
            foreach (var raw in List(scene[3]))
            {
                var n = List(raw, 8, "object");
                string id = Id(n[0]), parent = Text(n[1]);
                var shape = Shape(Id(n[2]));
                if (shape == TheatreShape.Focus && parent != "") Fail("focus is a root composite node");
                if (byId.ContainsKey(id)) Fail("duplicate object: " + id);
                if (parent != "" && (!byId.TryGetValue(parent, out var p) || (p.Shape != TheatreShape.Group && p.Shape != TheatreShape.Light)))
                    Fail("parent must be an earlier group/light: " + parent);
                var geometry = List(n[3]);
                float w = 0f, h = 0f;
                TheatrePoint[] points = Array.Empty<TheatrePoint>();
                if (shape == TheatreShape.Line)
                {
                    if (geometry.Count != 2) Fail("line requires width and points");
                    w = Positive(geometry[0]); points = Points(geometry[1], 2);
                    for (int i = 1; i < points.Length; i++)
                        if (DistanceSquared(points[i], points[i - 1]) < 0.0001f) Fail("line contains zero-length segment: " + id);
                }
                else if (shape == TheatreShape.Polygon) { points = Points(n[3], 3); ValidateConvex(points, id); }
                else if (shape == TheatreShape.Glow || shape == TheatreShape.Image || shape == TheatreShape.Focus)
                {
                    if (geometry.Count != 2) Fail("glow/image requires width and height");
                    w = Positive(geometry[0]); h = Positive(geometry[1]);
                    if (shape == TheatreShape.Focus && w >= h) Fail("focus radii must satisfy 0 < inner < outer in scene units");
                }
                else if (shape == TheatreShape.Light)
                {
                    if (geometry.Count != 1) Fail("light requires radius");
                    w = Positive(geometry[0]);
                }
                else if (geometry.Count != 0) Fail("group cannot have geometry");
                string asset = Text(n[5]), light = Text(n[6]);
                if (shape == TheatreShape.Image) Asset(asset);
                else if (asset != "") Fail("only images have an asset");
                if (shape != TheatreShape.Image && light != "") Fail("only images bind a light");
                var initial = new Dictionary<TheatreProperty, float>();
                foreach (var rawProperty in List(n[7]))
                {
                    var pair = List(rawProperty, 2, "property");
                    var property = Property(Id(pair[0]));
                    var value = Value(property, pair[1]);
                    if (shape == TheatreShape.Focus && property != TheatreProperty.X && property != TheatreProperty.Y && property != TheatreProperty.Opacity) Fail("focus only supports x/y/opacity");
                    if (property == TheatreProperty.Reveal && shape != TheatreShape.Line) Fail("initial reveal only applies to lines: " + id);
                    if (initial.ContainsKey(property)) Fail("duplicate initial property: " + id);
                    initial.Add(property, value);
                }
                var node = new TheatreNode { Id = id, Parent = parent, Shape = shape, Width = w, Height = h,
                    Points = points, Color = Color(Text(n[4])), Asset = asset, Light = light, Initial = initial };
                nodes.Add(node); byId.Add(id, node);
            }
            if (nodes.Count == 0) Fail("scene must contain objects");
            foreach (var node in nodes)
                if (node.Light != "" && (!byId.TryGetValue(node.Light, out var light) || light.Shape != TheatreShape.Light))
                    Fail("image light must reference a light object: " + node.Id);

            int cueCount = 0;
            TheatreCommand ParseCue(object raw, bool background, int depth)
            {
                if (depth > 32 || ++cueCount > 10000) Fail("timeline exceeds nesting or size limit");
                var c = List(raw); if (c.Count == 0) Fail("empty cue");
                string op = Id(c[0]);
                if (op == "sequence" || op == "parallel" || op == "during" || op == "repeat" || op == "loop")
                {
                    int first = op == "repeat" ? 2 : 1;
                    if (c.Count <= first) Fail("empty time structure: " + op);
                    int count = 1;
                    if (op == "repeat") { float n = Positive(c[1]); if (n != (int)n || n > 10000) Fail("repeat requires an integer 1..10000"); count = (int)n; Count(c, 3, op); }
                    if (op == "loop") { if (!background) Fail("unbounded loop must be inside a during background"); count = 0; Count(c, 2, op); }
                    if (op == "during" && c.Count < 3) Fail("during needs a main action and background actions");
                    var children = new List<TheatreCommand>();
                    for (int i = first; i < c.Count; i++) children.Add(ParseCue(c[i], background || (op == "during" && i > 1), depth + 1));
                    if (op == "repeat" || op == "loop")
                        if (MinimumDuration(children[0]) <= 0) Fail("repeat body must consume positive time and cannot wait for input");
                    if (op == "parallel" || op == "during")
                    {
                        var combined = new HashSet<string>(StringComparer.Ordinal);
                        foreach (var child in children)
                            foreach (var write in Writes(child)) if (!combined.Add(write)) Fail("concurrent property conflict: " + write);
                    }
                    return new TheatreCommand { Kind = op == "sequence" ? TheatreCommandKind.Sequence : op == "parallel" ? TheatreCommandKind.Parallel
                        : op == "during" ? TheatreCommandKind.During : TheatreCommandKind.Repeat, Children = children, RepeatCount = count };
                }
                TheatreCommand command;
                if (op == "animate" || op == "tween")
                {
                    Count(c, 4, op);
                    string id = Id(c[1]); var property = Property(Id(c[2]));
                    if (!byId.ContainsKey(id)) Fail("unknown animation target: " + id);
                    if (byId[id].Shape == TheatreShape.Focus && property != TheatreProperty.X && property != TheatreProperty.Y && property != TheatreProperty.Opacity) Fail("focus only supports x/y/opacity");
                    if (property == TheatreProperty.Reveal && byId[id].Shape != TheatreShape.Line)
                        Fail("reveal only applies to lines: " + id);
                    TheatreKey[] keys; bool smooth = false;
                    if (op == "tween")
                    {
                        var target = List(c[3], 3, "tween target");
                        string easing = Id(target[2]);
                        if (easing != "linear" && easing != "smooth") Fail("easing must be linear or smooth");
                        smooth = easing == "smooth";
                        keys = new[] { new TheatreKey(0f, 0f), new TheatreKey(Positive(target[0]), Value(property, target[1])) };
                    }
                    else
                    {
                        var k = List(c[3]);
                        if (k.Count < 2) Fail("animation requires at least two keys");
                        keys = new TheatreKey[k.Count];
                        for (int i = 0; i < k.Count; i++)
                        {
                            var pair = List(k[i], 2, "keyframe");
                            float time = Number(pair[0]);
                            if ((i == 0 && time != 0f) || (i > 0 && time <= keys[i - 1].Time)) Fail("key times must start at zero and strictly increase");
                            keys[i] = new TheatreKey(time, Value(property, pair[1]));
                        }
                    }
                    command = new TheatreCommand { Kind = TheatreCommandKind.Animate, Target = id, Property = property,
                        Keys = keys, FromCurrent = op == "tween", Smooth = smooth, Seconds = keys[keys.Length - 1].Time };
                }
                else if (op == "volume")
                {
                    Count(c, 4, op); string id = Id(c[1]); float value = Number(c[2]), seconds = Positive(c[3]);
                    if (value < 0 || value > 1) Fail("volume must be 0..1");
                    command = new TheatreCommand { Kind = TheatreCommandKind.SoundVolume, Target = id, FromCurrent = true, Seconds = seconds,
                        Keys = new[] { new TheatreKey(0, 0), new TheatreKey(seconds, value) } };
                }
                else if (op == "image")
                {
                    Count(c, 3, op); string id = Id(c[1]); string asset = Id(c[2]); Asset(asset);
                    if (!byId.TryGetValue(id, out var image) || image.Shape != TheatreShape.Image) Fail("image command requires an image object: " + id);
                    command = new TheatreCommand { Kind = TheatreCommandKind.Image, Target = id, Asset = asset };
                }
                else if (op == "wait") { Count(c, 2, op); command = new TheatreCommand { Kind = TheatreCommandKind.Wait, Seconds = Positive(c[1]) }; }
                else if (op == "clear-caption") { Count(c, 1, op); command = new TheatreCommand { Kind = TheatreCommandKind.ClearCaption }; }
                else if (op == "say" || op == "caption-for")
                {
                    Count(c, op == "say" ? 3 : 5, op);
                    command = new TheatreCommand { Kind = TheatreCommandKind.Say, Target = Id(c[1]), Text = Id(c[2]),
                        Seconds = op == "say" ? 0f : Positive(c[3]),
                        CaptionColor = op == "say" ? new TheatreColor(.94f, .81f, .54f) : Color(Text(c[4])) };
                }
                else if (op == "sound")
                {
                    Count(c, 6, op); string id = Id(c[1]); string asset = Id(c[2]); Asset(asset);
                    if (c[3] is not bool loop) throw new ArgumentException("theatre: sound loop must be boolean");

                    float volume = Number(c[4]), pan = Number(c[5]);
                    if (volume < 0 || volume > 1 || pan < -1 || pan > 1) Fail("sound volume/pan out of range");
                    command = new TheatreCommand { Kind = TheatreCommandKind.Sound, Target = id, Asset = asset, Loop = loop, Volume = volume, Pan = pan };
                }
                else if (op == "stop-sound")
                {
                    Count(c, 2, op); string id = Id(c[1]);
                    command = new TheatreCommand { Kind = TheatreCommandKind.StopSound, Target = id };
                }
                else throw new ArgumentException("theatre: unknown command " + op);
                if (background && (command.Kind == TheatreCommandKind.Say || command.Kind == TheatreCommandKind.ClearCaption)) Fail("background cannot own subtitles");
                if (command.Seconds > 120f) Fail("one action cannot exceed 120 seconds");
                return command;
            }
            var program = ParseCue(rawProgram, false, 0);
            return new TheatreScene { Width = width, Height = height, Background = Color(Text(scene[2])), Nodes = nodes, Program = program };
        }
        private static HashSet<string> Writes(TheatreCommand cue)
        {
            var writes = new HashSet<string>(StringComparer.Ordinal);
            if (cue.Kind == TheatreCommandKind.Animate) writes.Add("object/" + cue.Target + "/" + cue.Property);
            if (cue.Kind == TheatreCommandKind.Image) writes.Add("object/" + cue.Target + "/asset");
            if (cue.Kind == TheatreCommandKind.Say || cue.Kind == TheatreCommandKind.ClearCaption) writes.Add("caption");
            if (cue.Kind == TheatreCommandKind.Sound || cue.Kind == TheatreCommandKind.StopSound) writes.Add("sound/" + cue.Target);
            if (cue.Kind == TheatreCommandKind.SoundVolume) writes.Add("sound/" + cue.Target);
            foreach (var child in cue.Children) writes.UnionWith(Writes(child));
            return writes;
        }
        private static float MinimumDuration(TheatreCommand cue)
        {
            if (cue.Kind == TheatreCommandKind.Say && cue.Seconds == 0) return float.NegativeInfinity;
            if (cue.Children.Count == 0) return cue.Seconds;
            float value = 0;
            foreach (var child in cue.Children)
            {
                float duration = MinimumDuration(child);
                if (float.IsNegativeInfinity(duration)) return duration;
                value = cue.Kind == TheatreCommandKind.Sequence ? value + duration : Math.Max(value, duration);
                if (cue.Kind == TheatreCommandKind.During) break;
            }
            return value;
        }
        public static TheatreProperty Property(string name) => name switch {
            "x" => TheatreProperty.X, "y" => TheatreProperty.Y, "scale-x" => TheatreProperty.ScaleX,
            "scale-y" => TheatreProperty.ScaleY, "rotation" => TheatreProperty.Rotation, "opacity" => TheatreProperty.Opacity,
            "reveal" => TheatreProperty.Reveal, "brightness" => TheatreProperty.Brightness,
            _ => throw new ArgumentException("theatre: unknown property " + name) };
        private static TheatreShape Shape(string name) => name switch {
            "group" => TheatreShape.Group, "line" => TheatreShape.Line, "polygon" => TheatreShape.Polygon,
            "glow" => TheatreShape.Glow, "image" => TheatreShape.Image, "light" => TheatreShape.Light, "focus" => TheatreShape.Focus,
            _ => throw new ArgumentException("theatre: unknown shape " + name) };
        private static float Value(TheatreProperty property, object raw)
        {
            float value = Number(raw);
            if ((property == TheatreProperty.Opacity || property == TheatreProperty.Reveal) && (value < 0f || value > 1f)) Fail("opacity/reveal must be 0..1");
            if (property == TheatreProperty.Brightness && (value < 0f || value > 4f)) Fail("brightness must be 0..4");
            if ((property == TheatreProperty.ScaleX || property == TheatreProperty.ScaleY) && value == 0f) Fail("scale cannot be zero");
            return value;
        }
        private static List<object> List(object raw) => raw as List<object> ?? throw new ArgumentException("theatre: expected list");
        private static List<object> List(object raw, int count, string label) { var list = List(raw); Count(list, count, label); return list; }
        private static void Count(List<object> list, int count, string label) { if (list.Count != count) Fail(label + " requires " + count + " fields"); }
        private static string Text(object raw) => raw is string text ? text : throw new ArgumentException("theatre: expected string");
        private static string Id(object raw) { if (raw is not string && raw is not Schemy.Symbol) Fail("identifier must be a string or symbol"); string id = SchemeValue.AsId(raw); if (string.IsNullOrWhiteSpace(id)) Fail("empty identifier/text"); return id; }
        private static float Number(object raw)
        {
            if (raw is not int && raw is not long && raw is not float && raw is not double && raw is not decimal) Fail("expected number");
            float value = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
            if (float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 10000f) Fail("number must be finite and within +/-10000");
            return value;
        }
        private static float Positive(object raw) { float n = Number(raw); if (n <= 0f) Fail("value must be positive"); return n; }
        private static TheatrePoint[] Points(object raw, int minimum)
        {
            var list = List(raw); if (list.Count < minimum) Fail("not enough path points");
            var points = new TheatrePoint[list.Count];
            for (int i = 0; i < list.Count; i++) { var p = List(list[i], 2, "point"); points[i] = new TheatrePoint(Number(p[0]), Number(p[1])); }
            return points;
        }
        private static float DistanceSquared(TheatrePoint a, TheatrePoint b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
        private static void ValidateConvex(TheatrePoint[] points, string id)
        {
            float sign = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                var a = points[i]; var b = points[(i + 1) % points.Length]; var c = points[(i + 2) % points.Length];
                if (DistanceSquared(a, b) < 0.0001f) Fail("polygon has duplicate adjacent points: " + id);
                float cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                if (Math.Abs(cross) < 0.0001f) continue;
                if (sign != 0f && Math.Sign(sign) != Math.Sign(cross)) Fail("polygon must be convex: " + id);
                sign = cross;
            }
            if (sign == 0f) Fail("polygon has no area: " + id);
            // A consistently turning star can still self-intersect; reject nonadjacent edge crossings.
            for (int i = 0; i < points.Length; i++)
                for (int j = i + 2; j < points.Length; j++)
                {
                    if (i == 0 && j == points.Length - 1) continue;
                    if (Cross(points[i], points[(i + 1) % points.Length], points[j]) * Cross(points[i], points[(i + 1) % points.Length], points[(j + 1) % points.Length]) < 0f
                        && Cross(points[j], points[(j + 1) % points.Length], points[i]) * Cross(points[j], points[(j + 1) % points.Length], points[(i + 1) % points.Length]) < 0f)
                        Fail("polygon self-intersects: " + id);
                }
        }
        private static float Cross(TheatrePoint a, TheatrePoint b, TheatrePoint c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        private static TheatreColor Color(string text)
        {
            if ((text.Length != 7 && text.Length != 9) || text[0] != '#' || !uint.TryParse(text.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hex))
                throw new ArgumentException("theatre: color must be #RRGGBB or #RRGGBBAA");
            if (text.Length == 7) hex = (hex << 8) | 255u;
            return new TheatreColor((hex >> 24) / 255f, ((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);
        }
        private static void Asset(string path) { if (string.IsNullOrWhiteSpace(path) || path.StartsWith("/") || path.Contains("..") || path.Contains("\\") || path.Contains(":")) Fail("asset must be a relative Resources path"); }
        private static void Fail(string message) => throw new ArgumentException("theatre: " + message);
    }
}
