namespace LastMachinist.Machining.Turning
{
    public enum TailstockToolKind { None, CenterDrill, Drill10, Drill16 }
    public enum DrillVisualKind { SteppedCenter, StraightDrill }
    public sealed class DrillDefinition
    {
        public string ToolId { get; }
        public string DisplayName { get; }
        public float Diameter { get; }
        public float MaximumDepth { get; }
        public TailstockToolKind ToolType { get; }
        public DrillVisualKind Visual { get; }
        public DrillDefinition(string id, string name, float diameter, float maxDepth, TailstockToolKind type)
        { ToolId=id; DisplayName=name; Diameter=diameter; MaximumDepth=maxDepth; ToolType=type;
            Visual=type==TailstockToolKind.CenterDrill?DrillVisualKind.SteppedCenter:DrillVisualKind.StraightDrill; }
    }
    public static class TailstockTools
    {
        public static readonly DrillDefinition Center = new DrillDefinition("center_drill", "센터드릴 Ø4", 4, 3, TailstockToolKind.CenterDrill);
        public static readonly DrillDefinition Drill10 = new DrillDefinition("drill_10", "드릴 Ø10", 10, 80, TailstockToolKind.Drill10);
        public static readonly DrillDefinition Drill16 = new DrillDefinition("drill_16", "드릴 Ø16", 16, 80, TailstockToolKind.Drill16);
        public const string BoringId = "boring_tool";
        public static DrillDefinition Find(TailstockToolKind kind) => kind == TailstockToolKind.CenterDrill ? Center : kind == TailstockToolKind.Drill10 ? Drill10 : kind == TailstockToolKind.Drill16 ? Drill16 : null;
    }
    public sealed class TailstockState
    {
        public bool Active { get; internal set; }
        public TailstockToolKind MountedTool { get; internal set; }
        public float AxialPosition { get; internal set; }
        public float QuillExtension { get; internal set; }
        public float HandwheelDegrees { get; internal set; }
        public float ZeroDegrees { get; internal set; }
        public float ZeroExtension { get; internal set; }
        public float TipPosition => AxialPosition - QuillExtension;
        public DrillDefinition Definition => TailstockTools.Find(MountedTool);
    }
}
