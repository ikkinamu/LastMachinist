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
        private GameplaySystemMenu systemMenu;
        private readonly SaveGameStore saveStore = new SaveGameStore();
        private bool checkpointPending;
        private bool titleHasValidSave, titleHasAnySave;
        private void QueueAutoSave() => checkpointPending = true;
        private void SaveAutoEvent() { if (enabled) TrySaveAuto(); }
        private void BindAutoSaveEvents(Quest01Integration value)
        {
            value.RepeatableQuests.Changed += QueueAutoSave;
            value.ProgressionChanged += QueueAutoSave;
        }
        private void UnbindAutoSaveEvents(Quest01Integration value)
        {
            if (value == null) return;
            value.RepeatableQuests.Changed -= QueueAutoSave;
            value.ProgressionChanged -= QueueAutoSave;
        }
        private void OnQuestStateChanged(QuestState previous, QuestState current)
        { if (SaveGameComposer.IsCheckpoint(current)) checkpointPending = true; }
        private void LateUpdate()
        {
            if (!checkpointPending || overlay == null || world == null) return;
            // State events may precede stock mounting and location changes in the
            // same command. Capture only after those handlers have returned.
            checkpointPending = false;
            TrySaveAuto();
        }
        private void OnDestroy()
        {
            if (Quest01 != null) Quest01.Quest.StateChanged -= OnQuestStateChanged;
            UnbindAutoSaveEvents(Quest01);
            ExplorationSession.AutoSaveRequested -= SaveAutoEvent;
            questFlow?.Dispose();
            if (systemMenu != null) Destroy(systemMenu.gameObject);
        }
        private SaveGameData CaptureCurrent()
        {
            lathe.SyncInventoryMeasurements();
            var data = SaveGameComposer.Capture(Quest01, world.CurrentLocation);
            data.ActiveMachineId = lathe.BenchDrill?.Active != null ? "bench_drill" : lathe.ActiveMachiningWorkpiece != null ? "lathe" : null;
            return data;
        }
        private bool TrySaveManual()
        {
            try { saveStore.Save(CaptureCurrent()); overlay.PersistenceMessage = "저장했습니다."; return true; }
            catch (System.Exception error) { overlay.PersistenceMessage = "저장하지 못했습니다."; Debug.LogException(error); return false; }
        }
        private void SaveCurrent() => TrySaveManual();
        private bool TrySaveAuto()
        {
            if (lathe?.ActiveMachiningWorkpiece != null || Quest01 == null || world == null) return false;
            try { saveStore.SaveAuto(CaptureCurrent()); return true; }
            catch (System.Exception error) { Debug.LogWarning("자동 저장에 실패했습니다: " + error.Message); return false; }
        }
        private bool BeginNewGame()
        {
            try { saveStore.Delete(); titleHasAnySave = false; titleHasValidSave = false; return true; }
            catch (System.Exception error) { overlay.PersistenceMessage = "기존 저장을 초기화하지 못했습니다."; Debug.LogWarning(error.Message); return false; }
        }
        private bool ContinueGame()
        {
            try
            {
                var data = saveStore.LoadLatestValid();
                if (data == null) { overlay.PersistenceMessage = "정상 저장을 찾지 못했습니다."; return false; }
                var restored = SaveGameComposer.Restore(data, lathe.CreateStockWorkpiece);
                lathe.RestoreQuest(restored);
                Quest01.Quest.StateChanged -= OnQuestStateChanged;
                UnbindAutoSaveEvents(Quest01);
                Quest01 = restored;
                Quest01.Quest.StateChanged += OnQuestStateChanged;
                BindAutoSaveEvents(Quest01);
                checkpointPending = false;
                BindQuestFlow();
                world.Bind(restored, lathe, questFlow);
                world.RestoreLocation(data.CurrentSceneId);
                overlay.Resume(restored);
                overlay.PersistenceMessage = "불러왔습니다.";
                return true;
            }
            catch (System.Exception error) { overlay.PersistenceMessage = "불러오지 못했습니다."; Debug.LogException(error); return false; }
        }
        // F12 debug load is kept outside the normal player flow.
        private void LoadCurrent() { ContinueGame(); }
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
            LastMachinist.Inventory.InitialWorkshopWorkpieces.EnsureCreated(inventory, System.Guid.NewGuid().ToString("N"));
            Quest01 = new Quest01Integration(inventory, new QuestRuntimeState(), lathe.CreateStockWorkpiece);
            Quest01.Quest.Data.StockCount = 3;
            Quest01.Quest.StateChanged += OnQuestStateChanged;
            BindAutoSaveEvents(Quest01);
            ExplorationSession.AutoSaveRequested += SaveAutoEvent;
            lathe.BindQuest(Quest01);
            Quest01.AttachWorkpiece(inventory.Workpieces[0].MachiningWorkpiece);
            lathe.MountInitialWorkpiecePreview(inventory.Workpieces[0].MachiningWorkpiece);
            var debug = gameObject.AddComponent<DevelopmentDebugPanel>();
            debug.ReadCurrent = CaptureCurrent; debug.SaveCurrent = SaveCurrent; debug.LoadCurrent = LoadCurrent;
            debug.AddStock = () => {
                var definition = Resources.Load<WorkpieceDefinition>("WorkpieceDefinitions/RoundBar50");
                Quest01.Inventory.AddWorkpiece(definition.CreateInstance(new ConditionProperties(), "debug", System.Guid.NewGuid().ToString("N")));
            };
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
            overlay.BindPrologue(QueueAutoSave);
            overlay.BindDialogueFlow(questFlow);
            overlay.BindDialogueIllustrations(illustrations);
            titleHasAnySave = saveStore.HasAnySave();
            titleHasValidSave = saveStore.HasValidSave();
            overlay.BindPersistence(SaveCurrent, ContinueGame, BeginNewGame, GameQuit.Perform,
                () => titleHasValidSave, () => titleHasAnySave);
            var menuHost = new GameObject("Gameplay System Menu");
            DontDestroyOnLoad(menuHost);
            systemMenu = menuHost.AddComponent<GameplaySystemMenu>();
            systemMenu.Bind(TrySaveManual, GameQuit.Perform, () => overlay != null && overlay.GameStarted, lathe);
            Debug.Log("Last Machinist: QUEST 01 systems loaded; turning station ready.", this);
        }
    }
}
