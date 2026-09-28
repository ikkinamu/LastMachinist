using UnityEngine;

namespace LastMachinist.Machining.Core
{
    [CreateAssetMenu(menuName = "Last Machinist/Material Definition")]
    public sealed class MaterialDefinition : ScriptableObject
    {
        public string Id;
        public string MaterialId => Id;
        public string DisplayName;
        [TextArea] public string Description;
        [Range(0, 100)] public int HardnessRating;
        [Range(0, 100)] public int StrengthRating;
        [Range(0, 100)] public int MachinabilityRating;
        [Range(0, 100)] public int CorrosionResistanceRating;
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id)) throw new System.ArgumentException("Material ID required.");
            foreach (var rating in new[] { HardnessRating, StrengthRating, MachinabilityRating, CorrosionResistanceRating })
                if (rating < 0 || rating > 100) throw new System.ArgumentOutOfRangeException("Material rating must be 0..100.");
        }
    }
}
