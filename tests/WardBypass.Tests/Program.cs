using System;
using System.Collections.Generic;
using WardBypass;

internal static class Program
{
    private const float Width = 1920f;
    private const float Height = 1080f;
    private const float Margin = 48f;

    private static int Main()
    {
        var scenarios = new List<(string Name, Action Run)>
        {
            ("VisiblePointKeepsItsPosition", VisiblePointKeepsItsPosition),
            ("PointOffTheRightEdgeIsPinnedThere", PointOffTheRightEdgeIsPinnedThere),
            ("PointBehindTheCameraIsMirroredToAnEdge", PointBehindTheCameraIsMirroredToAnEdge),
            ("PlacementNeverLeavesTheMargins", PlacementNeverLeavesTheMargins),
            ("InvalidInputCentersTheMarker", InvalidInputCentersTheMarker),
            ("MarkersShrinkAndFadeWithDistance", MarkersShrinkAndFadeWithDistance),
            ("LabelsShowOwnerAndMeters", LabelsShowOwnerAndMeters),
            ("MarkerTexturesHaveTheirShapes", MarkerTexturesHaveTheirShapes),
        };
        int failed = 0;
        foreach (var (name, run) in scenarios)
        {
            try { run(); Console.WriteLine("PASS " + name); }
            catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error.Message); }
        }
        Console.WriteLine(scenarios.Count + " WardBypass scenario(s), " + failed + " failed. No game runtime was exercised.");
        return failed == 0 ? 0 : 1;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static bool Near(double a, double b, double tolerance = 1e-3) => Math.Abs(a - b) <= tolerance;

    private static void VisiblePointKeepsItsPosition()
    {
        // Unity screen y grows upwards, GUI y downwards.
        MarkerPlacement place = LocatorMath.Place(500f, 300f, 20f, Width, Height, Margin);
        Check(place.OnScreen, "a point inside the view is on screen");
        Check(Near(place.X, 500f) && Near(place.Y, Height - 300f), "position converted to GUI space: " + place.X + ", " + place.Y);
    }

    private static void PointOffTheRightEdgeIsPinnedThere()
    {
        MarkerPlacement place = LocatorMath.Place(Width + 800f, Height * 0.5f, 20f, Width, Height, Margin);
        Check(!place.OnScreen, "off-screen point is flagged");
        Check(Near(place.X, Width - Margin) && Near(place.Y, Height * 0.5f), "pinned to the right margin: " + place.X + ", " + place.Y);
        Check(Near(place.Angle, 0f), "arrow points right: " + place.Angle);

        MarkerPlacement corner = LocatorMath.Place(Width + 5000f, -5000f, 20f, Width, Height, Margin);
        Check(corner.Angle > 0f && corner.Angle < 90f, "below-right points down and right: " + corner.Angle);
    }

    private static void PointBehindTheCameraIsMirroredToAnEdge()
    {
        // Directly behind projects to the screen center with negative depth.
        MarkerPlacement behind = LocatorMath.Place(Width * 0.5f, Height * 0.5f, -10f, Width, Height, Margin);
        Check(!behind.OnScreen, "behind the camera is never on screen");
        Check(Near(behind.Y, Height - Margin) && Near(behind.X, Width * 0.5f), "straight behind goes to the bottom edge: " + behind.X + ", " + behind.Y);
        Check(Near(behind.Angle, 90f), "arrow points down: " + behind.Angle);

        // Behind and to the left projects on the right half; mirrored it belongs on the left.
        MarkerPlacement left = LocatorMath.Place(Width * 0.75f, Height * 0.5f, -10f, Width, Height, Margin);
        Check(Near(left.X, Margin), "behind-left goes to the left edge: " + left.X);
    }

    private static void PlacementNeverLeavesTheMargins()
    {
        var random = new Random(41);
        for (int i = 0; i < 5000; i++)
        {
            float x = (float)(random.NextDouble() * 20000.0 - 10000.0);
            float y = (float)(random.NextDouble() * 20000.0 - 10000.0);
            float depth = (float)(random.NextDouble() * 200.0 - 100.0);
            MarkerPlacement place = LocatorMath.Place(x, y, depth, Width, Height, Margin);
            Check(place.X >= Margin - 0.01f && place.X <= Width - Margin + 0.01f, "x inside margins: " + place.X);
            Check(place.Y >= Margin - 0.01f && place.Y <= Height - Margin + 0.01f, "y inside margins: " + place.Y);
            Check(!float.IsNaN(place.Angle), "angle is a number");
        }
    }

    private static void InvalidInputCentersTheMarker()
    {
        MarkerPlacement nan = LocatorMath.Place(float.NaN, 10f, 5f, Width, Height, Margin);
        Check(!nan.OnScreen && Near(nan.X, Width * 0.5f) && Near(nan.Y, Height * 0.5f), "NaN input is contained");
        MarkerPlacement empty = LocatorMath.Place(10f, 10f, 5f, 0f, 0f, Margin);
        Check(!empty.OnScreen && empty.X == 0f && empty.Y == 0f, "zero-sized screen is contained");
        MarkerPlacement hugeMargin = LocatorMath.Place(Width * 2f, 0f, 5f, Width, Height, 5000f);
        Check(Near(hugeMargin.Y, Height * 0.5f), "a margin larger than the screen is clamped");
    }

    private static void MarkersShrinkAndFadeWithDistance()
    {
        Check(Near(LocatorMath.MarkerScale(0f, 100f), 1f) && Near(LocatorMath.MarkerScale(LocatorMath.NearDistance, 100f), 1f), "full size up close");
        Check(Near(LocatorMath.MarkerScale(100f, 100f), LocatorMath.MinScale) && Near(LocatorMath.MarkerScale(500f, 100f), LocatorMath.MinScale), "minimum size at range");
        Check(Near(LocatorMath.MarkerAlpha(100f, 100f), LocatorMath.MinAlpha), "minimum alpha at range");
        float previous = 2f;
        for (float d = 0f; d <= 120f; d += 1f)
        {
            float scale = LocatorMath.MarkerScale(d, 100f);
            Check(scale <= previous + 1e-6f, "scale never grows with distance");
            previous = scale;
        }
        Check(Near(LocatorMath.MarkerScale(50f, 5f), LocatorMath.MinScale), "a range below the near distance still works");
        float nanAlpha = LocatorMath.MarkerAlpha(float.NaN, 100f);
        Check(nanAlpha >= LocatorMath.MinAlpha && nanAlpha <= 1f, "NaN distance is contained");
    }

    private static void LabelsShowOwnerAndMeters()
    {
        Check(LocatorMath.Meters(23.4f) == 23 && LocatorMath.Meters(23.6f) == 24, "meters round");
        Check(LocatorMath.Meters(-3f) == 0 && LocatorMath.Meters(float.NaN) == 0 && LocatorMath.Meters(1e9f) == 99999, "meters are bounded");
        Check(LocatorMath.Label("Ragnar", 12) == "Ragnar\n12 m", "owner above distance");
        Check(LocatorMath.Label("", 7) == "7 m" && LocatorMath.Label(null, 7) == "7 m", "unknown owner shows distance only");
    }

    private static void MarkerTexturesHaveTheirShapes()
    {
        const int size = 64;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float disc = LocatorMath.DiscAlpha(x, y, size);
                float dot = LocatorMath.DotAlpha(x, y, size);
                Check(dot >= 0f && dot <= 1f, "dot alpha in range at " + x + "," + y);
                float ring = LocatorMath.RingAlpha(x, y, size);
                float arrow = LocatorMath.ArrowAlpha(x, y, size);
                Check(disc >= 0f && disc <= 1f && ring >= 0f && ring <= 1f && arrow >= 0f && arrow <= 1f, "alpha in range at " + x + "," + y);
            }
        Check(LocatorMath.DiscAlpha(32, 32, size) > 0.9f && LocatorMath.DiscAlpha(0, 0, size) == 0f, "disc is solid in the middle and empty in the corner");
        Check(LocatorMath.DotAlpha(32, 32, size) == 1f && LocatorMath.DotAlpha(56, 32, size) == 1f && LocatorMath.DotAlpha(62, 32, size) == 0f && LocatorMath.DotAlpha(0, 0, size) == 0f, "dot is solid with a crisp edge");
        Check(LocatorMath.RingAlpha(32, 32, size) == 0f && LocatorMath.RingAlpha(0, 0, size) == 0f, "ring is hollow and stays inside");
        // r = 0.84 along +x: pixel center (x + 0.5) / 32 - 1 = 0.84.
        Check(LocatorMath.RingAlpha(58, 32, size) > 0.9f, "ring is solid on its radius");
        Check(LocatorMath.ArrowAlpha(50, 32, size) > 0.9f, "arrow is solid towards the tip");
        Check(LocatorMath.ArrowAlpha(14, 32, size) == 0f, "the chevron notch is cut out");
        Check(LocatorMath.ArrowAlpha(2, 32, size) == 0f && LocatorMath.ArrowAlpha(62, 32, size) == 0f, "nothing beyond back or tip");
        Check(Near(LocatorMath.ArrowAlpha(40, 20, size), LocatorMath.ArrowAlpha(40, 43, size)), "arrow is symmetric");
    }
}
