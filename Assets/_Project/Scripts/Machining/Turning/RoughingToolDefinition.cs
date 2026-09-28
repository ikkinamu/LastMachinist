using System;
using UnityEngine;

namespace LastMachinist.Machining.Turning
{
    [CreateAssetMenu(menuName = "Last Machinist/Roughing Tool")]
    public sealed class RoughingToolDefinition : ScriptableObject
    {
        public string Id = "roughing_tool";
        public string DisplayName = "Roughing tool";
        public bool CanCut = true;
        [Tooltip("Placeholder gameplay cutting width in mm, not a physical tool model.")]
        [Min(0.01f)] public float Width = 2f;
        public RoughingToolState CreateState() => new RoughingToolState(Id, DisplayName, CanCut, Width);
    }

    [Serializable]
    public sealed class RoughingToolState
    {
        public string Id { get; }
        public string DisplayName { get; }
        public bool CanCut { get; }
        public float Width { get; }
        public float EdgeStart => -Width/2;
        public float EdgeEnd => Width/2;
        public RoughingToolState(string id, string displayName, bool canCut, float width)
        {
            if (string.IsNullOrWhiteSpace(id) || float.IsNaN(width) || float.IsInfinity(width) || width <= 0)
                throw new ArgumentException("Invalid roughing tool definition.");
            Id = id; DisplayName = displayName; CanCut = canCut; Width = width;
        }
    }
}
