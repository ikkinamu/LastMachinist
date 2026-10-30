using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using LastMachinist.Machining.Turning;

namespace LastMachinist.Core
{
    // Keeps the workshop loaded, including its unsaved domain state, during exploration.
    public sealed class ExplorationSession : MonoBehaviour
    {
        public static event Action AutoSaveRequested;
        public static void NotifyRecovery() => AutoSaveRequested?.Invoke();
        private static ExplorationSession current;
        private readonly List<GameObject> suspendedRoots = new List<GameObject>();
        private Scene workshop, exploration;
        private LathePrototype lathe;
        private Material grey;
        private bool returning;
        private string regionName;
        private CursorLockMode previousLock;
        private bool previousCursor;
        public FirstPersonExplorer Player { get; private set; }
        public ExplorationInspection Inspection { get; private set; }
        public static bool IsExploring => current != null;
        public static bool RequestWorkshopReturn()
        {
            if (current == null || current.returning) return false;
            current.ReturnToWorkshop(); return true;
        }
        public static bool Enter(ExplorationRegionDefinition region, LathePrototype station)
        {
            if (IsExploring || region == null || station == null || station.DialogueBlocked ||
                !station.ExplorationUnlocked ||
                station.RegionProgression == null || !station.RegionProgression.CanEnter(region.RegionId)) return false;
            var host = new GameObject("Exploration session");
            DontDestroyOnLoad(host);
            current = host.AddComponent<ExplorationSession>();
            current.lathe = station;
            current.workshop = station.gameObject.scene;
            current.previousLock = Cursor.lockState; current.previousCursor = Cursor.visible;
            try {
                current.Build(region);
                station.RegionProgression.DiscoverRegion(region.RegionId, RegionDiscoveryState.Discovered);
                AutoSaveRequested?.Invoke();
                return true;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                current.ReturnToWorkshop();
                return false;
            }
        }
        private void Build(ExplorationRegionDefinition region)
        {
            regionName = region.DisplayName;
            // This small greybox is generated into a real separate Scene at entry.
            // No build-index strings or duplicated region identifiers are needed.
            exploration = SceneManager.CreateScene(region.SceneName);
            SceneManager.SetActiveScene(exploration);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            grey = new Material(shader) { color = new Color(0.48f, 0.53f, 0.57f) };
            Block("Floor", new Vector3(0, -0.5f, 0), new Vector3(24, 1, 24));
            Block("North boundary", new Vector3(0, 2, 12), new Vector3(25, 4, 1));
            Block("South boundary", new Vector3(0, 2, -12), new Vector3(25, 4, 1));
            Block("West boundary", new Vector3(-12, 2, 0), new Vector3(1, 4, 24));
            Block("East boundary", new Vector3(12, 2, 0), new Vector3(1, 4, 24));
            if (region.EnvironmentPrefab != null) Instantiate(region.EnvironmentPrefab);
            else if (region.RegionId == RegionProgression.AbandonedWorkshopId)
            {
                // Replaceable blockout: entrance, workshop bay, workbench and scrap storage.
                Block("Workshop left wall", new Vector3(-7, 2, 5), new Vector3(.4f, 4, 12));
                Block("Workshop right wall", new Vector3(7, 2, 5), new Vector3(.4f, 4, 12));
                Block("Workshop rear wall", new Vector3(0, 2, 10), new Vector3(14, 4, .4f));
                Block("Entrance left", new Vector3(-4.5f, 2, -1), new Vector3(5, 4, .4f));
                Block("Entrance right", new Vector3(4.5f, 2, -1), new Vector3(5, 4, .4f));
                Block("Workbench", new Vector3(0, .45f, 6), new Vector3(3, .9f, 1.5f));
                Block("Storage shelf", new Vector3(-5, 1.5f, 8), new Vector3(2, .2f, 1));
                Block("Scrap bin", new Vector3(-8, .4f, -3), new Vector3(2, .8f, 2));
            }
            else
            {
                Block("Obstacle A", new Vector3(-3, 1, 0), new Vector3(3, 2, 3));
                Block("Obstacle B", new Vector3(4, 1.5f, 3), new Vector3(2, 3, 5));
            }
            var spawn = new GameObject("Player Spawn Point"); spawn.transform.position = region.SpawnPosition;
            var light = new GameObject("Exploration light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.3f; light.transform.rotation = Quaternion.Euler(50, -30, 0);
            var player = new GameObject("First person player"); player.transform.position = spawn.transform.position;
            Player = player.AddComponent<FirstPersonExplorer>();
            var camera = new GameObject("First person camera").AddComponent<Camera>();
            camera.transform.SetParent(player.transform, false); camera.transform.localPosition = new Vector3(0, 1.6f, 0);
            camera.orthographic = false; camera.fieldOfView = 70; camera.nearClipPlane = 0.05f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.12f, 0.18f, 0.25f);
            camera.gameObject.AddComponent<AudioListener>();
            lathe.Router.SetActive(false);
            foreach (var root in workshop.GetRootGameObjects())
                if (root.activeSelf) { suspendedRoots.Add(root); root.SetActive(false); }
            Player.Initialize(region, camera.transform);
            Inspection = player.AddComponent<ExplorationInspection>();
            Inspection.Initialize(Player, camera, region.InteractionRange, lathe.PlayerInventory);
            Inspection.ItemInspected = item => {
                if (item.Definition.ItemId == LastMachinist.Quest.WorkshopStory.BenchDiscoveryId && lathe.WorkshopStory.DiscoverBench())
                    lathe.RegionProgression.RecordMajorDiscovery(region.RegionId, LastMachinist.Quest.WorkshopStory.BenchDiscoveryId);
            };
            Inspection.RecoverEquipment = item => lathe.WorkshopStory.RecoverBench();
            string sessionId = Guid.NewGuid().ToString("N");
            foreach (var placement in region.Items)
            {
                if (placement == null || placement.Definition == null) continue;
                if (placement.Definition.ItemId == LastMachinist.Quest.WorkshopStory.BenchDiscoveryId && lathe.WorkshopStory.Data.BenchAcquired) continue;
                var item = GameObject.CreatePrimitive(placement.Shape);
                item.name = placement.Definition.DisplayName;
                item.transform.position = placement.Position;
                item.transform.localScale = placement.Scale;
                item.GetComponent<Renderer>().sharedMaterial = grey;
                if (placement.Definition.VisualPrefab != null)
                {
                    item.GetComponent<Renderer>().enabled = false;
                    Instantiate(placement.Definition.VisualPrefab, item.transform, false);
                }
                else if (placement.Definition.ItemId == LastMachinist.Quest.WorkshopStory.BenchDiscoveryId)
                {
                    item.GetComponent<Renderer>().enabled = false;
                    DrillPart(item.transform, "Base", new Vector3(0, -.4f, 0), new Vector3(.8f, .1f, .8f));
                    DrillPart(item.transform, "Column", new Vector3(0, 0, .25f), new Vector3(.12f, .85f, .12f));
                    DrillPart(item.transform, "Head", new Vector3(0, .35f, 0), new Vector3(.5f, .18f, .7f));
                    DrillPart(item.transform, "Chuck", new Vector3(0, .2f, -.2f), new Vector3(.16f, .15f, .16f));
                    DrillPart(item.transform, "Bit", new Vector3(0, .06f, -.2f), new Vector3(.04f, .15f, .04f));
                    DrillPart(item.transform, "Table", new Vector3(0, -.12f, -.1f), new Vector3(.6f, .08f, .5f));
                }
                var inspectable = item.AddComponent<InspectableItem>();
                inspectable.Initialize(placement.Definition);
                if (placement.Definition.Workpiece != null)
                    inspectable.BindInstance(placement.Definition.Workpiece.CreateInstance(placement.Conditions, region.RegionId, sessionId));
            }
        }
        private void Block(string name, Vector3 position, Vector3 size)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name; block.transform.position = position; block.transform.localScale = size;
            block.GetComponent<Renderer>().sharedMaterial = grey;
        }
        private void DrillPart(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name; part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = grey;
            Destroy(part.GetComponent<Collider>()); // Parent owns the single investigation collider.
        }
        public void ReturnToWorkshop()
        {
            if (returning) return;
            returning = true;
            // Recovery writes to persistent inventory synchronously in Recover().
            // Closing inspection does not repeat the inventory transaction.
            Inspection?.Close();
            if (Player != null) Player.gameObject.SetActive(false);
            StartCoroutine(ReturnRoutine());
        }
        private IEnumerator ReturnRoutine()
        {
            if (workshop.IsValid() && workshop.isLoaded) SceneManager.SetActiveScene(workshop);
            if (exploration.IsValid() && exploration.isLoaded) yield return SceneManager.UnloadSceneAsync(exploration);
            foreach (var root in suspendedRoots) if (root != null) root.SetActive(true);
            if (lathe != null) lathe.OpenPage(HudPage.Workshop);
            current = null;
            AutoSaveRequested?.Invoke();
            Cursor.lockState = previousLock; Cursor.visible = previousCursor;
            if (grey != null) Destroy(grey);
            current = null;
            Destroy(gameObject);
        }
        private void OnGUI()
        {
            if (returning || Player == null || GameplaySystemMenu.BlocksGameplay) return;
            var matrix = GUI.matrix; GUI.matrix = MvpUiScale.Apply1280x800();
            GUI.Box(new Rect(16, 16, 500, 64), regionName + " · Greybox\nWASD 이동 / 마우스 시점 / 좌클릭 조사 / Esc 메뉴");
            GUI.Label(new Rect(16, 88, 500, 30), "이번 탐색에서 회수: " + (Inspection?.RecoveredItemIds.Count ?? 0) + "개");
            if (Inspection != null && Inspection.IsOpen) Inspection.DrawWindow();
            else if (Player.MenuOpen && GameplaySystemMenu.Current == null)
            {
                var panel = new Rect(440, 280, 400, 220);
                MvpUiScale.DrawOpaqueBackground(panel);
                GUI.Label(new Rect(456, 300, 368, 40), "탐색 메뉴");
                if (GUI.Button(new Rect(456, 352, 368, 42), "탐색 계속")) Player.SetMenu(false);
                if (GUI.Button(new Rect(456, 410, 368, 42), "작업장 복귀")) ReturnToWorkshop();
            }
            else GUI.Label(new Rect(632, 392, 16, 16), "+");
            GUI.matrix = matrix;
        }
    }
}
