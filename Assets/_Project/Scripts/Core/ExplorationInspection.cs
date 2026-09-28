using System.Collections.Generic;
using UnityEngine;

namespace LastMachinist.Core
{
    public sealed class ExplorationInspection : MonoBehaviour
    {
        private readonly ExplorationRecoveryState recovered = new ExplorationRecoveryState();
        private FirstPersonExplorer player;
        private Camera view;
        private float range;
        private LastMachinist.Inventory.InventoryState inventory;
        private Vector2 scroll;
        public InspectableItem Target { get; private set; }
        public IReadOnlyCollection<string> RecoveredItemIds => recovered.ItemIds;
        public bool IsOpen => Target != null;

        public void Initialize(FirstPersonExplorer explorer, Camera camera, float interactionRange, LastMachinist.Inventory.InventoryState playerInventory = null)
        {
            player = explorer; view = camera; range = Mathf.Max(0, interactionRange);
            inventory = playerInventory;
            player.InteractRequested += Inspect;
            player.InspectionCloseRequested += Close;
        }

        public void Inspect()
        {
            if (player == null || !player.CanInteract || IsOpen || view == null) return;
            // The nearest collider wins, including walls and non-inspectable props.
            if (!Physics.Raycast(view.ViewportPointToRay(new Vector3(.5f, .5f, 0)), out var hit,
                    range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            var item = hit.collider.GetComponentInParent<InspectableItem>();
            if (item == null || item.Definition == null || !item.isActiveAndEnabled ||
                string.IsNullOrWhiteSpace(item.RecoveryKey) || recovered.Contains(item.RecoveryKey)) return;
            Target = item; scroll = Vector2.zero;
            player.SetInspectionOpen(true);
        }

        public bool Recover()
        {
            if (Target == null || !Target.isActiveAndEnabled || Target.Definition == null) return false;
            var data = Target.Definition;
            string recoveryKey = Target.RecoveryKey;
            if (!data.CanRecover || string.IsNullOrWhiteSpace(recoveryKey) || recovered.Contains(recoveryKey)) return false;
            // A failed inventory write leaves both the world object and session log untouched.
            if (data.Workpiece != null && (Target.Instance == null || inventory == null || !inventory.AddWorkpiece(Target.Instance))) return false;
            if (!recovered.TryRecord(recoveryKey)) return false;
            Target.gameObject.SetActive(false);
            Close();
            return true;
        }

        public void Close()
        {
            Target = null;
            if (player != null) player.SetInspectionOpen(false);
        }

        private void Update()
        {
            if (player != null && player.InspectionOpen &&
                (Target == null || !Target.isActiveAndEnabled || Target.Definition == null)) Close();
        }

        // Existing project uses IMGUI, not Canvas/EventSystem pointer routing.
        public void DrawWindow()
        {
            if (!IsOpen) return;
            var data = Target.Definition;
            MvpUiScale.DrawOpaqueBackground(new Rect(320, 160, 640, 480));
            var title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, wordWrap = true };
            var text = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
            GUI.Label(new Rect(344, 180, 592, 72), data.DisplayName, title);
            GUILayout.BeginArea(new Rect(344, 260, 592, 280));
            scroll = GUILayout.BeginScrollView(scroll);
            if (data.Workpiece != null && data.Workpiece.BaseInspectionProperties != null)
                foreach (var property in data.Workpiece.BaseInspectionProperties)
                    if (property != null) GUILayout.Label(property.PropertyLabel + ": " + property.PropertyValue, text);
            foreach (var property in data.InspectionProperties)
                if (property != null) GUILayout.Label(property.PropertyLabel + ": " + property.PropertyValue, text);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.enabled = data.CanRecover;
            bool recover = GUI.Button(new Rect(344, 566, 280, 50), "회수");
            GUI.enabled = true;
            bool close = GUI.Button(new Rect(656, 566, 280, 50), "닫기");
            // Apply after drawing so a cleared Target cannot invalidate this GUI pass.
            if (recover) Recover();
            else if (close) Close();
        }

        private void OnDestroy()
        {
            if (player == null) return;
            player.InteractRequested -= Inspect;
            player.InspectionCloseRequested -= Close;
        }
    }
}
