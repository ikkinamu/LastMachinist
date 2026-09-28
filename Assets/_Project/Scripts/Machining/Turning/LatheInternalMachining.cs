using System;
using LastMachinist.Machining.Core;

namespace LastMachinist.Machining.Turning
{
    public sealed partial class LatheController
    {
        public TailstockState Tailstock { get; } = new TailstockState();
        public string StatusMessage { get; private set; }
        private bool boringEntered;
        public string ActiveToolLabel => Tailstock.Active ? "심압대 · " + (Tailstock.Definition?.DisplayName ?? "공구 없음") :
            (SelectedTool == ToolKind.Boring ? "내경 공구 · " : "외경 공구 · ") + (CurrentToolDisplayName ?? "공구 없음");
        private bool CanRetractTailstock()
        {
            if (Profile != null && Tailstock.TipPosition < Profile.TotalLength)
            { StatusMessage = "심압대 공구를 공작물 밖으로 후퇴시키세요."; return false; }
            return true;
        }
        private bool ExecuteInternalCommand(LatheCommand command)
        {
            switch (command.Kind)
            {
                case LatheCommandKind.RotateQuill:
                    ExecuteInternalCommand(new LatheCommand(LatheCommandKind.FeedQuill,LatheDialMath.DegreesToMillimetres(command.Value,Scale)));
                    return true;
                case LatheCommandKind.ZeroQuill:
                    Tailstock.ZeroDegrees=Tailstock.HandwheelDegrees; Tailstock.ZeroExtension=Tailstock.QuillExtension; return true;
                case LatheCommandKind.SelectTailstockTool:
                    if (!Enum.IsDefined(typeof(TailstockToolKind), (int)command.Value) || command.Value != (int)command.Value) throw new ArgumentException("Invalid tailstock tool.");
                    if (SpindleRunning) { StatusMessage="주축을 정지한 뒤 심압대 공구를 변경하세요."; return true; }
                    if (!CanRetractTailstock()) return true;
                    Tailstock.MountedTool=(TailstockToolKind)(int)command.Value; Tailstock.Active=true;
                    StatusMessage=Tailstock.Definition == null ? "먼저 심압대 공구를 장착하세요." : Tailstock.Definition.DisplayName+" 장착";
                    return true;
                case LatheCommandKind.SetTailstockActive:
                    if (SpindleRunning) { StatusMessage="주축을 정지한 뒤 공구 계열을 변경하세요."; return true; }
                    if (!CanRetractTailstock()) return true;
                    Tailstock.Active=command.Value!=0; return true;
                case LatheCommandKind.MoveTailstock:
                    if (!Active || Profile == null) return true;
                    if (SpindleRunning || !CanRetractTailstock()) { StatusMessage="주축 정지·공구 후퇴 후 심압대 본체를 이동하세요."; return true; }
                    Tailstock.AxialPosition=Math.Max(Profile.TotalLength+Tailstock.QuillExtension, Math.Min(Profile.TotalLength+100,Tailstock.AxialPosition+command.Value));
                    StatusMessage="심압대 본체 위치를 조정했습니다."; return true;
                case LatheCommandKind.FeedQuill:
                    if (!Active || Profile == null) { StatusMessage="공작물을 장착하고 가공을 재개하세요."; return true; }
                    if (!Tailstock.Active || Tailstock.Definition == null) { StatusMessage="먼저 심압대 공구를 장착하세요."; return true; }
                    var definition=Tailstock.Definition;
                    float maximumDepth=Math.Max(0, Math.Min(definition.MaximumDepth,Profile.TotalLength-gripLength));
                    float maximumExtension=Math.Min(90,Tailstock.AxialPosition-Profile.TotalLength+maximumDepth);
                    float old=Tailstock.QuillExtension;
                    Tailstock.QuillExtension=Math.Max(0,Math.Min(maximumExtension,old+command.Value));
                    Tailstock.HandwheelDegrees+=LatheDialMath.MillimetresToDegrees(Tailstock.QuillExtension-old,Scale);
                    if (command.Value <= 0) { StatusMessage="심압대 공구 후퇴"; return true; }
                    float depth=Math.Max(0,Profile.TotalLength-Tailstock.TipPosition);
                    if (!SpindleRunning || workpiece.IsFinished) { StatusMessage="주축 정지 상태에서는 위치만 조정합니다."; return true; }
                    if (depth <= 0 || Tailstock.QuillExtension <= old) { StatusMessage="공작물 접촉 위치 또는 이송 한계를 확인하세요."; return true; }
                    bool left=Orientation==Orientation.Reversed;
                    if (definition.ToolType != TailstockToolKind.CenterDrill && !(left?Profile.InnerProfile.CenteredLeft:Profile.InnerProfile.CenteredRight))
                    { StatusMessage="먼저 이 끝면을 센터드릴로 가공하세요."; return true; }
                    LastMoveRemovedMaterial=Profile.EnlargeHole(left?0:Profile.TotalLength-depth,left?depth:Profile.TotalLength,definition.Diameter/2,definition.ToolId,left);
                    if (LastMoveRemovedMaterial && definition.ToolType==TailstockToolKind.CenterDrill)
                    { if (left) Profile.InnerProfile.CenteredLeft=true; else Profile.InnerProfile.CenteredRight=true; }
                    StatusMessage=LastMoveRemovedMaterial ? "심압대 접촉 가공 중" : "이미 가공된 영역이거나 남은 벽 두께가 부족합니다.";
                    return true;
                default: return false;
            }
        }
        private void CutBore(float radius, float oldZ, float newZ)
        {
            if (!Active || Profile==null || !ToolMounted || workpiece.IsFinished) return;
            bool left=Orientation==Orientation.Reversed;
            float depth=Profile.InnerProfile.DepthFromFace(Profile.TotalLength,left);
            if (newZ >= Profile.TotalLength) { boringEntered=false; StatusMessage="내경 바이트를 구멍 중심에 맞춰 진입하세요."; return; }
            if (depth <= 0) { StatusMessage="내경 바이트가 진입할 구멍이 없습니다."; ToolZ=oldZ; return; }
            if (newZ < Profile.TotalLength-depth)
            { StatusMessage="기존 구멍 깊이를 넘어서 진입할 수 없습니다."; ToolZ=oldZ; return; }
            float a=Math.Min(oldZ,newZ), b=Math.Min(Profile.TotalLength,Math.Max(oldZ,newZ));
            if (!boringEntered)
            {
                float minimum=float.MaxValue;
                float from=left?Profile.TotalLength-b:a, to=left?Profile.TotalLength-a:b;
                foreach (var s in Profile.InnerProfile.Segments)
                    if (s.EndZ>from && s.StartZ<to) minimum=Math.Min(minimum,s.Radius);
                if (oldZ < Profile.TotalLength || radius < 1 || radius+1 > minimum)
                { StatusMessage="바이트를 밖으로 빼고 구멍 안에 들어갈 위치로 맞추세요."; ToolZ=oldZ; return; }
                boringEntered=true;
            }
            if (!SpindleRunning) { StatusMessage="주축 정지 상태에서는 위치만 조정합니다."; return; }
            // The bar behind the cutting edge must fit the existing entry passage.
            // This keeps this phase to bores/steps, rather than cutting an internal groove.
            float trailStart=Math.Min(Profile.TotalLength,Math.Max(oldZ,newZ)+0.5f);
            if (trailStart < Profile.TotalLength)
            {
                float ta=left?0:trailStart, tb=left?Profile.TotalLength-trailStart:Profile.TotalLength;
                float minimum=float.MaxValue;
                foreach (var s in Profile.InnerProfile.Segments)
                    if (s.EndZ>ta && s.StartZ<tb) minimum=Math.Min(minimum,s.Radius);
                if (radius>minimum+0.0001f)
                { ToolX=minimum; StatusMessage="입구 쪽부터 내경을 넓혀 바이트의 진입 공간을 확보하세요."; return; }
            }
            // A 1 mm axial cutting edge; it cannot cut beyond the existing bore bottom.
            a=Math.Max(Profile.TotalLength-depth,Math.Min(oldZ,newZ)-0.5f);
            b=Math.Min(Profile.TotalLength,Math.Max(oldZ,newZ)+0.5f);
            TurningCoordinateConverter.MachineIntervalToWorkpiece(a,b,Profile.TotalLength,Orientation,out a,out b);
            float maximum=Profile.MinimumOuterRadius(a,b)-TurningProfile.MinimumWall;
            if (radius > maximum) { StatusMessage="남은 벽 두께 한계입니다. 더 확대할 수 없습니다."; return; }
            LastMoveRemovedMaterial=Profile.EnlargeHole(a,b,radius,TailstockTools.BoringId,left);
            StatusMessage=LastMoveRemovedMaterial ? "내경 접촉 가공 중" : "구멍 내부 위치 조정 · 절삭 접촉 없음";
        }
    }
}
