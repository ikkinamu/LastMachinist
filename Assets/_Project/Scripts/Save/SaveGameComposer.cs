using System;
using LastMachinist.Integration;

namespace LastMachinist.Save
{
    public static class SaveGameComposer
    {
        public static bool IsCheckpoint(LastMachinist.Quest.QuestState state)
            => state == LastMachinist.Quest.QuestState.Offered ||
               state == LastMachinist.Quest.QuestState.FarmerArrived ||
               state == LastMachinist.Quest.QuestState.VernierReceived ||
               state == LastMachinist.Quest.QuestState.MaterialSelected ||
               state == LastMachinist.Quest.QuestState.ReadyToDeliver ||
               state == LastMachinist.Quest.QuestState.Completed;
        public static Quest01Integration Restore(SaveGameData data, Func<LastMachinist.Machining.Core.WorkpieceState> createStock = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            data.Validate();
            // Restore the cut geometry, but require an explicit new mount after loading.
            // No stale reservation can strand an owned workpiece after process restart.
            data.Inventory.ReleaseMachining();
            var integration = new Quest01Integration(data.Inventory, data.Quest, createStock);
            integration.RestoreRepeatableQuests(data.RepeatableQuests);
            integration.RestoreRegions(data.Regions);
            if (data.WorkpieceInstances.Count > 0)
            {
                integration.AttachWorkpiece(data.WorkpieceInstances[0]);
                integration.Measurement.ImportRecords(data.MeasurementRecords);
            }
            return integration;
        }
        public static SaveGameData Capture(Quest01Integration integration, string sceneId = null)
        {
            if (integration == null) throw new ArgumentNullException(nameof(integration));
            var data = new SaveGameData { Inventory = integration.Inventory, Quest = integration.Quest.Data,
                CurrentSceneId = sceneId, PumpRepaired = integration.PumpRepaired, RepeatableQuests = integration.RepeatableQuests.Data,
                Regions = integration.Regions.Data };
            if (integration.CurrentWorkpiece != null) data.WorkpieceInstances.Add(integration.CurrentWorkpiece);
            if (integration.Measurement != null) data.MeasurementRecords = integration.Measurement.ExportRecords();
            data.Validate();
            return data;
        }
    }
}
