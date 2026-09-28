using UnityEngine;

namespace LastMachinist.Inventory
{
    public enum ItemType { Material, Workpiece, MeasurementTool, CuttingTool, QuestItem, Misc }

    [CreateAssetMenu(menuName = "Last Machinist/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public ItemType ItemType;
        public Sprite Icon;
        public bool Stackable;
        [Min(1)] public int MaxStack = 1;
    }
}
