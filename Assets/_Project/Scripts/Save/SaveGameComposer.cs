using System;
using LastMachinist.Integration;

namespace LastMachinist.Save
{
    public static class SaveGameComposer
    {
        public static bool IsCheckpoint(LastMachinist.Quest.QuestState state)
            => state == LastMachinist.Quest.QuestState.Offered ||
               state == LastMachinist.Quest.QuestState.FarmerArrived ||
               state == LastMachinist.Quest.QuestState.ReturnedToWorkshop ||
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
            integration.RestoreStory(data.WorkshopStory);
            if (!string.IsNullOrEmpty(data.CurrentWorkpieceInstanceId))
            {
                integration.AttachWorkpiece(data.Inventory.FindWorkpiece(data.CurrentWorkpieceInstanceId).MachiningWorkpiece);
                integration.Measurement.ImportRecords(data.MeasurementRecords);
            }
            if (data.WorkpieceInstances.Count > 0)
            {
                var saved = data.WorkpieceInstances[0];
                var owned = data.Inventory.FindWorkpiece(saved.InstanceId);
                if (owned == null && data.Quest.State >= LastMachinist.Quest.QuestState.MaterialSelected && data.Quest.State <= LastMachinist.Quest.QuestState.Installing)
                {
                    var definition = UnityEngine.Resources.Load<LastMachinist.Inventory.WorkpieceDefinition>("WorkpieceDefinitions/RoundBar50");
                    var item = definition.CreateInstance(new LastMachinist.Inventory.ConditionProperties(), "workshop", "legacy-save");
                    item.InstanceId = saved.InstanceId; item.AcquisitionSource = LastMachinist.Inventory.InitialWorkshopWorkpieces.Source;
                    item.MaterialId = saved.MaterialId; item.HasMachiningGeometry = true; item.MachiningWorkpiece = saved.Copy();
                    item.MachiningWorkpiece.SourceItemId = item.WorkpieceDefinitionId;
                    item.MachiningMeasurements = data.MeasurementRecords;
                    if (!data.Inventory.AddWorkpiece(item)) throw new InvalidOperationException("Cannot restore legacy owned workpiece.");
                    owned = data.Inventory.FindWorkpiece(saved.InstanceId);
                }
                if (owned != null && data.Quest.State >= LastMachinist.Quest.QuestState.MaterialSelected && data.Quest.State <= LastMachinist.Quest.QuestState.Installing)
                    data.Quest.WorkpieceInstanceId = owned.InstanceId;
                integration.AttachWorkpiece(owned?.MachiningWorkpiece ?? saved);
                integration.Measurement.ImportRecords(data.MeasurementRecords);
            }
            return integration;
        }
        public static SaveGameData Capture(Quest01Integration integration, string sceneId = null)
        {
            if (integration == null) throw new ArgumentNullException(nameof(integration));
            var data = new SaveGameData { Inventory = integration.Inventory, Quest = integration.Quest.Data,
                CurrentSceneId = sceneId, PumpRepaired = integration.PumpRepaired, RepeatableQuests = integration.RepeatableQuests.Data,
                Regions = integration.Regions.Data, WorkshopStory = integration.Story.Data };
            if (integration.CurrentWorkpiece != null)
            {
                if (integration.Inventory.FindWorkpiece(integration.CurrentWorkpiece.InstanceId) != null)
                    data.CurrentWorkpieceInstanceId = integration.CurrentWorkpiece.InstanceId;
                else data.WorkpieceInstances.Add(integration.CurrentWorkpiece);
            }
            if (integration.Measurement != null) data.MeasurementRecords = integration.Measurement.ExportRecords();
            data.Validate();
            return data;
        }
    }
}
