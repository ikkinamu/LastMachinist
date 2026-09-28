using UnityEngine;

namespace LastMachinist.Core
{
    [CreateAssetMenu(menuName = "Last Machinist/Exploration Map")]
    public sealed class ExplorationMapDefinition : ScriptableObject
    {
        [Tooltip("External map art only. Names and progression markers belong to the UI.")]
        public Texture2D BackgroundImage;
        public float PlaceholderAspect = 1;
    }
    public static class ExplorationMapLayout
    {
        public static readonly Rect MapArea = new Rect(404, 144, 490, 570);
        public static readonly Rect InfoArea = new Rect(910, 128, 330, 576);
        public static readonly Rect StartButton = new Rect(926, 718, 298, 48);
        public static Rect ImageRect(Rect area, float aspect)
        {
            aspect = Mathf.Max(.01f, aspect);
            float w = Mathf.Min(area.width, area.height * aspect), h = w / aspect;
            return new Rect(area.center.x-w/2, area.center.y-h/2, w, h);
        }
        // Same image rect for rendering and normalized marker projection; origin is top-left.
        public static Rect Marker(Rect image, Vector2 normalized)
            => new Rect(image.x + Mathf.Clamp01(normalized.x)*image.width-22,
                image.y + Mathf.Clamp01(normalized.y)*image.height-22, 44, 44);
    }
}
