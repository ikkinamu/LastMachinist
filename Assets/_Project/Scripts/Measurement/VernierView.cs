using UnityEngine;

namespace LastMachinist.Measurement
{
    // Instrument-space coordinates; no player record or geometry mutation lives here.
    public sealed class VernierDisplayModel
    {
        public InstrumentReading Reading { get; }
        public VernierScale Scale { get; }
        public float JawOpening => Reading.Value;
        public float ScaleWindowStart => Mathf.Max(0, Reading.MainScaleValue - 2);
        public VernierDisplayModel(InstrumentReading reading)
        {
            Reading = reading ?? throw new System.ArgumentNullException(nameof(reading));
            Scale = new VernierScale(reading.VernierDivisions);
        }
        public float TickPosition(int index) => Scale.TickPosition(Reading, index);
    }

    public static class VernierView
    {
        public static readonly Rect ScaleBounds = new Rect(400, 360, 800, 270);
        public static void Draw(InstrumentReading reading)
        {
            var model = new VernierDisplayModel(reading);
            const float left = 425, width = 745, mainY = 472;
            float pixels = width / (model.Scale.Divisions + 4);
            var numbers = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter };
            Fill(ScaleBounds, new Color(0.10f, 0.13f, 0.16f));
            GUI.Label(new Rect(left, 370, width, 30), "확대 눈금 · 본척과 부척을 직접 읽으세요", numbers);
            Fill(new Rect(left, 420, width, 54), new Color(0.73f, 0.77f, 0.78f));
            var ink = new Color(0.07f, 0.1f, 0.12f);
            for (int i = 0; i <= model.Scale.Divisions + 4; i++)
            {
                float x = left + pixels * i;
                Fill(new Rect(x, mainY - (i % 5 == 0 ? 25 : 18), 1.4f, i % 5 == 0 ? 25 : 18), ink);
                var old = GUI.color; GUI.color = ink;
                GUI.Label(new Rect(x - 19, 420, 38, 24), (model.ScaleWindowStart + i).ToString("0"), numbers);
                GUI.color = old;
            }
            float zeroX = left + (model.JawOpening - model.ScaleWindowStart) * pixels;
            Fill(new Rect(zeroX - 8, mainY + 2, model.Scale.Divisions * model.Scale.Pitch * pixels + 16, 63), new Color(0.50f, 0.58f, 0.61f));
            for (int i = 0; i <= model.Scale.Divisions; i++)
            {
                float x = left + (model.TickPosition(i) - model.ScaleWindowStart) * pixels;
                Fill(new Rect(x, mainY + 2, 1.4f, 27), ink);
                if (i % 2 == 0)
                {
                    var old = GUI.color; GUI.color = ink;
                    GUI.Label(new Rect(x - 18, mainY + 31, 36, 24), i.ToString(), numbers);
                    GUI.color = old;
                }
            }
            GUI.Label(new Rect(left, 550, width, 27), "본척: 1 mm  ·  부척 한 칸: " + model.Scale.Resolution.ToString("0.##") + " mm", numbers);
            GUI.Label(new Rect(left, 585, width, 28), "부척 0 왼쪽의 본척 값 + 일치하는 부척 번호 × 한 칸 값", numbers);
        }
        private static void Fill(Rect rect, Color color)
        {
            var old = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        }
    }
}
