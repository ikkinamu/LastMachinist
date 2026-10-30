using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LastMachinist.Save
{
    // One store owns the legacy manual slot and both rolling auto generations.
    public sealed class SaveGameStore
    {
        private const string SlotFile = "last_machinist_slot.json";
        private readonly string slotPath;
        public SaveGameStore(string slotPath = null) { this.slotPath = slotPath; }
        public string SlotPath => slotPath ?? Path.Combine(Application.persistentDataPath, SlotFile);
        public string AutoPath => SlotPath + ".auto0";
        public string AutoBackupPath => SlotPath + ".auto1";

        public void Save(SaveGameData data) => Write(data, SlotPath);
        public void SaveAuto(SaveGameData data)
        {
            string staged = Stage(data, AutoPath);
            try
            {
                // A corrupt current auto must never replace the last good backup.
                if (TryRead(AutoPath, out _))
                {
                    string backupStage = AutoBackupPath + ".tmp";
                    try { File.Copy(AutoPath, backupStage, true); Promote(backupStage, AutoBackupPath); }
                    finally { if (File.Exists(backupStage)) File.Delete(backupStage); }
                }
                Promote(staged, AutoPath);
            }
            finally { if (File.Exists(staged)) File.Delete(staged); }
        }
        private static void Write(SaveGameData data, string path)
        {
            string staged = Stage(data, path);
            try { Promote(staged, path); }
            finally { if (File.Exists(staged)) File.Delete(staged); }
        }
        private static string Stage(SaveGameData data, string path)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            data.Validate();
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string staged = path + ".tmp";
            try
            {
                File.WriteAllText(staged, JsonUtility.ToJson(data, true));
                if (!TryRead(staged, out _)) throw new InvalidDataException("Staged save is invalid.");
                return staged;
            }
            catch { if (File.Exists(staged)) File.Delete(staged); throw; }
        }
        private static void Promote(string staged, string destination)
        {
            if (File.Exists(destination)) File.Replace(staged, destination, null);
            else File.Move(staged, destination);
        }
        private static bool TryRead(string path, out SaveGameData data)
        {
            data = null;
            if (!File.Exists(path)) return false;
            try
            {
                var candidate = JsonUtility.FromJson<SaveGameData>(File.ReadAllText(path));
                candidate?.Validate();
                if (candidate == null) return false;
                // Validate reconstructability without mutating the returned DTO.
                SaveGameComposer.Restore(JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(candidate)));
                data = candidate;
                return true;
            }
            catch (Exception) { return false; }
        }
        public SaveGameData Load()
        {
            if (!File.Exists(SlotPath)) return null;
            if (!TryRead(SlotPath, out var data)) throw new InvalidDataException("Manual save is invalid.");
            return data;
        }
        public SaveGameData LoadLatestValid()
        {
            foreach (var path in Candidates()) if (TryRead(path, out var data)) return data;
            return null;
        }
        public bool HasValidSave() => Candidates().Any(path => TryRead(path, out _));
        public bool Exists() => File.Exists(SlotPath);
        public bool HasAnySave() => Candidates().Any();
        private IEnumerable<string> Candidates()
            => new[] { SlotPath, AutoPath, AutoBackupPath }.Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc).ThenBy(path => path, StringComparer.Ordinal);
        public void Delete()
        {
            foreach (var path in new[] { SlotPath, AutoPath, AutoBackupPath }) if (File.Exists(path)) File.Delete(path);
        }
    }
}
