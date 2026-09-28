using System;
using UnityEngine;

namespace LastMachinist.Machining.Turning
{
    [CreateAssetMenu(menuName = "Last Machinist/Chamfer Tool")]
    public sealed class ChamferToolDefinition : ScriptableObject
    {
        public string Id = "chamfer_tool";
        public string DisplayName = "Chamfer tool";
        [Min(0.01f)] public float DefaultWidth = 1f;
        public ChamferToolState CreateState() => new ChamferToolState(Id, DisplayName, DefaultWidth);
    }

    [Serializable]
    public sealed class ChamferToolState
    {
        public string Id { get; }
        public string DisplayName { get; }
        public float DefaultWidth { get; }
        // Axial and radial reach of the finite 45-degree edge, in profile millimetres.
        // DefaultWidth remains for compatibility with existing tool assets, not cut depth.
        public const float CuttingEdgeReach = 10f;
        public ChamferToolState(string id, string displayName, float defaultWidth)
        {
            if (string.IsNullOrWhiteSpace(id) || defaultWidth <= 0 || float.IsNaN(defaultWidth) || float.IsInfinity(defaultWidth))
                throw new ArgumentException("Invalid chamfer tool definition.");
            Id = id; DisplayName = displayName; DefaultWidth = defaultWidth;
        }
    }
}
