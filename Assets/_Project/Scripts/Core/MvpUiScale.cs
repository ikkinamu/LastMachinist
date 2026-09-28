using UnityEngine;

namespace LastMachinist.Core
{
    // IMGUI equivalent of anchored containers, in the shared 1280x800 space.
    public static class MvpPanelLayout
    {
        public const float Margin = 16, Gap = 8, ButtonHeight = 42;
        public static readonly Rect SessionActions = new Rect(990, 15, 274, 30);
        public static readonly Rect Page = new Rect(384, 96, 880, 692);
        public static Rect Close(Rect panel) => new Rect(panel.xMax - Margin - 84, panel.y + Margin, 84, ButtonHeight);
        public static Rect Actions(Rect panel) => new Rect(panel.x + Margin, panel.yMax - Margin - ButtonHeight, panel.width - Margin * 2, ButtonHeight);
        public static Rect Cell(Rect row, int index, int count)
        {
            float width = (row.width - Gap * (count - 1)) / count;
            return new Rect(row.x + index * (width + Gap), row.y, width, row.height);
        }
        public static Rect QuestList => new Rect(Page.x + Margin, Page.y + 80, 240, Page.height - 160);
        public static Rect QuestDetail => new Rect(QuestList.xMax + Gap, QuestList.y, Page.xMax - Margin - QuestList.xMax - Gap, QuestList.height);
        public static Rect QuestAccept => Cell(Actions(Page), 2, 3);
        public static int ButtonRow(string[] labels, GUIStyle style)
        {
            int clicked = -1;
            GUILayout.BeginHorizontal();
            for (int i = 0; i < labels.Length; i++)
            {
                if (i > 0) GUILayout.Space(Gap);
                if (GUILayout.Button(labels[i], style, GUILayout.ExpandWidth(true))) clicked = i;
            }
            GUILayout.EndHorizontal();
            return clicked;
        }
    }
    // Keeps the 1280x800 authored layout proportional on every window size.
    // Letterboxing is intentional: it prevents character art and dialogue boxes
    // from being stretched when the game runs at 16:9, 16:10, or 4:3.
    public static class MvpUiScale
    {
        public static void DrawOpaqueBackground(Rect area)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.025f, 0.035f, 0.05f, 1f);
            // Explicit opaque pixels; GUI.Box skin backgrounds can be translucent.
            GUI.DrawTexture(area, Texture2D.whiteTexture, ScaleMode.StretchToFill, false);
            GUI.color = previous;
        }

        public static void DrawPageBackground(bool showStation)
        {
            if (!showStation)
            {
                DrawOpaqueBackground(new Rect(0, 0, 1280, 800));
                return;
            }
            DrawOpaqueBackground(new Rect(0, 0, 384, 800));
            DrawOpaqueBackground(new Rect(384, 0, 896, 120));
            DrawOpaqueBackground(new Rect(384, 640, 896, 160));
        }

        public static Matrix4x4 Apply1280x800()
        {
            Rect content = ContentRect(Screen.width, Screen.height);
            float scale = content.width / 1280f;
            return Matrix4x4.TRS(new Vector3(content.x, content.y, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
        }

        public static Rect ContentRect(float width, float height)
        {
            float scale = Mathf.Min(Mathf.Max(1, width) / 1280f, Mathf.Max(1, height) / 800f);
            return new Rect((width - 1280 * scale) / 2, (height - 800 * scale) / 2, 1280 * scale, 800 * scale);
        }

        // Authored GUI coordinates use a top-left origin; camera/input pixels use bottom-left.
        public static Rect ToScreenRect(Rect layout, float width, float height)
        {
            Rect content = ContentRect(width, height);
            float scale = content.width / 1280f;
            return new Rect(content.x + layout.x * scale,
                height - content.y - layout.yMax * scale, layout.width * scale, layout.height * scale);
        }

        public static void DrawLetterbox()
        {
            var matrix = GUI.matrix;
            var color = GUI.color;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.black;
            Rect content = ContentRect(Screen.width, Screen.height);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, content.y), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, content.yMax, Screen.width, Screen.height - content.yMax), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, 0, content.x, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(content.xMax, 0, Screen.width - content.xMax, Screen.height), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.matrix = matrix;
        }
    }
}
