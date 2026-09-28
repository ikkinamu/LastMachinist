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
        public static bool Enter(ExplorationRegionDefinition region, LathePrototype station)
        {
            if (IsExploring || region == null || station == null || station.DialogueBlocked ||
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
            Block("Obstacle A", new Vector3(-3, 1, 0), new Vector3(3, 2, 3));
            Block("Obstacle B", new Vector3(4, 1.5f, 3), new Vector3(2, 3, 5));
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
            string sessionId = Guid.NewGuid().ToString("N");
            foreach (var placement in region.Items)
            {
                if (placement == null || placement.Definition == null) continue;
                var item = GameObject.CreatePrimitive(placement.Shape);
                item.name = placement.Definition.DisplayName;
                item.transform.position = placement.Position;
                item.transform.localScale = placement.Scale;
                item.GetComponent<Renderer>().sharedMaterial = grey;
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
        public void ReturnToWorkshop()
        {
            if (returning) return;
            returning = true;
            if (Player != null) Player.gameObject.SetActive(false);
            StartCoroutine(ReturnRoutine());
        }
        private IEnumerator ReturnRoutine()
        {
            if (workshop.IsValid() && workshop.isLoaded) SceneManager.SetActiveScene(workshop);
            if (exploration.IsValid() && exploration.isLoaded) yield return SceneManager.UnloadSceneAsync(exploration);
            foreach (var root in suspendedRoots) if (root != null) root.SetActive(true);
            if (lathe != null) lathe.OpenPage(HudPage.Workshop);
            Cursor.lockState = previousLock; Cursor.visible = previousCursor;
            if (grey != null) Destroy(grey);
            current = null;
            Destroy(gameObject);
        }
        private void OnGUI()
        {
            if (returning || Player == null) return;
            var matrix = GUI.matrix; GUI.matrix = MvpUiScale.Apply1280x800();
            GUI.Box(new Rect(16, 16, 500, 64), regionName + " · Greybox\nWASD 이동 / 마우스 시점 / 좌클릭 조사 / Esc 메뉴");
            GUI.Label(new Rect(16, 88, 500, 30), "이번 탐색에서 회수: " + (Inspection?.RecoveredItemIds.Count ?? 0) + "개");
            if (Inspection != null && Inspection.IsOpen) Inspection.DrawWindow();
            else if (Player.MenuOpen)
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
