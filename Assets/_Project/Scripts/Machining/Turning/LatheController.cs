using System;
using LastMachinist.Machining.Core;

namespace LastMachinist.Machining.Turning
{
    // Pure C# domain. X is radial distance; Z is workpiece-local until Phase 3.
    public sealed partial class LatheController
    {
        private readonly WorkpieceState workpiece;
        private readonly RoughingToolState availableTool;
        private readonly ChamferToolState availableChamfer;
        private readonly float gripLength, maxX, maxZ;
        public Axis SelectedAxis { get; private set; } = Axis.X;
        public MovementScale Scale { get; private set; } = MovementScale.Medium;
        public float ToolX { get; private set; }
        public float ToolZ { get; private set; }
        public bool SpindleRunning { get; private set; }
        public bool ToolMounted { get; private set; }
        public bool Active { get; private set; } = true;
        public bool LastMoveRemovedMaterial { get; private set; }
        public float XHandwheelDegrees { get; private set; }
        public float ZHandwheelDegrees { get; private set; }
        public float XDisplayZeroOffset { get; private set; }
        public float ZDisplayZeroOffset { get; private set; }
        private float xDisplayZeroDegrees, zDisplayZeroDegrees;
        public float XRelativeDialReading => ToolX - XDisplayZeroOffset;
        public float ZRelativeDialReading => ToolZ - ZDisplayZeroOffset;
        public float XHandwheelDisplayDegrees => XHandwheelDegrees - xDisplayZeroDegrees;
        public float ZHandwheelDisplayDegrees => ZHandwheelDegrees - zDisplayZeroDegrees;
        public RoughingToolState Tool => ToolMounted && (SelectedTool == ToolKind.Roughing || SelectedTool == ToolKind.Finishing) ? availableTool : null;
        public ToolKind SelectedTool { get; private set; } = ToolKind.Roughing;
        public string CurrentToolId => !ToolMounted ? null : SelectedTool == ToolKind.Parting ? "parting_tool" : SelectedTool == ToolKind.Finishing ? "finishing_tool" : SelectedTool == ToolKind.Boring ? TailstockTools.BoringId : SelectedTool == ToolKind.Chamfer ? availableChamfer?.Id : availableTool?.Id;
        public string CurrentToolDisplayName => !ToolMounted ? null : SelectedTool == ToolKind.Parting ? "절단 바이트" : SelectedTool == ToolKind.Finishing ? "정삭 바이트" : SelectedTool == ToolKind.Boring ? "내경 바이트" : SelectedTool == ToolKind.Chamfer ? availableChamfer?.DisplayName : availableTool?.DisplayName;
        public bool HasChamferTool => availableChamfer != null;
        public Orientation Orientation => workpiece?.Orientation ?? Orientation.Normal;
        public bool IsMounted => workpiece != null;
        public TurningProfile Profile => workpiece?.Geometry.RoundProfile;
        public WorkpieceState Workpiece => workpiece;
        public LatheMode Mode => !Active ? LatheMode.Paused : Profile == null || !ToolMounted ? LatheMode.Setup :
            !SpindleRunning ? LatheMode.Ready : LastMoveRemovedMaterial ? LatheMode.Machining : LatheMode.SpindleRunning;

        public float ExposedEnd => Orientation == Orientation.Normal ? Profile.TotalLength : 0;
        public LatheController(WorkpieceState workpiece, RoughingToolState tool, float baseStep = 0.05f,
            float initialX = 30, float initialZ = 80, float gripLength = 15, ChamferToolState chamfer = null, bool supportsParting = false)
        {
            workpiece?.Validate();
            if (!Finite(baseStep) || baseStep <= 0 || !Finite(initialX) || initialX < 0 ||
                !Finite(initialZ) || !Finite(gripLength) || gripLength < 0 || initialZ < gripLength ||
                (workpiece != null && gripLength >= workpiece.Geometry.RoundProfile.TotalLength))
                throw new ArgumentException("Invalid lathe setup.");
            this.workpiece = workpiece; availableTool = tool; availableChamfer = chamfer; this.gripLength = gripLength;
            SupportsParting=supportsParting;
            ToolMounted = tool != null; ToolX = initialX; ToolZ = initialZ;
            XDisplayZeroOffset = initialX; ZDisplayZeroOffset = initialZ;
            maxX = Math.Max(50, initialX);
            maxZ = Math.Max(initialZ, (Profile?.TotalLength ?? 100) + 10); Tailstock.AxialPosition = (Profile?.TotalLength ?? 100) + 8;
        }

        public void Execute(LatheCommand command)
        {
            if (!Finite(command.Value)) throw new ArgumentException("Non-finite command.");
            LastMoveRemovedMaterial = false;
            if (ExecuteInternalCommand(command)) return;
            switch (command.Kind)
            {
                case LatheCommandKind.SelectAxis:
                    CheckAxis(command.Axis); SelectedAxis = command.Axis; break;
                case LatheCommandKind.SetScale:
                    if (command.Value != 1 && command.Value != 10 && command.Value != 100)
                        throw new ArgumentException("Invalid movement scale.");
                    Scale = (MovementScale)(int)command.Value; break;
                case LatheCommandKind.SetActive:
                    Active = command.Value != 0;
                    if (!Active) SpindleRunning = false;
                    break;
                case LatheCommandKind.SetSpindle:
                    SpindleRunning = Active && Profile != null && command.Value != 0;
                    Cut(ToolX, ToolZ, ToolZ); break;
                case LatheCommandKind.SetTool:
                    if (!SpindleRunning) ToolMounted = command.Value != 0 && (SelectedTool == ToolKind.Boring || (SelectedTool == ToolKind.Chamfer ? availableChamfer != null : availableTool != null));
                    break;
                case LatheCommandKind.SelectTool:
                    if (SpindleRunning) { StatusMessage = "주축을 정지한 뒤 공구 변경·재고정을 하세요."; return; }
                    if (!Enum.IsDefined(typeof(ToolKind),(int)command.Value) || command.Value!=(int)command.Value)
                        throw new ArgumentException("Invalid tool.");
                    if((ToolKind)(int)command.Value==ToolKind.Parting && !SupportsParting) { StatusMessage="절단은 인벤토리 공작물 가공에서 사용하세요."; return; }
                    if (!CanRetractTailstock()) return; Tailstock.Active = false; boringEntered = false; SelectedTool = (ToolKind)(int)command.Value; StatusMessage = "공구를 선택했습니다.";
                    ToolMounted = SelectedTool == ToolKind.Boring || (SelectedTool == ToolKind.Chamfer ? availableChamfer != null : availableTool != null);
                    break;
                case LatheCommandKind.Reclamp:
                    if (SpindleRunning) { StatusMessage = "주축을 정지한 뒤 공구 변경·재고정을 하세요."; return; }
                    if (workpiece == null || !CanRetractTailstock()) return; boringEntered = false;
                    workpiece.Orientation = workpiece.Orientation == Orientation.Normal ? Orientation.Reversed : Orientation.Normal;
                    ToolZ = Math.Max(gripLength + (Tool?.Width ?? 0), Math.Min(maxZ, ToolZ));
                    break;
                case LatheCommandKind.ApplyChamfer:
                    // Retained enum/API for source compatibility; fixed-feature gameplay is retired.
                    break;
                case LatheCommandKind.SetDialZero:
                    CheckAxis(command.Axis);
                    if (command.Axis == Axis.X)
                    {
                        XDisplayZeroOffset = ToolX;
                        xDisplayZeroDegrees = XHandwheelDegrees;
                    }
                    else
                    {
                        ZDisplayZeroOffset = ToolZ;
                        zDisplayZeroDegrees = ZHandwheelDegrees;
                    }
                    break;
                case LatheCommandKind.Move:
                    CheckAxis(command.Axis);
                    if (!Active || command.Value == 0) return;
                    float oldX = ToolX, oldZ = ToolZ;
                    double delta = LatheDialMath.DegreesToMillimetres(command.Value, Scale);
                    if (command.Axis == Axis.X) ToolX = (float)Math.Max(0, Math.Min(maxX, ToolX + delta));
                    else ToolZ = (float)Math.Max(gripLength + (Tool?.Width ?? 0) / 2, Math.Min(maxZ, ToolZ + delta));
                    if (ToolX != oldX || ToolZ != oldZ) Cut(SelectedTool == ToolKind.Boring ? ToolX : Math.Min(oldX, ToolX), oldZ, ToolZ);
                    float actual = command.Axis == Axis.X ? ToolX - oldX : ToolZ - oldZ;
                    float actualDegrees = LatheDialMath.MillimetresToDegrees(actual, Scale);
                    if (command.Axis == Axis.X) XHandwheelDegrees += actualDegrees;
                    else ZHandwheelDegrees += actualDegrees;
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(command));
            }
        }

        private void Cut(float radius, float oldZ, float newZ)
        {
            if (Tailstock.Active) return;
            if (SelectedTool == ToolKind.Parting) { CutParting(radius,newZ); return; }
            if (SelectedTool == ToolKind.Boring) { CutBore(radius, oldZ, newZ); return; }
            if (Active && SpindleRunning && Profile != null && ToolMounted && SelectedTool == ToolKind.Chamfer && availableChamfer != null && !workpiece.IsFinished)
            {
                float near = Math.Min(oldZ, newZ), far = Math.Max(oldZ, newZ);
                // Tip = (Z, X), heel = (Z - reach, X + reach), in machine coordinates.
                // The sweep is a 45-degree edge plus the tip's constant-radius travel.
                CutEdge(near - ChamferToolState.CuttingEdgeReach, near, radius + ChamferToolState.CuttingEdgeReach, radius);
                if (far > near) CutEdge(near, far, radius, radius);
                return;
            }
            if (!Active || !SpindleRunning || Profile == null || Tool == null || !Tool.CanCut || workpiece.IsFinished) return;
            float from = Math.Max(gripLength, Math.Min(oldZ, newZ) + Tool.EdgeStart);
            float to = Math.Max(oldZ, newZ) + Tool.EdgeEnd;
            TurningCoordinateConverter.MachineIntervalToWorkpiece(from, to, Profile.TotalLength, Orientation, out from, out to);
            float inner=Profile.InnerProfile.MaximumRadius(from,to);
            if (inner>0 && radius<inner+TurningProfile.MinimumWall) StatusMessage="내경과 외경이 교차하는 벽 두께 한계입니다.";
            // Removal itself intersects the profile; no contact means no changed interval.
            LastMoveRemovedMaterial = Profile.RemoveMaterial(from, to, radius);
        }

        private void CutEdge(float from, float to, float fromRadius, float toRadius)
        {
            if (to <= gripLength) return;
            float clipped = Math.Max(gripLength, from);
            fromRadius += (toRadius - fromRadius) * (clipped - from) / (to - from);
            from = clipped;
            float localFrom=Orientation==Orientation.Reversed?Profile.TotalLength-to:from;
            float localTo=Orientation==Orientation.Reversed?Profile.TotalLength-from:to;
            float inner=Profile.InnerProfile.MaximumRadius(localFrom,localTo);
            if (inner>0 && Math.Min(fromRadius,toRadius)<inner+TurningProfile.MinimumWall) StatusMessage="내경과 외경이 교차하는 벽 두께 한계입니다.";
            if (Orientation == Orientation.Reversed)
                LastMoveRemovedMaterial |= Profile.RemoveLinearMaterial(Profile.TotalLength - to, Profile.TotalLength - from, toRadius, fromRadius);
            else LastMoveRemovedMaterial |= Profile.RemoveLinearMaterial(from, to, fromRadius, toRadius);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void CheckAxis(Axis axis)
        { if (axis != Axis.X && axis != Axis.Z) throw new ArgumentException("Invalid axis."); }
    }
}
