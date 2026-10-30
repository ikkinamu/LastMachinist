using UnityEngine;

namespace LastMachinist.Core
{
    [CreateAssetMenu(menuName = "Last Machinist/Exploration Region")]
    public sealed class ExplorationRegionDefinition : ScriptableObject
    {
        public string RegionId;
        public string DisplayName;
        public string SceneName;
        public bool IsUnlocked = true;
        [TextArea] public string Description;
        public Vector2 MapPosition = new Vector2(0.5f, 0.5f);
        public RegionDiscoveryState InitialDiscoveryState = RegionDiscoveryState.Discovered;
        public string RumoredName = "미확인 장소";
        [TextArea] public string[] RumoredInformation = System.Array.Empty<string>();
        [TextArea] public string[] KnownInformation = System.Array.Empty<string>();
        public string[] RelatedQuestIds = System.Array.Empty<string>();
        public MajorDiscoveryDefinition[] MajorDiscoveries = System.Array.Empty<MajorDiscoveryDefinition>();
        public string RevealAfterQuestId;
        public string InaccessibleReason = "현재 진입로를 사용할 수 없습니다.";
        public Texture2D MapIcon;
        public GameObject EnvironmentPrefab;
        public Vector3 SpawnPosition = new Vector3(0, 1, -8);
        public float MoveSpeed = 4;
        public float MouseSensitivity = 0.12f;
        [Min(0)] public float InteractionRange = 3;
        public ExplorationItemPlacement[] Items = System.Array.Empty<ExplorationItemPlacement>();
        public bool CanEnter => IsUnlocked && !string.IsNullOrWhiteSpace(RegionId) && !string.IsNullOrWhiteSpace(SceneName);
    }
}
