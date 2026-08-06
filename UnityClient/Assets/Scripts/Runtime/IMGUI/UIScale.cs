#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    /// <summary>
    /// Applies a GUI.matrix scale so all IMGUI code is written in a virtual
    /// coordinate space that stretches to fill any physical screen.
    ///
    /// Design height is fixed (1080). Scale is snapped to a "nice" fraction so
    /// that font and border pixels land on integer physical-pixel boundaries.
    /// Virtual width (VW) is dynamic — wider phones simply show more content,
    /// with no letterbox bars.
    ///
    /// Usage in OnGUI():
    ///   1. Call UIScale.Apply() once at the top (before IMGUIStyles.Init).
    ///   2. Use UIScale.VW / UIScale.VH for screen-edge anchoring.
    ///   3. Use Event.current.mousePosition directly — Unity auto-transforms it
    ///      into virtual space when GUI.matrix is active.
    ///   4. Convert Camera.WorldToScreenPoint with UIScale.WorldPointToVirtual().
    ///   5. Wrap border/panel Rects in UIScale.PixelSnap() to eliminate
    ///      sub-pixel edge blur at non-integer scales.
    /// </summary>
    public static class UIScale
    {
        // All virtual pixel values are designed against this height.
        private const float DesignHeight = 1080f;

        // Fractions with denominator ≤ 4.  Snapping to these keeps
        // (fontSize × scale) within 0.25 px of an integer.
        private static readonly float[] NiceScales =
            { 0.5f, 0.75f, 1f, 1.25f, 1.5f };

        private static float _scale = 1f;

        /// <summary>Current active scale factor (physical px / virtual px).</summary>
        public static float Scale => _scale;

        /// <summary>Virtual canvas width — dynamic, fills the physical screen.</summary>
        public static float VW { get; private set; } = 1920f;

        /// <summary>Virtual canvas height — always ≥ DesignHeight when snapping down.</summary>
        public static float VH { get; private set; } = 1080f;

        /// <summary>
        /// Call once at the start of OnGUI(). Sets GUI.matrix and recomputes
        /// VW / VH for this frame.
        /// </summary>
        public static void Apply()
        {
            float raw = Screen.height / DesignHeight;
            _scale = SnapNearest(raw);
            VH = Screen.height / _scale;
            VW = Screen.width  / _scale;
            GUI.matrix = Matrix4x4.TRS(
                Vector3.zero,
                Quaternion.identity,
                new Vector3(_scale, _scale, 1f));
        }

        // Returns the NiceScale closest to raw.
        // Snap-nearest (not snap-down) keeps the scale close to actual so the UI
        // doesn't suddenly jump half-size when the screen is just below a threshold.
        private static float SnapNearest(float raw)
        {
            float best = NiceScales[0];
            float bestDist = Mathf.Abs(raw - NiceScales[0]);
            foreach (var s in NiceScales)
            {
                float d = Mathf.Abs(raw - s);
                if (d < bestDist) { bestDist = d; best = s; }
            }
            return best;
        }

        // ── Pixel alignment ─────────────────────────────────────────────────
        // Snap virtual coordinates so they land on exact physical pixels.
        // Use on Rect bounds of panels/borders where sub-pixel gaps look blurry.

        public static float Floor(float v) => Mathf.Floor(v * _scale) / _scale;
        public static float Ceil(float v)  => Mathf.Ceil(v  * _scale) / _scale;

        /// <summary>
        /// Snap all four edges of a Rect to the nearest physical pixel boundary.
        /// Apply to any Rect that has a visible border or solid background.
        /// </summary>
        public static Rect PixelSnap(Rect r) => new Rect(
            Floor(r.x),
            Floor(r.y),
            Ceil(r.x + r.width)  - Floor(r.x),
            Ceil(r.y + r.height) - Floor(r.y));

        // ── Coordinate conversions ───────────────────────────────────────────

        /// <summary>
        /// Convert Camera.WorldToScreenPoint (actual screen pixels, Y-up) to
        /// virtual GUI coordinates (Y-down from top-left).
        /// </summary>
        public static Vector2 WorldPointToVirtual(Vector3 screenPoint) =>
            new Vector2(
                screenPoint.x / _scale,
                (Screen.height - screenPoint.y) / _scale);

        /// <summary>
        /// Scale a virtual size value to actual screen pixels. GL positions should use
        /// GUIUtility.GUIToScreenPoint so the current GUI group origin is preserved.
        /// </summary>
        public static float ScaleSize(float v) => v * _scale;
    }
}
