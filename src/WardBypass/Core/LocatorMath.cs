using System;

namespace WardBypass
{
    // Where a ward marker goes on screen. X and Y are GUI coordinates (origin top left, y down).
    internal struct MarkerPlacement
    {
        public float X;
        public float Y;
        public bool OnScreen;

        // Direction from the screen center towards the ward in degrees, clockwise from pointing right.
        // Only meaningful for off-screen markers, where it turns the edge arrow.
        public float Angle;
    }

    // Game-independent layout for the ward locator: screen placement with edge clamping, distance styling,
    // labels and the procedural marker textures. No Unity types, so the portable tests can link it.
    internal static class LocatorMath
    {
        internal const float NearDistance = 8f;
        internal const float MinScale = 0.6f;
        internal const float MinAlpha = 0.55f;

        // screenX/screenY/depth are a Unity screen point (origin bottom left, depth negative behind the camera).
        internal static MarkerPlacement Place(float screenX, float screenY, float depth, float width, float height, float margin)
        {
            var result = new MarkerPlacement();
            if (!(width > 0f) || !(height > 0f) || IsBad(screenX) || IsBad(screenY) || IsBad(depth))
            {
                result.X = width > 0f ? width * 0.5f : 0f;
                result.Y = height > 0f ? height * 0.5f : 0f;
                return result;
            }

            margin = Math.Max(0f, Math.Min(margin, Math.Min(width, height) * 0.5f));
            float x = screenX;
            float y = height - screenY;
            bool behind = depth < 0f;
            if (behind)
            {
                // A point behind the camera projects mirrored through the screen center.
                x = width - x;
                y = height - y;
            }

            if (!behind && x >= margin && x <= width - margin && y >= margin && y <= height - margin)
            {
                result.X = x;
                result.Y = y;
                result.OnScreen = true;
                return result;
            }

            float cx = width * 0.5f;
            float cy = height * 0.5f;
            float dx = x - cx;
            float dy = y - cy;
            if (Math.Abs(dx) < 1e-3f && Math.Abs(dy) < 1e-3f)
            {
                // Straight behind: point down, towards the player.
                dx = 0f;
                dy = 1f;
            }

            float halfWidth = cx - margin;
            float halfHeight = cy - margin;
            float scaleX = Math.Abs(dx) > 1e-6f ? halfWidth / Math.Abs(dx) : float.MaxValue;
            float scaleY = Math.Abs(dy) > 1e-6f ? halfHeight / Math.Abs(dy) : float.MaxValue;
            float scale = Math.Min(scaleX, scaleY);
            result.X = cx + dx * scale;
            result.Y = cy + dy * scale;
            result.Angle = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
            return result;
        }

        // 0 at NearDistance or closer, 1 at range or farther.
        internal static float Farness(float distance, float range)
        {
            if (IsBad(distance) || IsBad(range) || range <= NearDistance) return distance > NearDistance ? 1f : 0f;
            return Clamp01((distance - NearDistance) / (range - NearDistance));
        }

        internal static float MarkerScale(float distance, float range) => 1f - (1f - MinScale) * Farness(distance, range);

        internal static float MarkerAlpha(float distance, float range) => 1f - (1f - MinAlpha) * Farness(distance, range);

        internal static int Meters(float distance)
        {
            if (IsBad(distance) || distance <= 0f) return 0;
            if (distance >= 99999f) return 99999;
            return (int)Math.Round(distance);
        }

        internal static string Label(string owner, int meters)
        {
            string distance = meters + " m";
            return string.IsNullOrEmpty(owner) ? distance : owner + "\n" + distance;
        }

        // Soft glow behind the icon: full in the middle, fading to nothing at the edge.
        internal static float DiscAlpha(int x, int y, int size)
        {
            float r = Radius(x, y, size);
            float t = Clamp01(1f - r);
            return t * t;
        }

        // Crisp anti-aliased solid dot that fills most of the texture.
        internal static float DotAlpha(int x, int y, int size)
        {
            float r = Radius(x, y, size);
            float pixel = 2f / size;
            return Clamp01((0.9f - r) / pixel + 0.5f);
        }

        // Thin anti-aliased ring near the edge of the texture.
        internal static float RingAlpha(int x, int y, int size)
        {
            const float center = 0.84f;
            const float halfWidth = 0.07f;
            float r = Radius(x, y, size);
            float pixel = 2f / size;
            return Clamp01((halfWidth - Math.Abs(r - center)) / pixel + 0.5f);
        }

        // Anti-aliased chevron pointing right (towards +x).
        internal static float ArrowAlpha(int x, int y, int size)
        {
            float px = (x + 0.5f) / size * 2f - 1f;
            float py = (y + 0.5f) / size * 2f - 1f;
            float pixel = 2f / size;
            // Outer triangle: tip at (0.8, 0), back corners at (-0.6, +-0.7).
            float outer = Triangle(px, py, 0.8f, -0.6f, 0.7f, pixel);
            // Notch cut from the back to make it a chevron.
            float notch = Triangle(px, py, 0.05f, -0.75f, 0.45f, pixel);
            return Clamp01(outer - notch);
        }

        private static float Triangle(float px, float py, float tip, float back, float halfHeight, float pixel)
        {
            if (px < back - pixel || px > tip + pixel) return 0f;
            float length = tip - back;
            // Distance inside the slanted edges, measured perpendicular to them.
            float slope = halfHeight / length;
            float edge = ((tip - px) * slope - Math.Abs(py)) / (float)Math.Sqrt(1f + slope * slope);
            float backEdge = px - back;
            return Clamp01(Math.Min(edge, backEdge) / pixel + 0.5f);
        }

        private static float Radius(int x, int y, int size)
        {
            float px = (x + 0.5f) / size * 2f - 1f;
            float py = (y + 0.5f) / size * 2f - 1f;
            return (float)Math.Sqrt(px * px + py * py);
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        private static bool IsBad(float value) => float.IsNaN(value) || float.IsInfinity(value);
    }
}
