using System;
using UnityEngine;
using LastMachinist.Quest;

namespace LastMachinist.Core
{
    // Shared bottom-third dialogue view for intro choices and all world conversations.
    public static class DialoguePage
    {
        public static readonly Rect Bounds = new Rect(0, 536, 1280, 264);
        public static string SpeakerName(string id) => id == "kim_youngho" ? "김영호" : id == "narrator" ? "" : id == "park_seongcheol" ? "박성철" : id == "lee_seoyeon" ? "이서연" : "나";
        public static void Draw(DialogueNode node, Action next, Action<string> choose = null, Texture2D portrait = null)
        {
            if (node == null) return;
            var label = new GUIStyle(GUI.skin.label) { fontSize = 23, wordWrap = true };
            var name = new GUIStyle(label) { fontSize = 26, fontStyle = FontStyle.Bold };
            var button = new GUIStyle(GUI.skin.button) { fontSize = 20, wordWrap = true };
            // v0.6: keep the illustration in a separate character layer so the
            // bottom dialogue panel remains readable and the scene stays visible.
            if (portrait != null)
            {
                var rect = node.SpeakerId == "lee_seoyeon"
                    ? new Rect(570, 35, 740, 740)
                    : new Rect(-50, 35, 740, 740);
                var old = GUI.color; GUI.color = Color.white;
                GUI.DrawTexture(rect, portrait, ScaleMode.ScaleToFit, true);
                GUI.color = old;
            }
            // Draw the panel after the character: the lower body is naturally occluded.
            Fill(Bounds, new Color(0.045f, 0.065f, 0.08f, 1f));
            Fill(new Rect(0, Bounds.y, 1280, 3), new Color(0.73f, 0.56f, 0.32f));
            GUI.Label(new Rect(46, 555, 1130, 38), SpeakerName(node.SpeakerId), name);
            bool choices = choose != null && node.Choices != null && node.Choices.Count > 0;
            GUI.Label(new Rect(46, 605, 1100, 115), node.Text, label);
            if (choices)
            {
                for (int i = 0; i < node.Choices.Count; i++)
                    if (GUI.Button(new Rect(700, Bounds.y - (node.Choices.Count - i) * 57, 535, 48), node.Choices[i].Text, button)) choose(node.Choices[i].Id);
            }
            else if (GUI.Button(MvpPanelLayout.Cell(MvpPanelLayout.Actions(Bounds), 4, 5), "다음 ▶", button)) next?.Invoke();
            var input = Event.current;
            if (GUI.enabled && !choices && next != null && ((input.type == EventType.KeyDown &&
                (input.keyCode == KeyCode.Space || input.keyCode == KeyCode.Return)) ||
                (input.type == EventType.MouseUp && input.button == 0 && Bounds.Contains(input.mousePosition))))
            { input.Use(); next(); }
        }
        private static void Fill(Rect rect, Color color)
        { var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
    }
}
