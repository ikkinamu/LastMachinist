using System.IO;
using UnityEngine;

namespace LastMachinist.Save
{
    public sealed class SaveGameStore
    {
        private const string SlotFile = "last_machinist_slot.json";
        private readonly string slotPath;
        public SaveGameStore(string slotPath = null) { this.slotPath = slotPath; }
        public string SlotPath => slotPath ?? Path.Combine(Application.persistentDataPath, SlotFile);
        public void Save(SaveGameData data)
        {
            if (data == null) throw new System.ArgumentNullException(nameof(data));
            data.Validate();
            string directory = Path.GetDirectoryName(SlotPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string temporary = SlotPath + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
                if (File.Exists(SlotPath)) File.Replace(temporary, SlotPath, null);
                else File.Move(temporary, SlotPath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public SaveGameData Load()
        {
            if (!File.Exists(SlotPath)) return null;
            var data = JsonUtility.FromJson<SaveGameData>(File.ReadAllText(SlotPath));
            data?.Validate();
            return data;
        }
        public bool Exists() => File.Exists(SlotPath);
        public void Delete() { if (File.Exists(SlotPath)) File.Delete(SlotPath); }
    }
}
