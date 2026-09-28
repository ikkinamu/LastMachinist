using UnityEngine;
using UnityEngine.InputSystem;
using LastMachinist.Core;

namespace LastMachinist.Save
{
    // Development-only access to checkpoints and state inspection; hidden in non-development builds.
    public sealed class DevelopmentDebugPanel : MonoBehaviour
    {
        public SaveGameData Data;
        public System.Func<SaveGameData> ReadCurrent;
        public System.Action SaveCurrent, LoadCurrent;
        public System.Action AddStock, ForceVernier, ForceInference, TogglePump;
        private SaveGameStore store;
        private bool open;
        private void Awake() { store = new SaveGameStore(); }
        private void Update() { if (Debug.isDebugBuild && Keyboard.current != null && Keyboard.current.f12Key.wasPressedThisFrame) open = !open; }
        private void OnGUI()
        {
            if (!Debug.isDebugBuild || !open) return;
            if (ReadCurrent != null) Data = ReadCurrent();
            if (Data == null) return;
            GUILayout.BeginArea(new Rect(12, 12, 300, 360), GUI.skin.window);
            GUILayout.Label("개발 패널 (F12)");
            GUILayout.Label("퀘스트: " + (Data.Quest?.State.ToString() ?? "없음"));
            GUILayout.Label("소재: " + (Data.Quest?.StockCount.ToString() ?? "0"));
            if (GUILayout.Button("저장")) { if (SaveCurrent != null) SaveCurrent(); else store.Save(Data); }
            if (GUILayout.Button("불러오기") && store.Exists()) { if (LoadCurrent != null) LoadCurrent(); else Data = store.Load(); }
            if (GUILayout.Button("소재 1개 추가")) { if (AddStock != null) AddStock(); else Data.Inventory.Add(DefinitionIds.StructuralCarbonSteel, 1); }
            if (GUILayout.Button("버니어 강제 획득")) { if (ForceVernier != null) ForceVernier(); else Data.Quest.HasReceivedVernier = true; }
            if (GUILayout.Button("형상 추론 강제 완료")) { if (ForceInference != null) ForceInference(); else Data.Quest.PumpShaftInferred = true; }
            if (GUILayout.Button("펌프 수리 상태 전환")) { if (TogglePump != null) TogglePump(); else { Data.PumpRepaired = !Data.PumpRepaired; Data.Quest.PumpRepaired = Data.PumpRepaired; } }
            if (GUILayout.Button("저장 삭제")) store.Delete();
            GUILayout.EndArea();
        }
    }
}
