using System.Numerics;
using System.Text.Json;
using SSNoir;

if (args.Length != 2) throw new ArgumentException("Usage: camera-review cameras.json routes.json");
var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = true };
var input = JsonSerializer.Deserialize<Input>(File.ReadAllText(args[0]), options) ?? throw new Exception("Missing input");
if (input.Cameras.Count < 2 || input.Samples < 20) throw new Exception("Need >=2 cameras and >=20 samples");
Vector3 V(float[] a) => a.Length == 3 ? new(a[0], a[1], a[2]) : throw new Exception("Invalid vector");
Quaternion Q(float[] a) => a.Length == 4 ? Quaternion.Normalize(new(a[0], a[1], a[2], a[3])) : throw new Exception("Invalid quaternion");
float[] A(Vector3 a) => [a.X, a.Y, a.Z];
// 回归：背面来向保留作者构图；同半边仍按最近侧解，不能把 Orbit 一律锁死。
foreach (float yaw in new[] { 35f, 125f })
{
    var authored = new Vector3(0, 6, 20);
    var start = FocusTravelMath.FromPolar(new Vector3(yaw, 30, 100));
    var rotation = FocusTravelMath.Look(-authored);
    var plan = FocusTravelMath.Create(start, FocusTravelMath.Look(-start), authored, rotation,
        Vector3.Zero, Vector3.Zero, true, false, 16, 35, 1, 27, 27,
        FocusTravelPath.Default, 2, false) ?? throw new Exception("Missing Orbit regression plan");
    if (yaw > 90 && (Vector3.Distance(plan.End, authored) > .001f || FocusTravelMath.Angle(plan.EndRotation, rotation) > .1f))
        throw new Exception("Rear approach discarded authored facade");
    if (yaw < 90 && Vector3.Distance(plan.End, authored) < 1f)
        throw new Exception("Front approach no longer resolves nearest Orbit side");
}
var routes = new List<object>();
foreach (var source in input.Cameras)
    foreach (var target in input.Cameras)
    {
        if (source.Name == target.Name) continue;
        if (source.Mode is not ("pan" or "static" or "orbit") || target.Mode is not ("pan" or "static" or "orbit")) throw new Exception("Unknown mode");
        Vector3? a = source.Pivot != null ? V(source.Pivot) : null;
        Vector3? b = target.Pivot != null ? V(target.Pivot) : null;
        var plan = FocusTravelMath.Create(V(source.Position), Q(source.Rotation), V(target.Position), Q(target.Rotation), a, b,
            target.Mode == "orbit", target.Mode == "pan", target.MinPitch, target.MaxPitch, input.ModelScale,
            source.Fov, target.Fov, FocusTravelPath.Default, input.DefaultDuration, false) ?? throw new Exception($"No route: {source.Name} -> {target.Name}");
        if (a.HasValue && Vector3.Distance(plan.A, a.Value) > .001f)
            throw new Exception($"Explicit source focus was overwritten: {source.Name}");
        if (b.HasValue && Vector3.Distance(plan.B, b.Value) > .001f)
            throw new Exception($"Explicit target focus was overwritten: {target.Name}");
        var space = target.Volume;
        bool cut = space != null && !plan.Fits(new FocusTravelMath.TravelVolume
        {
            Center = V(space.Center),
            HalfSize = V(space.HalfSize),
            Rotation = Q(space.Rotation)
        }, source.Fov, target.Fov, source.Near, target.Near);
        var frames = new List<object>();
        for (int i = 0; i <= input.Samples; i++)
        {
            float progress = i / (float)input.Samples, t = FocusTravelMath.Ease(progress, plan.Path);
            plan.Sample(progress, out var p, out var q);
            if (!float.IsFinite(p.X + p.Y + p.Z + q.X + q.Y + q.Z + q.W)) throw new Exception("Non-finite pose");
            frames.Add(new
            {
                Progress = progress,
                Position = A(p),
                Forward = A(FocusTravelMath.Forward(q)),
                Up = A(Vector3.Transform(Vector3.UnitY, q)),
                Fov = source.Fov + (target.Fov - source.Fov) * t,
                Near = source.Near + (target.Near - source.Near) * t
            });
        }
        plan.Sample(0, out var first, out var firstRotation); plan.Sample(1, out var last, out var lastRotation);
        if (Vector3.Distance(first, plan.Start) > .001f || Vector3.Distance(last, plan.End) > .001f || FocusTravelMath.Angle(firstRotation, plan.StartRotation) > .1f || FocusTravelMath.Angle(lastRotation, plan.EndRotation) > .1f)
            throw new Exception($"Endpoint mismatch: {source.Name} -> {target.Name}");
        routes.Add(new { From = source.Name, To = target.Name, Path = plan.Path.ToString(), Crossfade = cut, plan.Duration, StartInterest = A(plan.A), TargetInterest = A(plan.B), Frames = frames });
    }
File.WriteAllText(args[1], JsonSerializer.Serialize(new { Source = "Shared FocusTravelMath.cs; authored endpoints, no player offsets", input.ModelScale, Routes = routes }, options));
Console.WriteLine($"Sampled {routes.Count} routes using shared runtime math.");

sealed class Input
{
    public List<Shot> Cameras { get; set; } = [];
    public int Samples { get; set; } = 100;
    public float ModelScale { get; set; } = 1;
    public float DefaultDuration { get; set; } = 2;
}
sealed class Shot
{
    public string Name { get; set; } = "";
    public string Mode { get; set; } = "";
    public float[] Position { get; set; } = [];
    public float[] Rotation { get; set; } = [];
    public float[]? Pivot { get; set; }
    public Space? Volume { get; set; }
    public float Fov { get; set; }
    public float Near { get; set; }
    public float MinPitch { get; set; } = 16;
    public float MaxPitch { get; set; } = 35;
}

sealed class Space
{
    public float[] Center { get; set; } = [];
    public float[] HalfSize { get; set; } = [];
    public float[] Rotation { get; set; } = [];
}
