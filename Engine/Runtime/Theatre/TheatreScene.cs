#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Theatre
{
    // Pure presentation data. No Unity objects, Scheme procedures or game state enter this module.
    public enum TheatreShape { Group, Line, Polygon, Glow, Image, Light, Focus }
    public enum TheatreProperty { X, Y, ScaleX, ScaleY, Rotation, Opacity, Reveal, Brightness }
    public enum TheatreCommandKind { Animate, Image, Wait, Say, Sound, StopSound, ClearCaption, SoundVolume, Sequence, Parallel, During, Repeat }
    public readonly struct TheatrePoint
    {
        public readonly float X, Y;
        public TheatrePoint(float x, float y) { X = x; Y = y; }
    }
    public readonly struct TheatreColor
    {
        public readonly float R, G, B, A;
        public TheatreColor(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; }
    }
    public readonly struct TheatreKey
    {
        public readonly float Time, Value;
        public TheatreKey(float time, float value) { Time = time; Value = value; }
    }
    public sealed class TheatreNode
    {
        public string Id { get; init; } = "";
        public string Parent { get; init; } = "";
        public TheatreShape Shape { get; init; }
        public TheatrePoint[] Points { get; init; } = Array.Empty<TheatrePoint>();
        public float Width { get; init; }
        public float Height { get; init; }
        public TheatreColor Color { get; init; }
        public string Asset { get; init; } = "";
        public string Light { get; init; } = "";
        public IReadOnlyDictionary<TheatreProperty, float> Initial { get; init; }
            = new Dictionary<TheatreProperty, float>();
    }
    public sealed class TheatreCommand
    {
        public TheatreCommandKind Kind { get; init; }
        public string Target { get; init; } = "";
        public TheatreProperty Property { get; init; }
        public TheatreKey[] Keys { get; init; } = Array.Empty<TheatreKey>();
        public bool FromCurrent { get; init; }
        public bool Smooth { get; init; }
        public IReadOnlyList<TheatreCommand> Children { get; init; } = Array.Empty<TheatreCommand>();
        // RepeatCount=0 is an unbounded background loop, only allowed inside During.
        public int RepeatCount { get; init; } = 1;
        public float Seconds { get; init; }
        public string Text { get; init; } = "";
        public TheatreColor CaptionColor { get; init; } = new TheatreColor(0.94f, 0.81f, 0.54f);
        public string Asset { get; init; } = "";
        public bool Loop { get; init; }
        public float Volume { get; init; } = 1f;
        public float Pan { get; init; }
    }
    public sealed class TheatreScene
    {
        public float Width { get; init; }
        public float Height { get; init; }
        public TheatreColor Background { get; init; }
        // Source order is painter order; parents must precede their children.
        public IReadOnlyList<TheatreNode> Nodes { get; init; } = Array.Empty<TheatreNode>();
        public TheatreCommand Program { get; init; } = new TheatreCommand { Kind = TheatreCommandKind.Sequence };
        public IEnumerable<TheatreCommand> Commands => Walk(Program);
        private static IEnumerable<TheatreCommand> Walk(TheatreCommand cue)
        {
            if (cue.Children.Count == 0) yield return cue;
            foreach (var child in cue.Children)
                foreach (var command in Walk(child)) yield return command;
        }
    }
}
