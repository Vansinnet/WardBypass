using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace WardBypass
{
    // Draws a pin over every player-built ward the client has loaded within range, on top of the world so walls
    // and terrain do not hide it: a dot on the exact centre of the ward stone, a stem and the icon bubble above it.
    // Wards outside the view are pinned to the screen edge with an arrow.
    // Optionally keeps the vanilla area ring on the ground visible for nearby enabled wards. Read-only: it never
    // changes a ward, sends nothing and patches nothing.
    internal static class Locator
    {
        internal static bool Active;

        private const float ScanInterval = 0.25f;
        private const float BaseMarkerSize = 40f;
        private const float EdgeMargin = 56f;
        private const float FallbackAnchorHeight = 1f;
        private const int TextureSize = 64;

        private static readonly Color ForeignActive = new Color(1f, 0.6f, 0.22f);
        private static readonly Color OwnActive = new Color(0.45f, 0.88f, 0.55f);
        private static readonly Color Inactive = new Color(0.62f, 0.68f, 0.78f);

        private static ConfigEntry<float> range;
        private static ConfigEntry<int> maxMarkers;
        private static ConfigEntry<bool> showRadius;
        private static ConfigEntry<float> radiusRange;

        private static AccessTools.FieldRef<List<PrivateArea>> allAreas;
        private static AccessTools.FieldRef<PrivateArea, ZNetView> netView;
        private static AccessTools.FieldRef<PrivateArea, Piece> pieceOf;

        private static readonly List<Target> targets = new List<Target>();
        private static readonly Dictionary<Sprite, Rect> iconCoords = new Dictionary<Sprite, Rect>();
        private static int count;
        private static int found;
        private static float nextScan;

        private static Texture2D disc;
        private static Texture2D dot;
        private static Texture2D ring;
        private static Texture2D arrow;
        private static GUIStyle labelStyle;
        private static GUIContent labelContent;
        private static int labelFontSize;

        private sealed class Target
        {
            public PrivateArea Area;
            public Vector3 Position;
            public Vector3 Anchor;
            public float Distance;
            public bool Enabled;
            public bool Own;
            public bool Inside;
            public Sprite Icon;
            public string Owner;
            public int Meters = -1;
            public string Label = string.Empty;
            public string DistanceText = string.Empty;
        }

        private sealed class ByDistance : IComparer<Target>
        {
            internal static readonly ByDistance Instance = new ByDistance();
            public int Compare(Target a, Target b) => a.Distance.CompareTo(b.Distance);
        }

        internal static void Bind(ConfigFile config)
        {
            range = config.Bind("Locator", "Range", 100f, new ConfigDescription(
                "Show wards up to this many meters away. Only wards in the area your game has loaded can be shown.",
                new AcceptableValueRange<float>(10f, 300f)));
            maxMarkers = config.Bind("Locator", "MaxMarkers", 25, new ConfigDescription(
                "Show at most this many wards, nearest first.", new AcceptableValueRange<int>(1, 100)));
            showRadius = config.Bind("Locator", "ShowRadius", true,
                "Also show the protected area of nearby enabled wards as the vanilla ring on the ground.");
            radiusRange = config.Bind("Locator", "RadiusRange", 50f, new ConfigDescription(
                "Show the ground ring only for wards up to this many meters away.", new AcceptableValueRange<float>(0f, 150f)));
        }

        // Resolves the private ward members once. Returns false (and the locator stays off) when they are missing.
        internal static bool Initialize(out string error)
        {
            try
            {
                allAreas = AccessTools.StaticFieldRefAccess<List<PrivateArea>>(AccessTools.Field(typeof(PrivateArea), "m_allAreas"));
                netView = AccessTools.FieldRefAccess<PrivateArea, ZNetView>("m_nview");
                pieceOf = AccessTools.FieldRefAccess<PrivateArea, Piece>("m_piece");
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                allAreas = null;
                netView = null;
                pieceOf = null;
                error = exception.Message;
                return false;
            }
        }

        internal static bool Available => allAreas != null;

        // Turns the locator on or off and returns how many wards are in range right now.
        internal static int Toggle()
        {
            Active = !Active && Available;
            if (!Active)
            {
                Clear();
                return 0;
            }
            Scan();
            nextScan = Time.time + ScanInterval;
            return found;
        }

        internal static void Update()
        {
            if (!Active) return;
            if (Time.time < nextScan) return;
            nextScan = Time.time + ScanInterval;
            Scan();
        }

        internal static void OnGUI()
        {
            if (!Active || count == 0 || Event.current.type != EventType.Repaint || !CanDraw()) return;
            Camera camera = Utils.GetMainCamera();
            if (!camera) return;
            EnsureResources();

            float width = Screen.width;
            float height = Screen.height;
            float uiScale = Mathf.Clamp(height / 1080f, 0.75f, 2.5f);
            int fontSize = Mathf.RoundToInt(13f * uiScale);
            if (fontSize != labelFontSize)
            {
                labelFontSize = fontSize;
                labelStyle.fontSize = fontSize;
            }

            Color savedColor = GUI.color;
            Matrix4x4 savedMatrix = GUI.matrix;
            float maxRange = range.Value;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
            // Farthest first, so nearer markers draw on top.
            for (int i = count - 1; i >= 0; i--)
            {
                Target target = targets[i];
                // Viewport coordinates, not screen pixels: with a render scale below 100 % the game renders the main
                // camera into a smaller texture (UpscaledFrameBuffer), and WorldToScreenPoint would use its size.
                Vector3 view = camera.WorldToViewportPoint(target.Anchor);
                MarkerPlacement place = LocatorMath.Place(view.x * width, view.y * height, view.z, width, height, EdgeMargin * uiScale);
                float scale = LocatorMath.MarkerScale(target.Distance, maxRange) * (place.OnScreen ? 1f : 0.85f);
                float alpha = LocatorMath.MarkerAlpha(target.Distance, maxRange);
                bool blocking = target.Inside && target.Enabled && !target.Own;
                if (blocking)
                {
                    // You stand inside this ward's area: let it breathe so it is easy to pick out.
                    scale *= 1f + 0.12f * pulse;
                    alpha = Mathf.Lerp(alpha, 1f, pulse);
                }
                DrawMarker(target, place, BaseMarkerSize * uiScale * scale, alpha, uiScale);
            }
            GUI.matrix = savedMatrix;
            GUI.color = savedColor;
        }

        internal static void Dispose()
        {
            Active = false;
            Clear();
            iconCoords.Clear();
            DestroyTexture(ref disc);
            DestroyTexture(ref dot);
            DestroyTexture(ref ring);
            DestroyTexture(ref arrow);
            labelStyle = null;
            labelContent = null;
            labelFontSize = 0;
        }

        private static void Scan()
        {
            count = 0;
            found = 0;
            Player player = Player.m_localPlayer;
            List<PrivateArea> areas = allAreas != null ? allAreas() : null;
            if (!player || areas == null)
            {
                ReleaseUnused();
                return;
            }

            Vector3 origin = player.transform.position;
            float maxRange = range.Value;
            for (int i = 0; i < areas.Count; i++)
            {
                PrivateArea area = areas[i];
                if (!area || area.m_ownerFaction != Character.Faction.Players) continue;
                ZNetView nview = netView(area);
                if (!nview || !nview.IsValid()) continue;
                Vector3 position = area.transform.position;
                float distance = Vector3.Distance(origin, position);
                if (!(distance <= maxRange)) continue;

                ZDO zdo = nview.GetZDO();
                Piece piece = pieceOf(area);
                Target target = Next();
                target.Area = area;
                target.Position = position;
                // The visual centre of the stone, so the pin points at the ward itself from every camera angle.
                MeshRenderer model = area.m_model;
                target.Anchor = model ? model.bounds.center : position + Vector3.up * FallbackAnchorHeight;
                target.Distance = distance;
                target.Enabled = zdo.GetBool(ZDOVars.s_enabled);
                target.Own = piece && piece.IsCreator();
                target.Icon = piece ? piece.m_icon : null;
                target.Inside = Utils.DistanceXZ(position, origin) < area.m_radius;

                string owner = zdo.GetString(ZDOVars.s_creatorName);
                int meters = LocatorMath.Meters(distance);
                if (meters != target.Meters || !string.Equals(owner, target.Owner, StringComparison.Ordinal))
                {
                    target.Owner = owner;
                    target.Meters = meters;
                    string shown = CensorShittyWords.FilterUGC(owner, UGCType.CharacterName, piece ? piece.GetCreator() : 0L);
                    target.Label = LocatorMath.Label(shown, meters);
                    target.DistanceText = LocatorMath.Label(null, meters);
                }
            }

            found = count;
            targets.Sort(0, count, ByDistance.Instance);
            count = Math.Min(count, maxMarkers.Value);
            ReleaseUnused();

            if (!showRadius.Value) return;
            float ringRange = radiusRange.Value;
            for (int i = 0; i < count; i++)
            {
                Target target = targets[i];
                // ShowAreaMarker keeps the ring for 0.5 s; the scan repeats every 0.25 s.
                if (target.Enabled && target.Distance <= ringRange) target.Area.ShowAreaMarker();
            }
        }

        private static Target Next()
        {
            if (count == targets.Count) targets.Add(new Target());
            return targets[count++];
        }

        // Drop references to wards that are no longer shown, so destroyed objects are not kept alive.
        private static void ReleaseUnused()
        {
            for (int i = count; i < targets.Count; i++)
            {
                targets[i].Area = null;
                targets[i].Icon = null;
            }
        }

        private static void Clear()
        {
            count = 0;
            found = 0;
            ReleaseUnused();
        }

        private static bool CanDraw()
        {
            return Player.m_localPlayer && !Hud.IsUserHidden() && !Menu.IsVisible() && !Minimap.IsOpen() &&
                !InventoryGui.IsVisible();
        }

        private static void DrawMarker(Target target, MarkerPlacement place, float size, float alpha, float uiScale)
        {
            Color tint = target.Own ? OwnActive : target.Enabled ? ForeignActive : Inactive;
            if (!target.Enabled) alpha *= 0.8f;
            // Whole pixels keep dot, stem, bubble and label moving together without shimmering against each other.
            Vector2 center = new Vector2(Mathf.Round(place.X), Mathf.Round(place.Y));

            if (place.OnScreen)
            {
                // Pin: the dot marks the exact spot, the bubble floats a fixed distance above it.
                float stem = Mathf.Round(10f * uiScale);
                Vector2 bubble = new Vector2(center.x, center.y - stem - size * 0.5f);
                float stemTop = bubble.y + size * 0.4f;
                float outline = Mathf.Max(3f, Mathf.Round(3f * uiScale));
                float core = Mathf.Max(1f, Mathf.Round(1.5f * uiScale));
                GUI.color = new Color(0.04f, 0.03f, 0.02f, alpha * 0.75f);
                GUI.DrawTexture(new Rect(center.x - outline * 0.5f, stemTop, outline, center.y - stemTop), Texture2D.whiteTexture);
                GUI.color = new Color(tint.r, tint.g, tint.b, alpha);
                GUI.DrawTexture(new Rect(center.x - core * 0.5f, stemTop, core, center.y - stemTop), Texture2D.whiteTexture);

                DrawBubble(target, bubble, size, tint, alpha);

                float dotSize = Mathf.Max(7f, Mathf.Round(9f * uiScale));
                GUI.color = new Color(0.04f, 0.03f, 0.02f, Mathf.Min(1f, alpha * 1.1f));
                GUI.DrawTexture(Square(center, dotSize), dot);
                GUI.color = new Color(Mathf.Lerp(tint.r, 1f, 0.35f), Mathf.Lerp(tint.g, 1f, 0.35f), Mathf.Lerp(tint.b, 1f, 0.35f), 1f);
                GUI.DrawTexture(Square(center, dotSize * 0.55f), dot);

                DrawLabel(target.Label, new Vector2(center.x, center.y + dotSize * 0.5f + 3f * uiScale), alpha, uiScale, false);
                return;
            }

            DrawBubble(target, center, size, tint, alpha);
            float radians = place.Angle * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            Vector2 arrowCenter = center + direction * (size * 0.72f);
            GUI.color = new Color(tint.r, tint.g, tint.b, alpha);
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(place.Angle, arrowCenter);
            GUI.DrawTexture(Square(arrowCenter, size * 0.55f), arrow);
            GUI.matrix = saved;

            // Off-screen: only the distance, placed on the side facing the screen center.
            Vector2 labelCenter = center - direction * (size * 0.5f + 12f * uiScale);
            DrawLabel(target.DistanceText, labelCenter, alpha, uiScale, true);
        }

        private static void DrawBubble(Target target, Vector2 center, float size, Color tint, float alpha)
        {
            GUI.color = new Color(tint.r, tint.g, tint.b, alpha * 0.5f);
            GUI.DrawTexture(Square(center, size * 1.6f), disc);
            GUI.color = new Color(0.04f, 0.03f, 0.02f, alpha * 0.75f);
            GUI.DrawTexture(Square(center, size * 1.05f), disc);
            GUI.color = new Color(tint.r, tint.g, tint.b, alpha);
            GUI.DrawTexture(Square(center, size), ring);

            if (target.Icon && TryGetIconCoords(target.Icon, out Rect coords))
            {
                float shade = target.Enabled ? 1f : 0.7f;
                GUI.color = new Color(shade, shade, shade, alpha);
                GUI.DrawTextureWithTexCoords(Square(center, size * 0.62f), target.Icon.texture, coords);
            }
        }

        private static void DrawLabel(string text, Vector2 anchor, float alpha, float uiScale, bool centered)
        {
            labelContent.text = text;
            Vector2 extent = labelStyle.CalcSize(labelContent);
            float x = anchor.x - extent.x * 0.5f;
            float y = centered ? anchor.y - extent.y * 0.5f : anchor.y;
            var rect = new Rect(Mathf.Round(x), Mathf.Round(y), extent.x, extent.y);
            float offset = Mathf.Max(1f, Mathf.Round(uiScale));
            GUI.color = new Color(0f, 0f, 0f, alpha * 0.85f);
            GUI.Label(new Rect(rect.x + offset, rect.y + offset, rect.width, rect.height), labelContent, labelStyle);
            GUI.color = new Color(1f, 0.96f, 0.88f, alpha);
            GUI.Label(rect, labelContent, labelStyle);
        }

        private static bool TryGetIconCoords(Sprite sprite, out Rect coords)
        {
            if (iconCoords.TryGetValue(sprite, out coords)) return coords.width > 0f;
            coords = new Rect(0f, 0f, 0f, 0f);
            try
            {
                // Sprite UVs are already in the (possibly atlased) texture, whatever the packing mode.
                Vector2[] uv = sprite.uv;
                if (uv != null && uv.Length > 0 && sprite.texture)
                {
                    float minX = uv[0].x, minY = uv[0].y, maxX = uv[0].x, maxY = uv[0].y;
                    for (int i = 1; i < uv.Length; i++)
                    {
                        minX = Mathf.Min(minX, uv[i].x);
                        minY = Mathf.Min(minY, uv[i].y);
                        maxX = Mathf.Max(maxX, uv[i].x);
                        maxY = Mathf.Max(maxY, uv[i].y);
                    }
                    coords = Rect.MinMaxRect(minX, minY, maxX, maxY);
                }
            }
            catch (Exception)
            {
                // Unreadable sprite: draw the marker without its icon.
            }
            iconCoords[sprite] = coords;
            return coords.width > 0f;
        }

        private static Rect Square(Vector2 center, float size) => new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);

        private static void EnsureResources()
        {
            if (!disc) disc = MakeTexture("WardLocatorDisc", LocatorMath.DiscAlpha);
            if (!dot) dot = MakeTexture("WardLocatorDot", LocatorMath.DotAlpha);
            if (!ring) ring = MakeTexture("WardLocatorRing", LocatorMath.RingAlpha);
            if (!arrow) arrow = MakeTexture("WardLocatorArrow", LocatorMath.ArrowAlpha);
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperCenter,
                    fontStyle = FontStyle.Bold,
                    richText = false,
                    wordWrap = false,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                };
                labelStyle.normal.textColor = Color.white;
                labelContent = new GUIContent(string.Empty);
                labelFontSize = 0;
            }
        }

        private static Texture2D MakeTexture(string name, Func<int, int, int, float> alpha)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
                for (int x = 0; x < TextureSize; x++)
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha(x, y, TextureSize) * 255f));
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture) UnityEngine.Object.Destroy(texture);
            texture = null;
        }
    }
}
