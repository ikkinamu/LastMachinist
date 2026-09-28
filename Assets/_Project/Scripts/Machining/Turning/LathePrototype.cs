using System;
using System.Collections.Generic;
using LastMachinist.Core;
using LastMachinist.Evaluation;
using LastMachinist.Measurement;
using LastMachinist.Integration;
using LastMachinist.Quest;
using LastMachinist.Machining.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastMachinist.Machining.Turning
{
    public enum HudPage { Lathe, Measurement, Quest, Memo, Inventory, Result, Workshop, Records, MachineSelect, Exploration }
    public enum LatheViewMode { Front, Top, Right, Isometric }

    // Composition/presentation only. Every input action passes through LatheInputRouter.
    public sealed partial class LathePrototype : MonoBehaviour
    {
        private LathePrototypeSettings settings;
        private LatheController controller;
        private LatheInputRouter router;
        private InputActionMap inputs;
        private InputAction xKey, zKey, fineKey, mediumKey, coarseKey, spindleKey, moveKeys, wheel;
        private Transform stock, tool, chuck, bed, headstock;
        private Transform rulerVisual;
        private MeshFilter toolMeshFilter;
        private Mesh roughToolMesh; private Mesh squareToolMesh;
        private Mesh chamferToolMesh;
        private Camera viewCamera;
        private float contactUntil;
        private MeasurementSession measurement;
        private MeasurementPoint[] measurementPoints;
        private string measurementInput = "";
        private string measurementMessage = "";
        public Func<bool> IsDialogueOpen;
        public HudPage CurrentPage => hudPage;
        public bool HideQuestOverlay => DialogueBlocked || hudPage != HudPage.Workshop;
        public bool DialogueBlocked => IsDialogueOpen != null && IsDialogueOpen();
        public bool HudVisible => initialized && isActiveAndEnabled && !DialogueBlocked;
        public LastMachinist.Inventory.InventoryState PlayerInventory => questIntegration?.Inventory;
        public string MeasurementInput => measurementInput;
        public event Action<string> Notification;
        private bool wasDialogueBlocked, resumeAfterDialogue;
        private MeasurementPoint pendingMeasurementPoint;
        private bool rulerAttached;
        private HudPage hudPage = HudPage.Lathe;
        private Vector2 sidebarScroll;
        private readonly LastMachinist.Inventory.WorkpieceInventoryPanel inventoryPanel = new LastMachinist.Inventory.WorkpieceInventoryPanel();
        private bool questSelected;
        private bool materialChosen;
        private int inferenceTab;
        private Vector2 inferenceTabsScroll;
        private ExplorationRegionDefinition[] explorationRegions;
        private ExplorationRegionDefinition selectedRegion;
        private ExplorationMapDefinition explorationMap;
        private Vector2 regionInfoScroll;
        public RegionProgression RegionProgression => questIntegration?.Regions;
        private Renderer[] stationRenderers;
        private static QuestPage PageAccess(HudPage page) => page == HudPage.Lathe ? QuestPage.Lathe :
            page == HudPage.Measurement ? QuestPage.Measurement : (page == HudPage.Memo || page == HudPage.Records) ? QuestPage.WorkMemo :
            page == HudPage.Result ? QuestPage.Result : page == HudPage.Inventory ? QuestPage.Inventory : QuestPage.Workshop;
        private bool PageAllowed(HudPage page)
        {
            if (ActiveMachiningWorkpiece != null && (page == HudPage.Lathe || page == HudPage.Measurement || page == HudPage.Records))
                return page != HudPage.Measurement || !controller.SpindleRunning;
            if (!CanOpenPage(PageAccess(page))) return false;
            if (page == HudPage.Measurement && (!controller.IsMounted || measurementPoints == null || measurementPoints.Length == 0)) return false;
            return page != HudPage.Measurement || questIntegration == null ||
                QuestAccessPolicy.CanUsePrecisionMeasurement(questIntegration.Quest.Data.State, !controller.SpindleRunning, questIntegration.Quest.Data.HasReceivedVernier);
        }
        public void OpenPage(HudPage page)
        {
            if (DialogueBlocked || !PageAllowed(page)) return;
            if (ActiveMachiningWorkpiece != null && page != HudPage.Lathe && page != HudPage.Measurement && page != HudPage.Records)
                EndInventoryMachining();
            if (page == HudPage.MachineSelect && hudPage != page) { tutorialPreparation = false; inventoryTurningSelected = false; selectedMachiningId = null; preparationQuestId = null; }
            if (activeDial != -1) { activeDial = -1; GUIUtility.hotControl = 0; }
            if (hudPage != page) { rulerAttached = false; sidebarScroll = Vector2.zero; measurementInput = ""; measurementMessage = ""; }
            hudPage = page;
            router.SetActive(page == HudPage.Lathe && isActiveAndEnabled);
            RefreshStationVisibility();
        }
        // Restore a predictable station presentation after loading a workshop checkpoint.
        public void ResumeWorkshop()
        {
            hudPage = PageAllowed(HudPage.Lathe) ? HudPage.Lathe : HudPage.Workshop;
            rulerAttached = false;
            sidebarScroll = Vector2.zero;
            if (router != null) router.SetActive(isActiveAndEnabled && hudPage == HudPage.Lathe && !DialogueBlocked);
            RefreshStationVisibility();
        }
        private void RefreshStationVisibility()
        {
            if (stationRenderers == null) return;
            foreach (var renderer in stationRenderers) renderer.enabled = hudPage == HudPage.Lathe && isActiveAndEnabled && !DialogueBlocked;
        }
        private void PageButton(string text, HudPage page, GUIStyle style)
        {
            GUI.enabled = PageAllowed(page);
            if (GUILayout.Button(text, style)) OpenPage(page);
            GUI.enabled = true;
        }
        private static void EndSidebar() { GUILayout.EndScrollView(); GUILayout.EndArea(); }
        private LatheViewMode viewMode = LatheViewMode.Front;
        private float spindleAngle;
        public void SetCameraView(LatheViewMode value)
        {
            if (!Enum.IsDefined(typeof(LatheViewMode), value)) throw new ArgumentOutOfRangeException(nameof(value));
            viewMode = value;
            if (initialized) UpdateVisuals();
        }
        public static Quaternion CameraRotation(LatheViewMode value)
        {
            switch (value)
            {
                case LatheViewMode.Front: return Quaternion.LookRotation(Vector3.forward, Vector3.up);
                case LatheViewMode.Top: return Quaternion.LookRotation(Vector3.down, Vector3.forward);
                case LatheViewMode.Right: return Quaternion.LookRotation(Vector3.left, Vector3.up);
                case LatheViewMode.Isometric: return Quaternion.LookRotation(new Vector3(-1, -1, 1), Vector3.up);
                default: throw new ArgumentOutOfRangeException(nameof(value));
            }
        }
        private Quest01Integration questIntegration;
        private bool initialized;
        public LatheController Controller => controller;
        public LatheInputRouter Router => router;
        public void BindQuest(Quest01Integration value)
        {
            if (ActiveMachiningWorkpiece != null) EndInventoryMachining();
            if (questIntegration != null) questIntegration.MachiningStockSelected -= MountSelectedStock;
            questIntegration = value;
            preparationQuestId = null; selectedRepeatableQuestId = null;
            inventoryMachining = value == null ? null : new LastMachinist.Inventory.InventoryMachiningSession(value.Inventory, ResolveWorkpieceDefinition);
            if (questIntegration != null) questIntegration.MachiningStockSelected += MountSelectedStock;
            if (initialized && !PageAllowed(hudPage)) OpenPage(HudPage.Workshop);
        }

        public void RestoreQuest(Quest01Integration value)
        {
            materialChosen = false; inferenceTab = 0;
            BindQuest(value);
            router.SetActive(false);
            if (value.CurrentWorkpiece != null && value.Quest.Data.State >= QuestState.MaterialSelected)
                MountSelectedStock(value.CurrentWorkpiece);
            measurement = value.Measurement;
            pendingMeasurementPoint = null; rulerAttached = false; measurementInput = "";
            hudPage = value.Quest.Data.State == QuestState.FinalInspection ? HudPage.Measurement :
                CanOpenPage(QuestPage.Lathe) ? HudPage.Lathe : HudPage.Workshop;
        }

        public WorkpieceState CreateStockWorkpiece() => new WorkpieceState {
            InstanceId = Guid.NewGuid().ToString("N"), MaterialId = DefinitionIds.StructuralCarbonSteel,
            SourceItemId = PrototypeStockId,
            Geometry = new WorkpieceGeometry { Type = GeometryType.RoundProfile,
                RoundProfile = new TurningProfile { TotalLength = settings.StockLength,
                    Segments = new List<TurningSegment> { new TurningSegment {
                        StartZ = 0, EndZ = settings.StockLength, Radius = settings.StockDiameter / 2f } } } } };

        private void MountSelectedStock(WorkpieceState workpiece)
        {
            if (ActiveMachiningWorkpiece != null) EndInventoryMachining();
            router.SetActive(false);
            controller.Profile.Changed -= OnCut;
            controller = new LatheController(workpiece, settings.RoughingTool.CreateState(), settings.BaseStep,
                settings.InitialRadialPosition, settings.InitialZPosition, settings.GripLength,
                settings.ChamferTool != null ? settings.ChamferTool.CreateState() : new ChamferToolState("chamfer_tool", "모따기 바이트", 1f));
            router = new LatheInputRouter(controller);
            router.SetActive(isActiveAndEnabled);
            controller.Profile.Changed += OnCut;
            stock.GetComponent<TurningWorkpieceView>().Bind(controller.Profile);
            measurement = questIntegration.Measurement;
            pendingMeasurementPoint = null;
            rulerAttached = false;
            measurementInput = "";
            contactUntil = 0;
            spindleAngle = 0;
            hudPage = HudPage.MachineSelect;
            tutorialPreparation = true;
            router.SetActive(false);
            UpdateVisuals();
        }

        public void Initialize(LathePrototypeSettings config, Camera camera)
        {
            if (initialized) throw new InvalidOperationException("Prototype already initialized.");
            if (config == null || config.RoughingTool == null || camera == null || config.WheelUnitsPerStep <= 0 ||
                config.StockLength <= 0 || config.StockDiameter <= 0)
                throw new ArgumentException("Missing or invalid prototype configuration.");
            settings = config; viewCamera = camera;
            var workpiece = new WorkpieceState
            {
                InstanceId = Guid.NewGuid().ToString("N"), MaterialId = DefinitionIds.StructuralCarbonSteel,
                SourceItemId = PrototypeStockId,
                Geometry = new WorkpieceGeometry
                {
                    Type = GeometryType.RoundProfile,
                    RoundProfile = new TurningProfile
                    {
                        TotalLength = config.StockLength,
                        Segments = new List<TurningSegment> { new TurningSegment { StartZ = 0, EndZ = config.StockLength, Radius = config.StockDiameter / 2 } }
                    }
                }
            };
            controller = new LatheController(workpiece, config.RoughingTool.CreateState(), config.BaseStep,
                config.InitialRadialPosition, config.InitialZPosition, config.GripLength,
                config.ChamferTool != null ? config.ChamferTool.CreateState() : new ChamferToolState("chamfer_tool", "모따기 바이트", 1f));
            router = new LatheInputRouter(controller);
            measurement = new MeasurementSession(workpiece.Geometry.RoundProfile, workpiece.InstanceId);
            measurementPoints = new[] {
                new MeasurementPoint("L1_Diameter", MeasurementPointType.Diameter, 0, 30, "L1 직경"),
                new MeasurementPoint("L1_Length", MeasurementPointType.Length, 0, 30, "L1 길이"),
                new MeasurementPoint("L2_Diameter", MeasurementPointType.Diameter, 30, 70, "L2 직경"),
                new MeasurementPoint("L2_Length", MeasurementPointType.Length, 30, 70, "L2 길이"),
                new MeasurementPoint("L3_Diameter", MeasurementPointType.Diameter, 70, 100, "L3 직경"),
                new MeasurementPoint("L3_Length", MeasurementPointType.Length, 70, 100, "L3 길이"),
                new MeasurementPoint("TotalLength", MeasurementPointType.Length, 0, 100, "전체 길이") };
            controller.Profile.Changed += OnCut;
            var stockObject = new GameObject("Workpiece (authoritative profile view)");
            stockObject.transform.SetParent(transform, false);
            var stockView = stockObject.AddComponent<TurningWorkpieceView>();
            stockObject.GetComponent<MeshRenderer>().sharedMaterial = config.StockMaterial;
            stockView.Bind(controller.Profile);
            stock = stockObject.transform;
            float left = -config.StockLength * 0.025f;
            chuck = Block("Chuck", new Vector3(left - 0.25f, 0, 0), new Vector3(0.65f, 3.1f, 3.1f), config.MachineMaterial);
            bed = Block("Bed", new Vector3(0, -2.35f, 0.4f), new Vector3(config.StockLength * 0.05f + 2, 0.35f, 2.2f), config.MachineMaterial);
            headstock = Block("Headstock", new Vector3(left - 0.85f, -0.4f, 0.4f), new Vector3(0.8f, 3.5f, 2.5f), config.MachineMaterial);
            tool = Block("Roughing tool", Vector3.zero, new Vector3(config.RoughingTool.Width * 0.05f, 0.75f, 0.35f), config.ToolMaterial);
            toolMeshFilter = tool.GetComponent<MeshFilter>();
            squareToolMesh = toolMeshFilter.sharedMesh; roughToolMesh = RoughingToolGeometry.Build(config.RoughingTool.CreateState());
            chamferToolMesh = CreateChamferToolMesh();
            rulerVisual = Block("Metal ruler", new Vector3(0, 1.65f, 0), new Vector3(config.StockLength * 0.05f, 0.08f, 0.06f), config.ToolMaterial);
            rulerVisual.gameObject.SetActive(false);
            // A visible stripe makes rotation readable even on a round cylinder.
            var marker = Block("Spindle rotation marker", Vector3.zero, new Vector3(0.67f, 0.13f, 2.8f), config.ToolMaterial);
            marker.SetParent(chuck, true); marker.localPosition = Vector3.zero;
            CreateTailstockVisuals();
            SetupInput();
            viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = new Color(0.055f, 0.07f, 0.085f);
            viewCamera.orthographic = true;
            viewCamera.orthographicSize = Mathf.Max(3.6f, config.StockDiameter * 0.07f);
            viewCamera.transform.position = new Vector3(3, 2.5f, -10);
            viewCamera.transform.LookAt(new Vector3(-0.3f, -0.2f, 0));
            initialized = true;
            stationRenderers = GetComponentsInChildren<Renderer>(true);
            UpdateVisuals();
        }

        private const string PrototypeStockId = "prototype_round_stock";
        private Transform Block(string label, Vector3 position, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = label; obj.transform.SetParent(transform, false);
            obj.transform.localPosition = position; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            var collider = obj.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            return obj.transform;
        }

        private void SetupInput()
        {
            inputs = new InputActionMap("Lathe");
            xKey = inputs.AddAction("Select X", binding: "<Keyboard>/x");
            zKey = inputs.AddAction("Select Z", binding: "<Keyboard>/z");
            fineKey = inputs.AddAction("Fine", binding: "<Keyboard>/digit1");
            mediumKey = inputs.AddAction("Medium", binding: "<Keyboard>/digit2");
            coarseKey = inputs.AddAction("Coarse", binding: "<Keyboard>/digit3");
            spindleKey = inputs.AddAction("Spindle", binding: "<Keyboard>/space");
            moveKeys = inputs.AddAction("Handwheel keys", InputActionType.Value);
            moveKeys.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/downArrow").With("Positive", "<Keyboard>/upArrow");
            wheel = inputs.AddAction("Handwheel scroll", InputActionType.Value, "<Mouse>/scroll/y");
            inputs.Enable();
        }

        private void Update()
        {
            if (!initialized) return;
            SynchronizeInputContext();
            if (DialogueBlocked) return;
            if (!PageAllowed(hudPage)) OpenPage(HudPage.Workshop);
            ReadInputsAndRefresh();
        }

        public void SynchronizeInputContext()
        {
            if (!initialized) return;
            if (DialogueBlocked)
            {
                if (!wasDialogueBlocked) resumeAfterDialogue = controller.Active;
                wasDialogueBlocked = true;
                router.SetActive(false); RefreshStationVisibility(); return;
            }
            if (wasDialogueBlocked)
            {
                wasDialogueBlocked = false;
                router.SetActive(resumeAfterDialogue && hudPage == HudPage.Lathe && PageAllowed(HudPage.Lathe));
            }
        }

        private void ReadInputsAndRefresh()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.f1Key.wasPressedThisFrame) OpenPage(HudPage.MachineSelect);
                if (Keyboard.current.f2Key.wasPressedThisFrame) OpenPage(HudPage.Measurement);
                if (Keyboard.current.f3Key.wasPressedThisFrame) OpenPage(HudPage.Quest);
                if (Keyboard.current.f8Key.wasPressedThisFrame) OpenPage(HudPage.Memo);
                if (Keyboard.current.f9Key.wasPressedThisFrame) OpenPage(HudPage.Inventory);
                if (Keyboard.current.f10Key.wasPressedThisFrame) OpenPage(HudPage.Result);
                if (Keyboard.current.f4Key.wasPressedThisFrame) SetCameraView(LatheViewMode.Front);
                if (Keyboard.current.f5Key.wasPressedThisFrame) SetCameraView(LatheViewMode.Top);
                if (Keyboard.current.f6Key.wasPressedThisFrame) SetCameraView(LatheViewMode.Right);
                if (Keyboard.current.f7Key.wasPressedThisFrame) SetCameraView(LatheViewMode.Isometric);
            }
            if (hudPage == HudPage.Lathe && CanOpenPage(QuestPage.Lathe))
            {
            if (xKey.WasPressedThisFrame()) router.SelectAxis(Axis.X);
            if (zKey.WasPressedThisFrame()) router.SelectAxis(Axis.Z);
            if (fineKey.WasPressedThisFrame()) router.SetMovementScale(MovementScale.Fine);
            if (mediumKey.WasPressedThisFrame()) router.SetMovementScale(MovementScale.Medium);
            if (coarseKey.WasPressedThisFrame()) router.SetMovementScale(MovementScale.Coarse);
            if (spindleKey.WasPressedThisFrame()) router.SetSpindleRunning(!controller.SpindleRunning);
                // Keyboard input contributes a discrete wheel rotation.  It must use
                // the same rotation->mm path as drag and mouse-wheel input; no
                // frame-time or speed based feed is allowed here.
                if (moveKeys.WasPressedThisFrame())
                {
                    float move = moveKeys.ReadValue<float>();
                    if (Mathf.Abs(move) > 0.001f) router.Scroll(move * 90f);
                }
                float scroll = wheel.ReadValue<float>();
                bool overStation = Mouse.current != null && StationScreenRect.Contains(Mouse.current.position.ReadValue());
                if (scroll != 0 && overStation) router.ApplyMouseWheel(scroll, settings.WheelUnitsPerStep);
            }
            if (controller.SpindleRunning)
            {
                spindleAngle = Mathf.Repeat(spindleAngle + settings.DisplayRotationSpeed * Time.deltaTime, 360f);
            }
            if(!CompleteParting()) UpdateVisuals();
        }

        private Rect StationScreenRect => LastMachinist.Core.MvpUiScale.ToScreenRect(
            new Rect(384, 120, 896, 520), Screen.width, Screen.height);

        private void UpdateVisuals()
        {
            RefreshStationVisibility();
            tool.gameObject.SetActive(controller.ToolMounted);
            tool.name = controller.SelectedTool == ToolKind.Chamfer ? "Chamfer tool (wedge)" : "Roughing tool (square)";
            toolMeshFilter.sharedMesh = controller.SelectedTool == ToolKind.Chamfer ? chamferToolMesh : (controller.SelectedTool == ToolKind.Roughing || controller.SelectedTool == ToolKind.Finishing) ? roughToolMesh : squareToolMesh;
            if (rulerVisual != null)
            {
                rulerVisual.gameObject.SetActive(rulerAttached);
                rulerVisual.localPosition = new Vector3(0, 1.35f, 0);
            }
            bool chamferSelected = controller.SelectedTool == ToolKind.Chamfer;
            bool slanted = controller.SelectedTool == ToolKind.Roughing || controller.SelectedTool == ToolKind.Finishing;
            tool.localScale = chamferSelected || slanted ? Vector3.one : new Vector3(LastMachinist.Inventory.WorkpieceSplitter.Kerf * 0.05f, 0.75f, 0.15f);
            float stockLength = controller.Profile.TotalLength;
            chuck.localPosition = new Vector3(-stockLength * 0.025f - 0.25f, 0, 0);
            bed.localScale = new Vector3(stockLength * 0.05f + 2, 0.35f, 2.2f);
            headstock.localPosition = new Vector3(-stockLength * 0.025f - 0.85f, -0.4f, 0.4f);
            tool.localPosition = new Vector3((controller.ToolZ - stockLength / 2) * 0.05f,
                controller.ToolX * 0.05f + (chamferSelected || slanted ? 0 : 0.375f), 0);
            UpdateTailstockVisuals();
            var spin = Quaternion.AngleAxis(spindleAngle, Vector3.right);
            stock.localRotation = spin * Quaternion.AngleAxis(controller.Orientation == Orientation.Reversed ? 180f : 0f, Vector3.up);
            chuck.localRotation = spin;
            viewCamera.pixelRect = StationScreenRect;
            // Every view enforces projection, including after a scene/load changes the camera.
            viewCamera.orthographic = true;
            float radialExtent = 0;
            foreach (var segment in controller.Profile.Segments) radialExtent = Mathf.Max(radialExtent, segment.Radius * 0.05f + 1.5f);
            viewCamera.orthographicSize = Mathf.Max(Mathf.Max(3.6f, radialExtent), (stockLength * 0.025f + (controller.Tailstock.Active ? 7 : 2)) / Mathf.Max(0.2f, viewCamera.aspect));
            Vector3 target = new Vector3(-0.3f, -0.2f, 0);
            var rotation = CameraRotation(viewMode);
            viewCamera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * 10f, rotation);
        }

        private void OnCut() { contactUntil = Time.unscaledTime + 0.35f; }
        private void OnApplicationFocus(bool focus) { if (!focus && initialized) router.SetActive(false); }
        private void OnDisable() { inputs?.Disable(); if (initialized) router.SetActive(false); RefreshStationVisibility(); }
        private void OnEnable() { inputs?.Enable(); RefreshStationVisibility(); }
        private void OnDestroy()
        {
            SyncInventoryMeasurements();
            inventoryMachining?.End();
            if (questIntegration != null) questIntegration.MachiningStockSelected -= MountSelectedStock;
            if (controller?.Profile != null) controller.Profile.Changed -= OnCut;
            inputs?.Dispose();
            if (roughToolMesh != null) { if(Application.isPlaying) Destroy(roughToolMesh); else DestroyImmediate(roughToolMesh); }
            if (chamferToolMesh != null) { if (Application.isPlaying) Destroy(chamferToolMesh); else DestroyImmediate(chamferToolMesh); }
        }

        private static Mesh CreateChamferToolMesh()
        {
            // Minimal functional edge visualization; finished tool art requires an external asset.
            var mesh = new Mesh { name = "Runtime chamfer tool wedge" };
            float reach = ChamferToolState.CuttingEdgeReach * 0.05f;
            // Origin is the cutting tip. Both axes use the same mm-to-world scale.
            mesh.vertices = new[]
            {
                new Vector3(-reach, reach, -0.08f), new Vector3(0, reach, -0.08f), new Vector3(0, 0, -0.08f),
                new Vector3(-reach, reach, 0.08f), new Vector3(0, reach, 0.08f), new Vector3(0, 0, 0.08f)
            };
            mesh.triangles = new[] { 0, 1, 2, 3, 5, 4, 0, 4, 1, 0, 3, 4, 0, 5, 3, 0, 2, 5, 2, 4, 5, 2, 1, 4 };
            mesh.RecalculateNormals();
            return mesh;
        }

        private void OnGUI()
        {
            if (!HudVisible) return;
            GUI.depth = 10;
            var oldMatrix = GUI.matrix;
            GUI.matrix = LastMachinist.Core.MvpUiScale.Apply1280x800();
            if (hudPage == HudPage.Workshop)
                MvpUiScale.DrawOpaqueBackground(new Rect(0, 0, 384, 800));
            else MvpUiScale.DrawPageBackground(hudPage == HudPage.Lathe);
            var title = new GUIStyle(GUI.skin.label) { fontSize = 29, fontStyle = FontStyle.Bold };
            var label = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            var button = new GUIStyle(GUI.skin.button) { fontSize = 17, fixedHeight = 42 };
            GUI.Box(new Rect(12, 12, 356, 776), GUIContent.none);
            GUILayout.BeginArea(new Rect(30, 28, 320, 575));
            GUILayout.Label("LAST MACHINIST", title);
            GUILayout.Label(hudPage == HudPage.Lathe ? "범용 선반" : "작업장", label);
            DrawProgressControls(button);
            if (GUILayout.Button("작업장으로 돌아가기", button)) OpenPage(HudPage.Workshop);
            GUILayout.BeginHorizontal();
                PageButton("가공", HudPage.MachineSelect, button);
                PageButton("측정", HudPage.Measurement, button);
                PageButton("퀘스트", HudPage.Quest, button);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            PageButton("메모", HudPage.Memo, button);
            PageButton("인벤토리", HudPage.Inventory, button);
            PageButton("결과", HudPage.Result, button);
            GUILayout.EndHorizontal();
            PageButton("탐색", HudPage.Exploration, button);
            GUILayout.Space(12);
            sidebarScroll = GUILayout.BeginScrollView(sidebarScroll);
            if(hudPage!=HudPage.Lathe) DrawRepeatableTracker(label);
            if (hudPage == HudPage.Exploration)
            {
                EndSidebar();
                DrawExplorationMap(title, label, button);
                GUI.matrix = oldMatrix;
                return;
            }
            if (hudPage == HudPage.MachineSelect)
            {
                EndSidebar();
                if (!tutorialPreparation)
                {
                    DrawInventoryMachiningPreparation(title, label, button);
                    GUI.matrix = oldMatrix; return;
                }
                GUI.Label(new Rect(420, 40, 540, 50), "작업 준비", title);
                var preparation = questIntegration?.Quest;
                bool preparing = preparation != null && preparation.Data.State == QuestState.ShapeInferred;
                GUI.Label(new Rect(420, 105, 740, 32), "1. 가공 방식 선택", label);
                GUI.enabled = preparing || PageAllowed(HudPage.Lathe);
                if (GUI.Button(new Rect(420, 145, 740, 48), "선반 · 회전체 가공", button))
                {
                    if (preparing) { preparation.SelectTurning(); materialChosen = false; }
                    else OpenPage(HudPage.Lathe);
                }
                GUI.enabled = false;
                GUI.Button(new Rect(420, 201, 740, 48), "밀링 · 준비 중", button);
                GUI.Button(new Rect(420, 257, 740, 48), "드릴링 · 준비 중", button);
                GUI.enabled = true;
                if (preparing && preparation.Data.TurningSelected)
                {
                    GUI.Label(new Rect(420, 337, 740, 32), "2. 재료 선택 · 선택한 방식: 선반", label);
                    int count = questIntegration.Inventory.GetQuantity(Quest01Integration.StockItemId);
                    GUI.enabled = count > 0;
                    if (GUI.Button(new Rect(420, 379, 740, 48), "기계구조용 탄소강 원형봉 · 보유 " + count + "개" + (materialChosen ? " · 선택됨" : ""), button)) materialChosen = true;
                    GUI.enabled = materialChosen && count > 0;
                    if (GUI.Button(new Rect(420, 451, 740, 48), "선반 / 탄소강 원형봉 1개 · 확정 후 가공 시작", button) && questIntegration.SelectMaterial())
                    {
                        materialChosen = false;
                        if (questIntegration.BeginMachining()) OpenPage(HudPage.Lathe);
                    }
                    GUI.enabled = true;
                }
                else GUI.Label(new Rect(420, 337, 740, 70), preparing ? "가공 방식을 선택하면 재료를 선택할 수 있습니다." : "측정과 형상 추론을 마친 뒤 작업을 준비하세요.", label);
                GUI.matrix = oldMatrix; return;
            }
            if (hudPage == HudPage.Workshop)
            {
                GUILayout.Label(questIntegration?.Quest.Data.State == QuestState.Offered
                    ? "수락 가능한 의뢰가 있습니다. 퀘스트 창에서 의뢰를 선택하고 수락하세요."
                    : "작업대에서 부품을 살펴보고, 준비가 되면 선반을 사용하세요.", label);
                EndSidebar(); GUI.matrix = oldMatrix; return;
            }
            if (hudPage == HudPage.Measurement)
            {
                EndSidebar();
                GUI.Label(new Rect(400, 35, 700, 48), "측정", title);
                DrawMeasurementPanel(label, button);
                GUI.matrix = oldMatrix;
                return;
            }
            if (hudPage == HudPage.Quest)
            {
                GUILayout.Label("의뢰를 선택하고 내용을 확인하세요.", label);
                EndSidebar();
                GUI.Label(new Rect(400, 35, 700, 48), "퀘스트", title);
                if (GUI.Button(new Rect(400, 88, 250, 42), "메인 / 스토리", button)) repeatableQuestTab = false;
                if (GUI.Button(new Rect(668, 88, 250, 42), "반복 의뢰", button)) repeatableQuestTab = true;
                if (repeatableQuestTab) { DrawRepeatableQuestWindow(label, button); GUI.matrix = oldMatrix; return; }
                var quest = questIntegration?.Quest;
                var list = MvpPanelLayout.QuestList;
                var detail = MvpPanelLayout.QuestDetail;
                GUI.Box(list, GUIContent.none); GUI.Box(detail, GUIContent.none);
                if (quest == null || quest.Data.State == QuestState.NotStarted)
                    GUI.Label(list, "등록된 의뢰가 없습니다.", label);
                else
                {
                    string status = quest.Data.State == QuestState.Offered ? "수락 가능" : quest.IsActive ? "진행 중" : "완료";
                    GUI.Label(new Rect(list.x + 16, list.y + 16, list.width - 32, 30), status, label);
                    if (GUI.Button(new Rect(list.x + 16, list.y + 56, list.width - 32, 60), "멈춰버린 펌프", button)) questSelected = true;
                    if (questSelected)
                    {
                        GUI.Label(new Rect(detail.x + 16, detail.y + 16, detail.width - 32, 44), "멈춰버린 펌프", label);
                        GUI.Label(new Rect(detail.x + 16, detail.y + 72, detail.width - 32, 200),
                            "서연의 농장에 있는 고장 난 펌프를 확인하고, 축을 측정·가공하여 펌프를 수리하세요.\n\n상태: " + status, label);
                        if (quest.IsActive) GUI.Label(new Rect(detail.x + 16, detail.y + 288, detail.width - 32, 100), QuestFlowController.ObjectiveFor(quest.Data.State), label);
                        if (quest.Data.State == QuestState.Offered && GUI.Button(MvpPanelLayout.QuestAccept, "수락", button)) quest.Accept();
                    }
                    else GUI.Label(new Rect(detail.x + 16, detail.y + 16, detail.width - 32, 80), "목록에서 의뢰를 선택하세요.", label);
                }
                GUI.matrix = oldMatrix;
                return;
            }
            if (hudPage == HudPage.Memo)
            {
                EndSidebar();
                DrawInferenceMemo(title, label, button);
                GUI.matrix = oldMatrix;
                return;
            }
            if (hudPage == HudPage.Inventory)
            {
                EndSidebar();
                GUI.Label(new Rect(400, 35, 700, 48), "인벤토리", title);
                GUI.Label(new Rect(400, 110, 290, 96), BuildPageText(), label);
                inventoryPanel.Draw(PlayerInventory, label, new GUIStyle(button) { fixedHeight = 64, wordWrap = true });
                GUI.matrix = oldMatrix;
                return;
            }
            if (hudPage == HudPage.Result || hudPage == HudPage.Records)
            {
                EndSidebar();
                GUI.Label(new Rect(400, 35, 700, 48), hudPage == HudPage.Memo ? "작업 메모" : hudPage == HudPage.Records ? "측정 기록 목록" : hudPage == HudPage.Inventory ? "인벤토리" : "검사 결과", title);
                GUI.Label(new Rect(420, 120, 760, (hudPage == HudPage.Memo || hudPage == HudPage.Records) ? 310 : 90), BuildPageText(), label);
                if (hudPage == HudPage.Records && GUI.Button(new Rect(420, 460, 260, 48), "측정으로 돌아가기", button)) OpenPage(HudPage.Measurement);
                if (hudPage == HudPage.Result && questIntegration != null && questIntegration.Quest.Data.State == QuestState.FinalInspection &&
                    GUI.Button(new Rect(420, 220, 240, 48), "실제 형상 판정", button))
                    questIntegration.Evaluate(CreatePrototypeRequirement());
                if (hudPage == HudPage.Result && questIntegration?.LastEvaluation != null &&
                    questIntegration.Quest.Data.State == QuestState.FinalInspection)
                {
                    if (questIntegration.LastEvaluation.CanRework && GUI.Button(new Rect(420, 280, 240, 48), "재가공으로 돌아가기", button))
                    { if (questIntegration.Rework()) OpenPage(HudPage.Lathe); }
                    if (questIntegration.LastEvaluation.Result == PartEvaluationResult.Rejected)
                    {
                        GUI.enabled = questIntegration.Inventory.GetQuantity(Quest01Integration.StockItemId) > 0;
                        if (GUI.Button(new Rect(420, 280, 300, 48), "새 소재 1개로 다시 시작", button)) questIntegration.RetryRejected();
                        GUI.enabled = true;
                        if (questIntegration.Inventory.GetQuantity(Quest01Integration.StockItemId) == 0)
                        {
                            GUI.Label(new Rect(420, 350, 700, 65), "소재를 모두 사용했습니다. MVP 연습 복구는 소재 3개로 가공을 다시 시작합니다.", label);
                            if (GUI.Button(new Rect(420, 425, 310, 48), "가공 연습 다시 시작 (MVP)", button))
                                questIntegration.RestartMachiningPractice();
                        }
                    }
                }
                GUI.matrix = oldMatrix;
                return;
            }
            EndSidebar();
            DrawHandwheelPanel(); DrawLatheMiniUi(label, button);
            GUI.Label(new Rect(420, 28, 780, 40), controller.ActiveToolLabel, title);
            GUI.Label(new Rect(420,72,800,44), controller.StatusMessage ?? "공구를 선택하고 위치를 조정하세요.", label);
            GUI.matrix = oldMatrix;
        }

        private void DrawExplorationMap(GUIStyle title, GUIStyle label, GUIStyle button)
        {
            explorationRegions ??= Resources.LoadAll<ExplorationRegionDefinition>("ExplorationRegions");
            explorationMap ??= Resources.Load<ExplorationMapDefinition>("ExplorationMap");
            GUI.Label(new Rect(404, 35, 540, 48), "탐색 · 지역 지도", title);
            if (RegionProgression == null) return;
            var background = explorationMap != null ? explorationMap.BackgroundImage : null;
            var map = ExplorationMapLayout.ImageRect(ExplorationMapLayout.MapArea, background != null ? (float)background.width/background.height : explorationMap != null ? explorationMap.PlaceholderAspect : 1);
            // MapArea / BackgroundImage / RegionMarkerContainer share the same fitted rect.
            if (background != null) GUI.DrawTexture(map, background, ScaleMode.StretchToFill);
            else { GUI.Box(map, ""); GUI.Label(new Rect(map.x+12,map.y+12,map.width-24,52), "지역 지도 · 임시 배경\n지도 아트 교체 예정", label); }
            foreach (var region in explorationRegions)
            {
                if (!RegionProgression.IsVisible(region.RegionId)) continue;
                var marker = ExplorationMapLayout.Marker(map, region.MapPosition);
                bool rumored = RegionProgression.Find(region.RegionId).DiscoveryState == RegionDiscoveryState.Rumored;
                Color old = GUI.backgroundColor;
                if (selectedRegion == region) GUI.backgroundColor = new Color(1,.78f,.28f);
                var content = new GUIContent(rumored ? "?" : "●", RegionProgression.Name(region));
                if (!rumored && region.MapIcon != null) content = new GUIContent(region.MapIcon, RegionProgression.Name(region));
                if (GUI.Button(marker, content, button)) { selectedRegion = region; regionInfoScroll = Vector2.zero; }
                GUI.backgroundColor = old;
                if (HasActiveRegionQuest(region)) GUI.Label(new Rect(marker.xMax-5,marker.y-12,24,28), "!", label);
                GUI.Label(new Rect(marker.center.x-85,marker.yMax+2,170,48), RegionProgression.Name(region), new GUIStyle(label) { alignment=TextAnchor.UpperCenter, fontSize=14, wordWrap=true });
            }
            if (selectedRegion != null && !RegionProgression.IsVisible(selectedRegion.RegionId)) selectedRegion = null;
            GUILayout.BeginArea(ExplorationMapLayout.InfoArea, GUI.skin.box);
            regionInfoScroll = GUILayout.BeginScrollView(regionInfoScroll);
            GUILayout.Label(selectedRegion == null ? "지도에서 지역 마커를 선택하세요." : RegionProgression.Information(selectedRegion), new GUIStyle(label) { wordWrap=true });
            if (selectedRegion != null && HasActiveRegionQuest(selectedRegion)) GUILayout.Label("\n관련 활성 의뢰\n멈춰버린 펌프", label);
            GUILayout.EndScrollView(); GUILayout.EndArea();
            GUI.enabled = selectedRegion != null && RegionProgression.CanEnter(selectedRegion.RegionId) && !ExplorationSession.IsExploring;
            if (GUI.Button(ExplorationMapLayout.StartButton, "탐색 시작", button)) ExplorationSession.Enter(selectedRegion, this);
            GUI.enabled = true;
        }
        private bool HasActiveRegionQuest(ExplorationRegionDefinition region)
            => System.Array.IndexOf(region.RelatedQuestIds, RegionProgression.PumpQuestId) >= 0 &&
               questIntegration.Quest.Data.State != QuestState.NotStarted && questIntegration.Quest.Data.State != QuestState.Completed;

        private void DrawInferenceMemo(GUIStyle title, GUIStyle label, GUIStyle button)
        {
            GUI.Label(new Rect(400, 35, 540, 48), "작업 메모 · 형상 추론", title);
            var memo = questIntegration?.Quest.Data.WorkMemo;
            if (memo == null) return;
            var entries = memo.CurrentInferences;
            GUILayout.BeginArea(new Rect(404, 112, 230, 590), GUI.skin.box);
            inferenceTabsScroll = GUILayout.BeginScrollView(inferenceTabsScroll);
            for (int i = 0; i < entries.Count; i++)
                if (GUILayout.Button(entries[i].Label, button)) inferenceTab = i;
            GUILayout.EndScrollView(); GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(650, 112, 590, 590), GUI.skin.box);
            if (entries.Count == 0) GUILayout.Label("추론할 대상 형상이 없습니다.", label);
            else
            {
                inferenceTab = Mathf.Clamp(inferenceTab, 0, entries.Count - 1);
                var entry = entries[inferenceTab];
                GUILayout.Label(entry.Label + " · 직접 추론한 값", label);
                GUILayout.Label("예상 직경 (mm)", label);
                entry.DiameterText = GUILayout.TextField(entry.DiameterText ?? "", 100);
                GUILayout.Label("예상 길이 (mm)", label);
                entry.LengthText = GUILayout.TextField(entry.LengthText ?? "", 100);
                GUILayout.Label("관찰 / 추론 메모", label);
                entry.Note = GUILayout.TextArea(entry.Note ?? "", 1000, GUILayout.Height(120));
                GUILayout.Label("입력한 값은 직접 기록됩니다. 측정 기록은 측정 기록 목록에서 확인하세요.", label);
                if (GUILayout.Button("측정 기록 목록", button)) OpenPage(HudPage.Records);
                GUILayout.Label(memo.TargetShapeUnlocked ? "형상 추론 완료" : memo.Hint, label);
            }
            GUILayout.EndArea();
            GUI.enabled = memo.CanInfer && questIntegration.Quest.Data.State == QuestState.InspectingBrokenShaft;
            if (GUI.Button(MvpPanelLayout.QuestAccept, "원래 형상 추론", button)) questIntegration.InferShapeFromMemo();
            GUI.enabled = true;
        }

        private void DrawAxisControls(Axis axis, GUIStyle button)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(axis + "축");
            int direction = MvpPanelLayout.ButtonRow(axis == Axis.X ? new[] { "X - 안쪽", "X + 바깥쪽" } : new[] { "Z - 왼쪽", "Z + 오른쪽" }, button);
            if (direction >= 0) router.ApplyHandwheelRotation(axis, direction == 0 ? -90 : 90);
            GUILayout.EndVertical();
        }

        private void DrawProgressControls(GUIStyle button)
        {
            if (ActiveMachiningWorkpiece != null)
            {
                GUILayout.Label("보유 공작물 가공 · " + InventoryMaterialLabel(), GUI.skin.label);
                if (GUILayout.Button("주축 정지 후 측정", button)) { router.SetSpindleRunning(false); OpenPage(HudPage.Measurement); }
                if (GUILayout.Button("가공 종료 · 인벤토리로", button)) OpenPage(HudPage.Inventory);
                return;
            }
            if (questIntegration == null) return;
            var state = questIntegration.Quest.Data.State;
            if (state == QuestState.MaterialSelected)
            {
                if (GUILayout.Button("가공 시작", button) && questIntegration.BeginMachining())
                    OpenPage(HudPage.MachineSelect);
            }
            else if (state == QuestState.Machining)
            {
                GUILayout.Label("최종 검사: 직경 · 길이 · 끝단 모따기", new GUIStyle(GUI.skin.label) { fontSize = 14 });
                if (controller.SpindleRunning && GUILayout.Button("주축 정지", button))
                    router.SetSpindleRunning(false);
                GUI.enabled = !controller.SpindleRunning;
                if (GUILayout.Button(controller.SpindleRunning ? "주축 정지 후 가공 종료" : "가공 종료 → 최종 측정", button))
                    FinishMachiningAndMeasure();
                GUI.enabled = true;
            }
            else if (state == QuestState.FinalInspection)
            {
                if (questIntegration.LastEvaluation?.CanRework == true && GUILayout.Button("추가 작업 → 선반으로", button))
                {
                    if (questIntegration.Rework()) OpenPage(HudPage.Lathe);
                    return;
                }
                if (hudPage != HudPage.Measurement && GUILayout.Button("최종 측정하기", button))
                    OpenPage(HudPage.Measurement);
                if (GUILayout.Button("측정 완료 → 형상 판정", button)) EvaluateFinishedWorkpiece();
            }
            else if (state == QuestState.ReadyToDeliver && GUILayout.Button("납품 진행 → 작업장", button))
                OpenPage(HudPage.Workshop);
        }

        public bool FinishMachiningAndMeasure()
        {
            if (ActiveMachiningWorkpiece != null && !DialogueBlocked && !controller.SpindleRunning) { OpenPage(HudPage.Measurement); return true; }
            if (DialogueBlocked || questIntegration == null || controller.SpindleRunning) return false;
            if (!questIntegration.BeginFinalInspection(true)) return false;
            OpenPage(HudPage.Measurement);
            return true;
        }

        public void EvaluateFinishedWorkpiece()
        {
            if (DialogueBlocked || questIntegration == null || controller.SpindleRunning ||
                questIntegration.Quest.Data.State != QuestState.FinalInspection) return;
            questIntegration.Evaluate(CreatePrototypeRequirement());
            OpenPage(HudPage.Result);
        }

        private string BuildPageText()
        {
            if (questIntegration == null) return "퀘스트 데이터가 아직 연결되지 않았습니다.";
            if (hudPage == HudPage.Records)
            {
                var text = new System.Text.StringBuilder("현재 공작물 · 직접 기록한 값 (mm)\n\n");
                foreach (var point in measurementPoints)
                {
                    text.Append(point.DisplayHint).Append(": ");
                    if (measurement != null && measurement.Records.TryGetValue(point.Id, out var record)) text.Append(record.PlayerValue.ToString("0.###"));
                    else text.Append("기록 없음");
                    text.AppendLine();
                }
                return text.ToString();
            }
            if (hudPage == HudPage.Inventory)
                return "기계구조용 탄소강 원형봉: " + questIntegration.Inventory.GetQuantity(Quest01Integration.StockItemId) + "개\n금속 자: 보유\n황삭 바이트: 보유\n모따기 바이트: 보유";
            var result = questIntegration.LastEvaluation;
            return result == null ? "아직 검사 결과가 없습니다." : "판정: " + ResultLabel(result.Result) + "\n" + result.Reason;
        }

        private static string ResultLabel(PartEvaluationResult result)
        {
            switch (result)
            {
                case PartEvaluationResult.Perfect: return "매우 양호";
                case PartEvaluationResult.Acceptable: return "사용 가능";
                case PartEvaluationResult.Reworkable: return "재가공 필요";
                default: return "사용 불가";
            }
        }

        private static string ViewLabel(LatheViewMode mode)
        {
            switch (mode)
            {
                case LatheViewMode.Top: return "평면";
                case LatheViewMode.Right: return "우측면";
                case LatheViewMode.Isometric: return "등각투영";
                default: return "정면";
            }
        }

        private static PartRequirement CreatePrototypeRequirement()
        {
            return new PartRequirement { PerfectTolerance = 0.25f, FunctionalTolerance = 1f, AssessCurrentFeatureLengths = true, Dimensions = new List<DimensionRequirement> {
                new DimensionRequirement("L1", 0, 30, 40), new DimensionRequirement("L2", 30, 70, 50), new DimensionRequirement("L3", 70, 100, 40) },
                RequiredChamferPositions = new List<float> { 0 } };
        }

        private bool CanOpenPage(QuestPage page)
        {
            if (ActiveMachiningWorkpiece != null && (page == QuestPage.Lathe || page == QuestPage.Measurement || page == QuestPage.WorkMemo)) return true;
            return questIntegration == null || QuestAccessPolicy.CanOpenPage(questIntegration.Quest.Data.State, page);
        }

        private void SelectMeasurementPoint(MeasurementPoint point)
        {
            if (point == null || measurement == null || controller.SpindleRunning) return;
            pendingMeasurementPoint = point;
            measurementInput = ""; measurementMessage = "";
            measurement.Select(point, "old_vernier", true);
            rulerAttached = true;
        }

        private void DrawMeasurementPanel(GUIStyle label, GUIStyle button)
        {
            if (ActiveMachiningWorkpiece == null && questIntegration != null && !ReferenceEquals(measurement, questIntegration.Measurement))
            {
                measurement = questIntegration.Measurement;
                rulerAttached = false; pendingMeasurementPoint = null; measurementInput = "";
            }
            if (measurement == null) return;
            if (GUI.Button(new Rect(1040, 35, 170, 42), "기록 목록", button)) OpenPage(HudPage.Records);
            for (int i = 0; i < measurementPoints.Length; i++)
            {
                var point = measurementPoints[i];
                var previousColor = GUI.backgroundColor;
                if (pendingMeasurementPoint == point) GUI.backgroundColor = new Color(0.9f, 0.7f, 0.3f);
                GUI.enabled = !controller.SpindleRunning;
                if (GUI.Button(new Rect(400, 100 + i * 35, 170, 31), point.DisplayHint))
                    SelectMeasurementPoint(point);
                GUI.backgroundColor = previousColor;
            }
            GUI.enabled = true;
            DrawMeasurementWorkpiece(label);
            if (rulerAttached && measurement.CurrentReading != null)
                VernierView.Draw(measurement.CurrentReading);
            else GUI.Label(new Rect(460, 440, 700, 60), "왼쪽 목록 또는 공작물을 선택하면 버니어 눈금이 표시됩니다.", label);
            GUI.Label(new Rect(410, 644, 240, 30), "읽은 값 입력 (mm)", label);
            GUI.enabled = rulerAttached && !controller.SpindleRunning;
            measurementInput = GUI.TextField(new Rect(650, 644, 200, 36), measurementInput);
            if (GUI.Button(new Rect(865, 644, 145, 40), "기록", button))
            {
                if (float.TryParse(measurementInput, out float value) && !float.IsNaN(value) && !float.IsInfinity(value))
                {
                    var record = measurement.Record(value);
                    if (ActiveMachiningWorkpiece != null) SyncInventoryMeasurements();
                    else questIntegration?.RecordMeasurement(record);
                    measurementMessage = "기록했습니다.";
                    Notification?.Invoke("측정 기록을 저장했습니다.");
                }
                else measurementMessage = "읽은 값을 숫자로 입력하세요.";
            }
            if (GUI.Button(new Rect(1025, 644, 175, 40), "다시 측정", button))
            {
                measurement.Select(pendingMeasurementPoint, "old_vernier", true);
                measurementInput = ""; measurementMessage = "";
            }
            GUI.enabled = true;
            GUI.Label(new Rect(410, 687, 780, 30), measurementMessage, label);
            if (ActiveMachiningWorkpiece == null && questIntegration?.Quest.Data.State == QuestState.InspectingBrokenShaft)
            {
                if (GUI.Button(new Rect(410, 740, 340, 40), questIntegration.Quest.Data.WorkMemo.L3WearObserved ? "L3 마모 관찰 기록됨" : "L3 마모 흔적 관찰", button))
                    questIntegration.ObserveL3Wear();
            }
            if (GUI.Button(new Rect(885, 740, 315, 42), PageAllowed(HudPage.Lathe) ? "가공으로 돌아가기" : "작업장으로 돌아가기", button))
                OpenPage(PageAllowed(HudPage.Lathe) ? HudPage.Lathe : HudPage.Workshop);
        }

        private void DrawMeasurementWorkpiece(GUIStyle label)
        {
            var profile = ActiveMachiningWorkpiece != null ? controller.Profile : questIntegration?.CurrentWorkpiece?.Geometry.RoundProfile ?? controller.Profile;
            if (profile == null) return;
            const float centreY = 225;
            float maximumRadius = 1;
            foreach (var segment in profile.Segments) maximumRadius = Mathf.Max(maximumRadius, segment.Radius);
            float unit = Mathf.Min(580f / profile.TotalLength, 88f / maximumRadius);
            float left = 905 - profile.TotalLength * unit / 2;
            var previous = GUI.color;
            GUI.color = new Color(0.08f, 0.1f, 0.13f, 1);
            GUI.DrawTexture(new Rect(585, 100, 630, 252), Texture2D.whiteTexture);
            // Direct clicks preserve the chosen dimension type and share the list selection.
            var evt = Event.current;
            if (!controller.SpindleRunning && evt.type == EventType.MouseDown && evt.button == 0)
            {
                var type = pendingMeasurementPoint?.Type ?? MeasurementPointType.Diameter;
                foreach (var candidate in measurementPoints)
                {
                    if (candidate.Type != type || candidate.EndZ - candidate.StartZ >= profile.TotalLength) continue;
                    var hit = new Rect(left + candidate.StartZ * unit, centreY - maximumRadius * unit - 10,
                        (candidate.EndZ - candidate.StartZ) * unit, maximumRadius * unit * 2 + 20);
                    if (!hit.Contains(evt.mousePosition)) continue;
                    SelectMeasurementPoint(candidate); evt.Use(); break;
                }
            }
            foreach (var segment in profile.SurfaceSpans())
            {
                var selected = pendingMeasurementPoint;
                float width = (segment.EndZ - segment.StartZ) * unit;
                int strips = Mathf.Max(1, Mathf.CeilToInt(width));
                for (int strip = 0; strip < strips; strip++)
                {
                    float sampleZ = Mathf.Lerp(segment.StartZ, segment.EndZ, (strip + 0.5f) / strips);
                    bool inRegion = selected == null || (sampleZ >= selected.StartZ && sampleZ <= selected.EndZ);
                    GUI.color = inRegion ? new Color(0.85f, 0.76f, 0.48f) : new Color(0.32f, 0.38f, 0.42f);
                    float radius = Mathf.Lerp(segment.StartRadius, segment.EndRadius, (strip + 0.5f) / strips);
                    GUI.DrawTexture(new Rect(left + segment.StartZ * unit + strip * width / strips,
                        centreY - radius * unit, width / strips, radius * 2 * unit), Texture2D.whiteTexture);
                }
            }
            var point = pendingMeasurementPoint;
            if (point != null)
            {
                float z = measurement.ContactPosition(point);
                float opening = rulerAttached && measurement.CurrentReading != null ? new VernierDisplayModel(measurement.CurrentReading).JawOpening : measurement.TrueDimension(point);
                float radius = point.Type == MeasurementPointType.Diameter ? opening / 2f : 0;
                float gap = rulerAttached ? 0 : 6;
                GUI.color = rulerAttached ? new Color(0.95f, 0.75f, 0.28f) : Color.white;
                if (point.Type == MeasurementPointType.Diameter)
                {
                    float x = left + z * unit;
                    float top = centreY - radius * unit - gap;
                    float bottom = centreY + radius * unit + gap;
                    GUI.DrawTexture(new Rect(x - 20, top - 3, 25, 3), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(x - 20, bottom, 25, 3), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(x - 23, top - 3, 3, bottom - top + 6), Texture2D.whiteTexture);
                }
                else
                {
                    measurement.MaterialBounds(point, out float materialStart, out _);
                    float a = left + materialStart * unit - gap, b = left + (materialStart + opening) * unit + gap;
                    float jawY = centreY - maximumRadius * unit - 8;
                    GUI.DrawTexture(new Rect(a - 3, jawY, 3, centreY - jawY), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(b, jawY, 3, centreY - jawY), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(a, jawY, b - a, 3), Texture2D.whiteTexture);
                }
            }
            GUI.color = previous;
            var caption = new GUIStyle(label) { fontSize = 15 };
            GUI.Label(new Rect(595, 105, 600, 25), point == null ? "공작물 클릭 · 직경 선택" : "검사 대상 · " + point.DisplayHint + "  /  다른 구간을 눌러 선택", caption);
        }

        private int activeDial = -1;
        private float previousDialAngle;
        private void DrawChamferDetail(Rect rect)
        {
            // Magnify the actual exposed profile, never a decorative target bevel.
            var profile = controller.Profile;
            if (profile == null) return;
            GUI.Box(rect, GUIContent.none);
            var caption = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            GUI.Label(new Rect(rect.x + 8, rect.y + 3, rect.width - 16, 20), "노출 끝단 · 실제 형상 확대", caption);
            bool reversed = controller.Orientation == Orientation.Reversed;
            float extent = Mathf.Min(4f, profile.TotalLength / 4f);
            float maxRadius = 0;
            const int samples = 100;
            for (int i = 0; i <= samples; i++)
            {
                float distance = extent * i / samples;
                maxRadius = Mathf.Max(maxRadius, profile.RadiusAt(reversed ? distance : profile.TotalLength - distance));
            }
            float scale = 16f, right = rect.xMax - 12, top = rect.y + 32, bottom = rect.yMax - 24;
            var oldColor = GUI.color;
            GUI.color = new Color(0.68f, 0.74f, 0.77f);
            for (int i = 0; i < samples; i++)
            {
                float distance = extent * (i + 0.5f) / samples;
                float radius = profile.RadiusAt(reversed ? distance : profile.TotalLength - distance);
                float y = Mathf.Clamp(top + (maxRadius - radius) * scale, top, bottom);
                GUI.DrawTexture(new Rect(right - (i + 1) * extent * scale / samples, y,
                    extent * scale / samples + 0.1f, bottom - y), Texture2D.whiteTexture);
            }
            GUI.color = oldColor;
            GUI.Label(new Rect(rect.x + 8, rect.yMax - 23, rect.width - 16, 20),
                profile.HasVisibleChamfer(controller.ExposedEnd) ? "경사 있음" : "경사 없음", caption);
        }

        private void DrawHandwheelPanel()
        {
            // A single bounded control panel below the camera. Every child uses
            // panel-local coordinates so titles, wheels and help share its padding.
            GUI.Box(new Rect(400, 642, 840, 154), GUIContent.none);
            if (!controller.Tailstock.Active) DrawDial(new Rect(408, 648, 164, 142), Axis.X, "X · 가로 이송");
            DrawDial(new Rect(582, 648, 164, 142), Axis.Z, controller.Tailstock.Active ? "퀼 Z" : "Z · 길이 이송", controller.Tailstock.Active);
            if (controller.Tailstock.Active) { DrawQuillReadout(); DrawFixedSpindle(); return; }
            var hint = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            GUI.Label(new Rect(760, 670, 250, 24), "이동 배율 ×" + (int)controller.Scale, hint);
            GUI.Label(new Rect(760, 694, 250, 22), "X 표시  " + controller.XRelativeDialReading.ToString("+0.###;-0.###;0") + " mm", hint);
            GUI.Label(new Rect(760, 716, 250, 22), "Z 표시  " + controller.ZRelativeDialReading.ToString("+0.###;-0.###;0") + " mm", hint);
            GUI.Label(new Rect(760, 742, 250, 48), controller.SelectedTool == ToolKind.Chamfer
                ? "45° 날을 모서리에 접촉시켜 이송하세요.\n깊게 이송할수록 더 많이 깎입니다."
                : "휠을 잡고 돌려 이동하세요.\n주축을 멈춘 뒤 측정하여 다음 가공을 판단하세요.", hint);
            DrawFixedSpindle();
        }
        private void DrawDial(Rect rect, Axis axis, string title, bool quill = false)
        {
            GUI.Box(rect, GUIContent.none);
            // The Wheel_Disc and the hit circle deliberately share this enlarged area.
            var wheelArea = new Rect(rect.x + 4, rect.y + 22, rect.width - 8, rect.height - 26);
            var center = wheelArea.center;
            float radius = Mathf.Min(wheelArea.width, wheelArea.height) * 0.5f - 4;
            float rotation = quill ? controller.Tailstock.HandwheelDegrees-controller.Tailstock.ZeroDegrees : axis == Axis.X ? controller.XHandwheelDisplayDegrees : controller.ZHandwheelDisplayDegrees;
            var art = EnvironmentArtDefinition.LoadDefault();
            var imageRect = new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2);
            Color light = new Color(0.72f, 0.76f, 0.78f);
            if (art != null && art.handwheelBase != null)
                GUI.DrawTexture(imageRect, art.handwheelBase, ScaleMode.ScaleToFit, true);
            if (art != null && art.handwheel != null)
            {
                var matrix = GUI.matrix;
                // Rotate in the same authored coordinate space as the image.
                // Nested GUI groups + RotateAroundPivot mixed clip and scaled coordinates.
                GUI.matrix = matrix * Matrix4x4.Translate(center) *
                    Matrix4x4.Rotate(Quaternion.Euler(0, 0, rotation)) * Matrix4x4.Translate(-center);
                var pivot = art.handwheelPivot;
                var wheelRect = new Rect(center.x - imageRect.width * pivot.x,
                    center.y - imageRect.height * pivot.y, imageRect.width, imageRect.height);
                GUI.DrawTexture(wheelRect, art.handwheel, ScaleMode.ScaleToFit, true);
                GUI.matrix = matrix;
            }
            else GUI.Label(imageRect, "휠 이미지 없음");
            DrawFixedPointer(center, radius);
            var caption = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            caption.normal.textColor = controller.SelectedAxis == axis ? new Color(0.91f, 0.72f, 0.38f) : light;
            GUI.Label(new Rect(rect.x + 8, rect.y + 6, rect.width - 70, 20), title, caption);
            if (GUI.Button(new Rect(rect.xMax - 62, rect.y + 4, 58, 20), "영점 설정"))
                { if(quill) router.ZeroQuill(); else router.SetDialZero(axis); }
            int control = GUIUtility.GetControlID(axis == Axis.X ? 7101 : 7102, FocusType.Passive);
            var evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition) && Vector2.Distance(center, evt.mousePosition) <= radius + 7)
            {
                GUIUtility.hotControl = control;
                router.SelectAxis(axis);
                activeDial = axis == Axis.X ? 0 : 1;
                previousDialAngle = LatheDialMath.Angle(center, evt.mousePosition);
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && activeDial == (axis == Axis.X ? 0 : 1))
            {
                float current = LatheDialMath.Angle(center, evt.mousePosition);
                float delta = LatheDialMath.SafeDeltaAngle(previousDialAngle, current);
                previousDialAngle = current;
                if (Mathf.Abs(delta) > 0.01f) { if(quill) router.RotateQuill(delta); else router.ApplyHandwheelRotation(axis, delta); }
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && activeDial == (axis == Axis.X ? 0 : 1))
            {
                activeDial = -1;
                GUIUtility.hotControl = 0;
                evt.Use();
            }
        }
        // Drawn after restoring GUI.matrix, therefore it is a sibling of Wheel_Disc and never rotates.
        private static void DrawFixedPointer(Vector2 center, float radius)
        {
            // A short index at 12 o'clock points to the outer tick ring only.
            // It intentionally does not reuse the supplied long-arm pointer art, which crossed the disc.
            var old = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(center.x - 1.2f, center.y - radius - 7, 2.4f, 9), Texture2D.whiteTexture);
            GUI.color = old;
        }
        private static void DialRing(Vector2 center, float radius, float width, Color color)
        {
            for (int i = 0; i < 64; i++)
            {
                float a = i * Mathf.PI * 2 / 64, b = (i + 1) * Mathf.PI * 2 / 64;
                DialLine(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, width, color);
            }
        }
        private static void DialLine(Vector2 from, Vector2 to, float width, Color color)
        {
            var matrix = GUI.matrix; var old = GUI.color;
            var delta = to - from;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
            GUI.color = color;
            GUI.DrawTexture(new Rect(from.x, from.y - width / 2, delta.magnitude, width), Texture2D.whiteTexture);
            GUI.color = old; GUI.matrix = matrix;
        }
    }
}

