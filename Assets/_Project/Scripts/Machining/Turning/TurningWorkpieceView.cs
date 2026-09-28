using UnityEngine;

namespace LastMachinist.Machining.Turning
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TurningWorkpieceView : MonoBehaviour
    {
        private TurningProfile profile;
        private Mesh mesh;
        public int Revision { get; private set; }
        public void Bind(TurningProfile value)
        {
            if (profile != null) profile.Changed -= Refresh;
            profile = value;
            if (mesh == null) { mesh = new Mesh { name = "Runtime turning profile" }; GetComponent<MeshFilter>().sharedMesh = mesh; }
            profile.Changed += Refresh;
            Refresh();
        }
        private void Refresh() { TurningMeshBuilder.Rebuild(mesh, profile); Revision++; }
        private void OnDestroy()
        {
            if (profile != null) profile.Changed -= Refresh;
            if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }
    }
}
