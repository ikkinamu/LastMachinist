using UnityEngine;
using LastMachinist.Inventory;
using LastMachinist.Integration;
using LastMachinist.Machining.Turning;
using LastMachinist.Quest;
using LastMachinist.Save;

namespace LastMachinist.Core
{
    /// <summary>Scene-local startup composition point for the isolated turning prototype.</summary>
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private LathePrototypeSettings latheSettings;
        [SerializeField] private DialogueCatalogDefinition dialogueDefinition;
        [Header("External NPC dialogue art (v0.6)")]
        [SerializeField] private Texture2D seongcheolDialogueDefault;
        [SerializeField] private Texture2D seoyeonDialogueDefault;
        [Header("External environment art (optional)")]
        [SerializeField] private Texture2D workshopBackground;
        [SerializeField] private Texture2D farmBackground;
        [SerializeField] private Texture2D pumpBroken;
        [SerializeField] private Texture2D pumpRepaired;
        private QuestFlowController questFlow;
        private void BindQuestFlow()
        {
            questFlow?.Dispose();
            questFlow = dialogueDefinition == null ? null : new QuestFlowController(Quest01.Quest, dialogueDefinition.BuildRuntimeCatalog());
            if (overlay != null) overlay.BindDialogueFlow(questFlow);
        }
        public Quest01Integration Quest01 { get; private set; }
        private LathePrototype lathe;
        private MvpWorldPanel world;
        private MvpStartupOverlay overlay;
        private readonly SaveGameStore saveStore = new SaveGameStore();
        private bool checkpointPending;
        private void OnQuestStateChanged(QuestState previous, QuestState current)
        { if (SaveGameComposer.IsCheckpoint(current)) checkpointPending = true; }
        private void LateUpdate()
        {
            if (!checkpointPending || overlay == null || world == null) return;
            // State events may precede stock mounting and location changes in the
            // same command. Capture only after those handlers have returned.
            checkpointPending = false;
            SaveCurrent();
        }
        private void OnDestroy()
        { if (Quest01 != null) Quest01.Quest.StateChanged -= OnQuestStateChanged; questFlow?.Dispose(); }
        private SaveGameData CaptureCurrent()
        {
            lathe.SyncInventoryMeasurements();
            return SaveGameComposer.Capture(Quest01, world.CurrentLocation);
        }
        private void SaveCurrent()
        {
            try { saveStore.Save(CaptureCurrent()); overlay.PersistenceMessage = "저장했습니다."; }
            catch (System.Exception error) { overlay.PersistenceMessage = "저장하지 못했습니다."; Debug.LogException(error); }
        }
        private void LoadCurrent()
        {
            try
            {
                var data = saveStore.Load();
                if (data == null) { overlay.PersistenceMessage = "저장 파일이 없습니다."; return; }
                var restored = SaveGameComposer.Restore(data, lathe.CreateStockWorkpiece);
                lathe.RestoreQuest(restored);
                Quest01.Quest.StateChanged -= OnQuestStateChanged;
                Quest01 = restored;
                Quest01.Quest.StateChanged += OnQuestStateChanged;
                checkpointPending = false;
                BindQuestFlow();
                world.Bind(restored, lathe, questFlow);
                world.RestoreLocation(data.CurrentSceneId);
                overlay.Resume(restored);
                overlay.PersistenceMessage = "불러왔습니다.";
            }
            catch (System.Exception error) { overlay.PersistenceMessage = "불러오지 못했습니다."; Debug.LogException(error); }
        }
        private void Start()
        {
            if (latheSettings == null)
            {
                Debug.LogError("Assign the lathe prototype settings on GameBootstrap.", this);
                return;
            }
            var prototype = new GameObject("Lathe Prototype");
            prototype.transform.SetParent(transform, false);
            lathe = prototype.AddComponent<LathePrototype>();
            lathe.Initialize(latheSettings, Camera.main);
            lathe.enabled = false;
            var inventory = new InventoryState();
            inventory.Add(DefinitionIds.StructuralCarbonSteel, 3);
            Quest01 = new Quest01Integration(inventory, new QuestRuntimeState(), lathe.CreateStockWorkpiece);
            Quest01.Quest.StateChanged += OnQuestStateChanged;
            lathe.BindQuest(Quest01);
            Quest01.AttachWorkpiece(lathe.Controller.Workpiece);
            var debug = gameObject.AddComponent<DevelopmentDebugPanel>();
            debug.ReadCurrent = CaptureCurrent; debug.SaveCurrent = SaveCurrent; debug.LoadCurrent = LoadCurrent;
            debug.AddStock = () => { Quest01.Inventory.Add(Quest01Integration.StockItemId, 1); Quest01.Quest.Data.StockCount = Quest01.Inventory.GetQuantity(Quest01Integration.StockItemId); };
            debug.ForceVernier = () => Quest01.Quest.Data.HasReceivedVernier = true;
            debug.ForceInference = () => { Quest01.Quest.Data.PumpShaftInferred = true; Quest01.Quest.Data.WorkMemo.TargetShapeUnlocked = true; };
            debug.TogglePump = () => Quest01.Quest.Data.PumpRepaired = !Quest01.Quest.Data.PumpRepaired;
            world = gameObject.AddComponent<MvpWorldPanel>();
            world.BindEnvironmentArt(workshopBackground, farmBackground, pumpBroken, pumpRepaired);
            BindQuestFlow();
            world.Bind(Quest01, lathe, questFlow);
            var illustrations = new[]
            {
                new CharacterIllustrationBinding { characterId = "Seongcheol", dialogueDefault = seongcheolDialogueDefault },
                new CharacterIllustrationBinding { characterId = "Seoyeon", dialogueDefault = seoyeonDialogueDefault }
            };
            world.BindDialogueIllustrations(illustrations);
            world.enabled = false;
            overlay = gameObject.AddComponent<MvpStartupOverlay>();
            overlay.Bind(Quest01, lathe, world);
            overlay.BindDialogueFlow(questFlow);
            overlay.BindDialogueIllustrations(illustrations);
            overlay.BindPersistence(SaveCurrent, LoadCurrent, saveStore.Exists);
            Debug.Log("Last Machinist: QUEST 01 systems loaded; turning station ready.", this);
        }
    }
}
