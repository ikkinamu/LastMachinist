using UnityEngine;

namespace LastMachinist.Core
{
    // Small binding asset in Resources; source images remain in Art/External.
    public sealed class EnvironmentArtDefinition : ScriptableObject
    {
        public Texture2D workshop;
        public Texture2D farm;
        public Texture2D pumpBroken;
        public Texture2D pumpRepaired;
        public Texture2D handwheel;
        public Texture2D handwheelBase;
        public Texture2D handwheelPointer;
        // Top-left normalized coordinates of the shaft hole in the supplied PNG.
        public Vector2 handwheelPivot = new Vector2(0.491f, 0.502f);
        public static EnvironmentArtDefinition LoadDefault()
            => Resources.Load<EnvironmentArtDefinition>("EnvironmentArt");
    }
}
