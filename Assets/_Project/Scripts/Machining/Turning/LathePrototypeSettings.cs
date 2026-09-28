using UnityEngine;

namespace LastMachinist.Machining.Turning
{
    [CreateAssetMenu(menuName = "Last Machinist/Lathe Prototype Settings")]
    public sealed class LathePrototypeSettings : ScriptableObject
    {
        [Header("Temporary playtest values — not final design constants")]
        [Min(1)] public float StockLength = 100;
        [Min(1)] public float StockDiameter = 50;
        [Min(0)] public float GripLength = 15;
        [Min(0.001f)] public float BaseStep = 0.05f;
        public float InitialRadialPosition = 30;
        public float InitialZPosition = 80;
        [Min(1)] public float WheelUnitsPerStep = 120;
        [Min(1)] public float DisplayRotationSpeed = 120;
        public RoughingToolDefinition RoughingTool;
        public ChamferToolDefinition ChamferTool;
        public Material StockMaterial;
        public Material ToolMaterial;
        public Material MachineMaterial;
    }
}
