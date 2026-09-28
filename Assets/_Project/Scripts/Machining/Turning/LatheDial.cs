using System;
using UnityEngine;

namespace LastMachinist.Machining.Turning
{
    public static class LatheDialMath
    {
        public const float DegreesPerMillimetre = 90f;
        public const float MillimetresPerStep = 1f;
        public static float DegreesToMillimetres(float degrees, MovementScale scale)
            => degrees / DegreesPerMillimetre * MillimetresPerStep * (int)scale;
        public static float MillimetresToDegrees(float millimetres, MovementScale scale)
            => millimetres / ((int)scale * MillimetresPerStep) * DegreesPerMillimetre;
        public static float MouseWheelToDegrees(float wheelUnits, float unitsPerDetent)
        {
            if (unitsPerDetent <= 0 || float.IsNaN(unitsPerDetent) || float.IsInfinity(unitsPerDetent))
                throw new ArgumentOutOfRangeException(nameof(unitsPerDetent));
            return wheelUnits / unitsPerDetent * DegreesPerMillimetre;
        }
        public static float Angle(Vector2 center, Vector2 pointer)
            => Mathf.Atan2(pointer.y - center.y, pointer.x - center.x) * Mathf.Rad2Deg;
        public static float SafeDeltaAngle(float previous, float current) => Mathf.DeltaAngle(previous, current);
    }

    // Pointer position -> angle delta -> common router command. It contains no machining rules.
    public sealed class LatheDial
    {
        private readonly LatheInputRouter router;
        private readonly Axis axis;
        private Vector2 center;
        private float previousAngle;
        private bool dragging;
        public LatheDial(LatheInputRouter router, Axis axis) { this.router = router ?? throw new ArgumentNullException(nameof(router)); this.axis = axis; }
        public void BeginDrag(Vector2 dialCenter, Vector2 pointer)
        { center = dialCenter; previousAngle = LatheDialMath.Angle(center, pointer); dragging = true; }
        public void Drag(Vector2 pointer)
        {
            if (!dragging) return;
            float current = LatheDialMath.Angle(center, pointer);
            float delta = LatheDialMath.SafeDeltaAngle(previousAngle, current);
            previousAngle = current;
            if (Mathf.Abs(delta) > 0.001f) router.ApplyHandwheelRotation(axis, delta);
        }
        public void EndDrag() { dragging = false; }
    }
}
