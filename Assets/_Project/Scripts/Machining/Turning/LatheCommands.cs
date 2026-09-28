namespace LastMachinist.Machining.Turning
{
    public enum Axis { X, Z }
    public enum MovementScale { Fine = 1, Medium = 10, Coarse = 100 }
    public enum LatheMode { Setup, Ready, SpindleRunning, Machining, Paused }
    public enum ToolKind { Roughing, Chamfer, Boring, Finishing, Parting }
    public enum LatheCommandKind { SelectAxis, SetScale, Move, SetSpindle, SetTool, SelectTool, Reclamp, ApplyChamfer, SetActive, SetDialZero, SelectTailstockTool, FeedQuill, MoveTailstock, SetTailstockActive, RotateQuill, ZeroQuill }

    public readonly struct LatheCommand
    {
        public readonly LatheCommandKind Kind;
        public readonly Axis Axis;
        public readonly float Value;
        public LatheCommand(LatheCommandKind kind, float value, Axis axis = Axis.X)
        { Kind = kind; Value = value; Axis = axis; }
    }

    // All input sources use this facade; only the controller decides whether a move cuts.
    public sealed class LatheInputRouter
    {
        private readonly LatheController controller;
        public LatheInputRouter(LatheController controller) { this.controller = controller; }
        // Read-only presentation state used when restoring or switching pages.
        public bool IsActive => controller != null && controller.Active;
        public void SelectAxis(Axis axis) => controller.Execute(new LatheCommand(LatheCommandKind.SelectAxis, 0, axis));
        public void SetMovementScale(MovementScale scale) => controller.Execute(new LatheCommand(LatheCommandKind.SetScale, (int)scale));
        // Degrees are the common input unit: 90° = 1 mm at ×1.
        public void ApplyHandwheelRotation(Axis axis, float rotationDegrees) => controller.Execute(new LatheCommand(LatheCommandKind.Move, rotationDegrees, axis));
        public void SetDialZero(Axis axis) => controller.Execute(new LatheCommand(LatheCommandKind.SetDialZero, 0, axis));
        // Kept for existing callers; the value is rotation degrees, not a separate movement rule.
        public void ApplyHandwheelDelta(Axis axis, float rotationDegrees) => ApplyHandwheelRotation(axis, rotationDegrees);
        public void ApplyMouseWheel(float wheelUnits, float unitsPerDetent)
            => Scroll(LatheDialMath.MouseWheelToDegrees(wheelUnits, unitsPerDetent));
        public void Scroll(float rotationDegrees) { if(controller.Tailstock.Active) RotateQuill(rotationDegrees); else ApplyHandwheelRotation(controller.SelectedAxis, rotationDegrees); }
        public void SetSpindleRunning(bool running) => controller.Execute(new LatheCommand(LatheCommandKind.SetSpindle, running ? 1 : 0));
        public void SetToolMounted(bool mounted) => controller.Execute(new LatheCommand(LatheCommandKind.SetTool, mounted ? 1 : 0));
        public void SelectTool(ToolKind tool) => controller.Execute(new LatheCommand(LatheCommandKind.SelectTool, (int)tool));
        public void RequestReclamp() => controller.Execute(new LatheCommand(LatheCommandKind.Reclamp, 1));
        public void ApplyChamfer(float width) => controller.Execute(new LatheCommand(LatheCommandKind.ApplyChamfer, width));
        public void SetActive(bool active) => controller.Execute(new LatheCommand(LatheCommandKind.SetActive, active ? 1 : 0));
        public void SelectTailstockTool(TailstockToolKind tool) => controller.Execute(new LatheCommand(LatheCommandKind.SelectTailstockTool, (int)tool));
        public void FeedQuill(float millimetres) => controller.Execute(new LatheCommand(LatheCommandKind.FeedQuill, millimetres));
        public void RotateQuill(float degrees) => controller.Execute(new LatheCommand(LatheCommandKind.RotateQuill,degrees));
        public void ZeroQuill() => controller.Execute(new LatheCommand(LatheCommandKind.ZeroQuill,0));
        public void MoveTailstock(float millimetres) => controller.Execute(new LatheCommand(LatheCommandKind.MoveTailstock, millimetres));
        public void SetTailstockActive(bool active) => controller.Execute(new LatheCommand(LatheCommandKind.SetTailstockActive, active ? 1 : 0));
    }
}
