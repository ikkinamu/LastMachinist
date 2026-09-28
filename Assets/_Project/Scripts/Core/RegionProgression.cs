using System;
using System.Collections.Generic;
using System.Linq;

namespace LastMachinist.Core
{
    public enum RegionDiscoveryState { Hidden, Rumored, Discovered }
    [Serializable] public sealed class MajorDiscoveryDefinition { public string Id, DisplayName; }
    [Serializable] public sealed class RegionRuntimeState
    {
        public string RegionId;
        public RegionDiscoveryState DiscoveryState;
        public bool IsAccessible;
        public string AccessReason;
        public List<string> KnownInformation = new List<string>();
        public List<string> MajorDiscoveryIds = new List<string>();
    }
    [Serializable] public sealed class RegionProgressionState
    {
        public List<RegionRuntimeState> Regions = new List<RegionRuntimeState>();
        public void Validate()
        {
            if (Regions == null) throw new ArgumentException("Missing region states.");
            var ids = new HashSet<string>();
            foreach (var r in Regions)
            {
                if (r == null || string.IsNullOrWhiteSpace(r.RegionId) || !ids.Add(r.RegionId) ||
                    !Enum.IsDefined(typeof(RegionDiscoveryState), r.DiscoveryState) || r.KnownInformation == null || r.MajorDiscoveryIds == null ||
                    r.KnownInformation.Any(string.IsNullOrWhiteSpace) || r.MajorDiscoveryIds.Any(string.IsNullOrWhiteSpace) ||
                    r.MajorDiscoveryIds.Distinct().Count() != r.MajorDiscoveryIds.Count)
                    throw new ArgumentException("Invalid region progression.");
            }
        }
    }
    // One progression owner; definitions remain static and contain no player's state.
    public sealed class RegionProgression
    {
        public const string PumpQuestId = "quest_pump_01";
        public const string AbandonedWorkshopId = "abandoned_workshop";
        private readonly Dictionary<string, ExplorationRegionDefinition> definitions;
        public RegionProgressionState Data { get; }
        public RegionProgression(IEnumerable<ExplorationRegionDefinition> regions, RegionProgressionState data = null)
        {
            definitions = regions.ToDictionary(r => r.RegionId);
            Data = data ?? new RegionProgressionState(); Data.Validate();
            foreach (var d in definitions.Values)
                if (Find(d.RegionId) == null) Data.Regions.Add(new RegionRuntimeState {
                    RegionId = d.RegionId, DiscoveryState = d.InitialDiscoveryState,
                    IsAccessible = d.IsUnlocked, AccessReason = d.InaccessibleReason });
        }
        public RegionRuntimeState Find(string id) => Data.Regions.Find(r => r.RegionId == id);
        public bool IsVisible(string id) => Find(id) != null && Find(id).DiscoveryState != RegionDiscoveryState.Hidden;
        public bool CanEnter(string id) => IsVisible(id) && Find(id).IsAccessible && definitions.TryGetValue(id, out var d) && !string.IsNullOrWhiteSpace(d.SceneName);
        public bool DiscoverRegion(string id, RegionDiscoveryState level)
        {
            var r = Find(id);
            if (r == null || !definitions.ContainsKey(id) || !Enum.IsDefined(typeof(RegionDiscoveryState), level) || (int)level <= (int)r.DiscoveryState) return false;
            r.DiscoveryState = level; return true;
        }
        public bool SetAccess(string id, bool accessible, string reason = null)
        {
            var r = Find(id); if (r == null) return false;
            r.IsAccessible = accessible; r.AccessReason = accessible ? null : (string.IsNullOrWhiteSpace(reason) ? "현재 접근할 수 없습니다." : reason); return true;
        }
        public bool AddKnownInformation(string id, string information)
        {
            var r = Find(id); if (r == null || string.IsNullOrWhiteSpace(information) || r.KnownInformation.Contains(information)) return false;
            r.KnownInformation.Add(information); return true;
        }
        public bool RecordMajorDiscovery(string id, string discoveryId)
        {
            var r = Find(id);
            if (r == null || !IsVisible(id) || !definitions.TryGetValue(id, out var d) ||
                !d.MajorDiscoveries.Any(m => m.Id == discoveryId) || r.MajorDiscoveryIds.Contains(discoveryId)) return false;
            r.MajorDiscoveryIds.Add(discoveryId); return true;
        }
        public void ApplyQuestCompletion(string questId)
        {
            foreach (var d in definitions.Values.Where(d => d.RevealAfterQuestId == questId))
            {
                DiscoverRegion(d.RegionId, RegionDiscoveryState.Rumored);
                foreach (var info in d.RumoredInformation) AddKnownInformation(d.RegionId, info);
            }
        }
        public string Name(ExplorationRegionDefinition d) => !IsVisible(d.RegionId) ? "" :
            Find(d.RegionId).DiscoveryState == RegionDiscoveryState.Rumored ? d.RumoredName : d.DisplayName;
        public string Information(ExplorationRegionDefinition d)
        {
            if (!IsVisible(d.RegionId)) return "";
            var r = Find(d.RegionId);
            var lines = new List<string> { Name(d), r.IsAccessible ? "접근 가능" : "접근 불가 · " + r.AccessReason };
            if (r.DiscoveryState == RegionDiscoveryState.Discovered) lines.Add(d.Description);
            var info = (r.DiscoveryState == RegionDiscoveryState.Discovered ? d.KnownInformation : d.RumoredInformation)
                .Concat(r.KnownInformation).Distinct().ToArray();
            if (info.Length > 0) lines.Add("\n알려진 정보\n" + string.Join("\n", info));
            var major = d.MajorDiscoveries.Where(m => r.MajorDiscoveryIds.Contains(m.Id)).Select(m => m.DisplayName).ToArray();
            if (major.Length > 0) lines.Add("\n주요 발견\n" + string.Join("\n", major));
            return string.Join("\n", lines);
        }
    }
}
