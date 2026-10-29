#nullable enable
using System;
using System.Numerics;

namespace SSNoir
{
    public enum FocusTravelPath { Default, DirectApproach, AuthoredArc }

    // 无 Unity 场景依赖。游戏、出发前预测和离线审阅直接编译同一份代码。
    public static class FocusTravelMath
    {
        public const float DirectLift = .04f;
        private const float Radians = (float)Math.PI / 180f;
        public static float Ease(float t, FocusTravelPath path) => path == FocusTravelPath.DirectApproach
            ? t * t * t * (t * (t * 6f - 15f) + 10f) : t * t * (3f - 2f * t);
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        private static float Clamp(float v, float a, float b) => Math.Max(a, Math.Min(b, v));
        public static float Angle(Quaternion a, Quaternion b) => 2f * (float)Math.Acos(Clamp(Math.Abs(Quaternion.Dot(a, b)), 0, 1)) / Radians;
        public static Vector3 Forward(Quaternion rotation) => Vector3.Transform(Vector3.UnitZ, rotation);
        public static Quaternion Look(Vector3 forward)
        {
            forward = Vector3.Normalize(forward);
            Vector3 right = Vector3.Cross(Vector3.UnitY, forward);
            // 垂直视线的方位无唯一解；采用固定右轴，避免 NaN，预测器继续检查突转。
            right = right.LengthSquared() < 1e-10f ? Vector3.UnitX : Vector3.Normalize(right);
            Vector3 up = Vector3.Cross(forward, right);
            return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(new Matrix4x4(
                right.X, right.Y, right.Z, 0, up.X, up.Y, up.Z, 0,
                forward.X, forward.Y, forward.Z, 0, 0, 0, 0, 1)));
        }
        public static Quaternion AimOffset(Vector3 p, Quaternion rotation, Vector3 interest) =>
            (interest - p).LengthSquared() < .0001f ? Quaternion.Identity : Quaternion.Inverse(Look(interest - p)) * rotation;
        public static Quaternion AimedAt(Vector3 p, Vector3 interest, Quaternion aim, Quaternion fallback) =>
            (interest - p).LengthSquared() < .0001f ? fallback : Look(interest - p) * aim;
        public static Vector3 Polar(Vector3 v) => new Vector3(
            (float)Math.Atan2(v.X, v.Z) / Radians,
            (float)Math.Atan2(v.Y, Math.Sqrt(v.X * v.X + v.Z * v.Z)) / Radians, v.Length());
        public static Vector3 FromPolar(Vector3 polar)
        {
            float yaw = polar.X * Radians, pitch = polar.Y * Radians, r = polar.Z;
            return new Vector3(r * (float)Math.Cos(pitch) * (float)Math.Sin(yaw), r * (float)Math.Sin(pitch), r * (float)Math.Cos(pitch) * (float)Math.Cos(yaw));
        }
        public static Vector3 ArcPosition(Vector3 startInterest, Vector3 endInterest, Vector3 startPolar, Vector3 endPolar, float t)
        {
            float delta = (endPolar.X - startPolar.X) % 360f;
            if (delta < 0) delta += 360f;
            if (delta > 180f) delta -= 360f;
            return Vector3.Lerp(startInterest, endInterest, t) + FromPolar(new Vector3(
                startPolar.X + delta * t, Lerp(startPolar.Y, endPolar.Y, t), Lerp(startPolar.Z, endPolar.Z, t)));
        }
        public static void DirectPosition(Vector3 start, Vector3 end, Vector3 startInterest, Vector3 endInterest, float t,
            out Vector3 interest, out Vector3 position)
        {
            interest = Vector3.Lerp(startInterest, endInterest, t);
            position = Vector3.Lerp(start, end, t);
            position.Y += (float)Math.Sin(Math.PI * t) * Vector3.Distance(start, end) * DirectLift;
        }
        public static bool GroundInterest(Vector3 position, Quaternion rotation, float height, float maxDistance, out Vector3 interest)
        {
            Vector3 forward = Forward(rotation); interest = Vector3.Zero;
            if (Math.Abs(forward.Y) < 1e-6f) return false;
            float distance = (height - position.Y) / forward.Y;
            if (distance <= 0 || distance > maxDistance) return false;
            interest = position + forward * distance; return true;
        }
        public static bool ResolveInterests(Vector3 start, Quaternion startRotation, Vector3 end, Quaternion endRotation,
            Vector3? sourceInterest, Vector3? targetInterest, out Vector3 a, out Vector3 b)
        {
            float height = targetInterest?.Y ?? sourceInterest?.Y ?? 0f;
            bool hasA = GroundInterest(start, startRotation, height, 500f, out a);
            bool hasB = GroundInterest(end, endRotation, height, 500f, out b);
            if (sourceInterest.HasValue) { a = sourceInterest.Value; hasA = true; }
            if (targetInterest.HasValue) { b = targetInterest.Value; hasB = true; }
            if (!hasA && !hasB) return false;
            if (!hasA) a = b; if (!hasB) b = a; return true;
        }
        public static bool DirectLosesTarget(Vector3 start, Vector3 end, Quaternion startRotation, Quaternion endRotation,
            Vector3 a, Vector3 b, float startFov, float endFov)
        {
            Quaternion startAim = AimOffset(start, startRotation, a), endAim = AimOffset(end, endRotation, b), previous = startRotation;
            int missed = 0;
            float allowed = Math.Max(90f, Angle(startRotation, endRotation) * 1.875f) * 2f;
            const int samples = 20;
            for (int i = 0; i <= samples; i++)
            {
                float progress = i / (float)samples, t = Ease(progress, FocusTravelPath.DirectApproach);
                DirectPosition(start, end, a, b, t, out var interest, out var p);
                Vector3 target = b - p; if (target.LengthSquared() < .0001f) return true;
                Quaternion rotation = AimedAt(p, interest, Quaternion.Slerp(startAim, endAim, t), Quaternion.Slerp(startRotation, endRotation, t));
                if (i > 0 && Angle(previous, rotation) * samples > allowed) return true;
                previous = rotation;
                float dot = Vector3.Dot(Vector3.Normalize(Forward(rotation)), Vector3.Normalize(target));
                if (progress >= .4f && progress <= .8f && (float)Math.Acos(Clamp(dot, -1, 1)) / Radians > Lerp(startFov, endFov, t) * .5f) missed++;
            }
            return missed >= 5;
        }

        // 地点作者声明的凸盒；与相机的交互 PanBounds 无关。
        public sealed class TravelVolume
        {
            public Vector3 Center, HalfSize;
            public Quaternion Rotation;
            public bool Contains(Vector3 world)
            {
                Vector3 local = Vector3.Transform(world - Center, Quaternion.Inverse(Rotation));
                return Math.Abs(local.X) <= HalfSize.X && Math.Abs(local.Y) <= HalfSize.Y && Math.Abs(local.Z) <= HalfSize.Z;
            }
        }

        public sealed class Plan
        {
            public Vector3 Start, End, A, B;
            public Quaternion StartRotation, EndRotation;
            public FocusTravelPath Path;
            public float Duration;
            public bool Fits(TravelVolume volume, float startFov, float endFov, float startNear, float endNear, float aspect = 16f / 9f)
            {
                const int samples = 120;
                for (int i = 0; i <= samples; i++)
                {
                    float progress = i / (float)samples, t = Ease(progress, Path);
                    Sample(progress, out var p, out var q);
                    if (!volume.Contains(p)) return false;
                    float near = Lerp(startNear, endNear, t);
                    float halfHeight = (float)Math.Tan(Lerp(startFov, endFov, t) * Radians * .5f) * near;
                    Vector3 center = p + Forward(q) * near;
                    Vector3 right = Vector3.Transform(Vector3.UnitX, q) * halfHeight * aspect;
                    Vector3 up = Vector3.Transform(Vector3.UnitY, q) * halfHeight;
                    if (!volume.Contains(center + right + up) || !volume.Contains(center + right - up)
                        || !volume.Contains(center - right + up) || !volume.Contains(center - right - up)) return false;
                }
                return true;
            }
            public void Sample(float progress, out Vector3 position, out Quaternion rotation)
            {
                float t = Ease(progress, Path);
                Vector3 interest = Vector3.Lerp(A, B, t);
                if (Path == FocusTravelPath.DirectApproach) DirectPosition(Start, End, A, B, t, out interest, out position);
                else position = ArcPosition(A, B, Polar(Start - A), Polar(End - B), t);
                rotation = AimedAt(position, interest, Quaternion.Slerp(AimOffset(Start, StartRotation, A), AimOffset(End, EndRotation, B), t), Quaternion.Slerp(StartRotation, EndRotation, t));
            }
        }
        public static Plan? Create(Vector3 start, Quaternion startRotation, Vector3 end, Quaternion endRotation,
            Vector3? sourceInterest, Vector3? targetInterest, bool targetOrbit, bool targetPan,
            float minPitch, float maxPitch, float modelScale, float startFov, float endFov,
            FocusTravelPath requested, float defaultDuration, bool redirected)
        {
            bool automatic = requested == FocusTravelPath.Default;
            var path = automatic ? FocusTravelPath.DirectApproach : requested;
            if (!ResolveInterests(start, startRotation, end, endRotation, sourceInterest, targetInterest, out var a, out var b))
            {
                if (path == FocusTravelPath.AuthoredArc) return null;
                float depth = Math.Max(Vector3.Distance(start, end), 1f);
                a = start + Forward(startRotation) * depth; b = end + Forward(endRotation) * depth;
            }
            Vector3 authoredA = a, authoredB = b;
            float duration = defaultDuration;
            if (path == FocusTravelPath.DirectApproach)
            {
                if (targetOrbit)
                {
                    Vector3 polar = Polar(end - b), incoming = start - b;
                    float incomingYaw = incoming.X * incoming.X + incoming.Z * incoming.Z < .0001f
                        ? (float)Math.Atan2(-Forward(startRotation).X, -Forward(startRotation).Z) / Radians
                        : (float)Math.Atan2(incoming.X, incoming.Z) / Radians;
                    // 最近侧不能越过作者观看方向的背半边：单面主体和街边建筑
                    // 在背面可能不可读或被邻楼遮挡。此时保留完整作者构图。
                    if (Math.Cos((incomingYaw - polar.X) * Radians) > 0)
                    {
                        polar.X = incomingYaw;
                        polar.Y = Clamp(polar.Y, minPitch, maxPitch);
                        end = b + FromPolar(polar); endRotation = Look(b - end);
                    }
                }
                if (!sourceInterest.HasValue && !redirected && GroundInterest(start, startRotation, b.Y, float.PositiveInfinity, out var groundA)) a = groundA;
                if (!targetInterest.HasValue && targetPan && GroundInterest(end, endRotation, b.Y, float.PositiveInfinity, out var groundB)) b = groundB;
                duration = Math.Max(Clamp(Vector3.Distance(start, end) / modelScale * .0012f, .9f, 1.5f), Angle(startRotation, endRotation) * 1.875f / 90f);
                if (automatic && DirectLosesTarget(start, end, startRotation, endRotation, a, b, startFov, endFov))
                { path = FocusTravelPath.AuthoredArc; a = authoredA; b = authoredB; duration = defaultDuration; }
            }
            if ((end - b).LengthSquared() < .0001f || (start - a).LengthSquared() < .0001f) return null;
            return new Plan { Start = start, End = end, StartRotation = startRotation, EndRotation = endRotation, A = a, B = b, Path = path, Duration = duration };
        }
    }
}
