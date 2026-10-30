using System;
using System.Collections.Generic;
using LastMachinist.Inventory;
using LastMachinist.Machining.Core;
using LastMachinist.Measurement;
using LastMachinist.Quest;

namespace LastMachinist.Save
{
    // Serializable state only; scene/controller reconstruction belongs to the composition layer.
    [Serializable]
    public sealed class SaveGameData
    {
        public int SaveVersion = 1;
        public InventoryState Inventory = new InventoryState();
        public List<WorkpieceState> WorkpieceInstances = new List<WorkpieceState>();
        public QuestRuntimeState Quest = new QuestRuntimeState();
        public List<RecordedMeasurement> MeasurementRecords = new List<RecordedMeasurement>();
        public string CurrentSceneId;
        public string CurrentWorkpieceInstanceId;
        public bool PumpRepaired;
        public RepeatableQuestState RepeatableQuests;
        public LastMachinist.Core.RegionProgressionState Regions;
        public WorkshopStoryState WorkshopStory;
        public string ActiveMachineId;

        public void Validate()
        {
            if (SaveVersion != 1 || Inventory == null || WorkpieceInstances == null || Quest == null || MeasurementRecords == null)
                throw new ArgumentException("Unsupported or incomplete save data.");
            Inventory.Validate();
            if (!Enum.IsDefined(typeof(LastMachinist.Core.PrologueStatus), Quest.Prologue))
                throw new ArgumentException("Invalid prologue state.");
            Regions?.Validate();
            WorkshopStory?.Validate();
            RepeatableQuestService.Validate(RepeatableQuests, Inventory);
            if (!Enum.IsDefined(typeof(QuestState), Quest.State) || Quest.QuestId != "quest_pump_01")
                throw new ArgumentException("Invalid quest state.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var workpiece in WorkpieceInstances)
            {
                if (workpiece == null) throw new ArgumentException("Null workpiece.");
                workpiece.Validate();
                if (!ids.Add(workpiece.InstanceId)) throw new ArgumentException("Duplicate workpiece instance.");
            }
            foreach (var owned in Inventory.Workpieces) ids.Add(owned.InstanceId);
            if (!string.IsNullOrEmpty(CurrentWorkpieceInstanceId) && Inventory.FindWorkpiece(CurrentWorkpieceInstanceId)?.MachiningWorkpiece == null)
                throw new ArgumentException("Missing current inventory workpiece.");
            foreach (var record in MeasurementRecords)
                if (record == null || !ids.Contains(record.WorkpieceInstanceId) || string.IsNullOrWhiteSpace(record.MeasurementPointId) ||
                    float.IsNaN(record.PlayerValue) || float.IsInfinity(record.PlayerValue))
                    throw new ArgumentException("Invalid saved measurement.");
        }
    }
}
