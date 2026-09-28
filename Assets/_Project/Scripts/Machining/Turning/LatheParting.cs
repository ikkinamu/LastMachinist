using System;
using LastMachinist.Inventory;

namespace LastMachinist.Machining.Turning
{
    public sealed partial class LatheController
    {
        public bool SupportsParting { get; }
        public bool PartingComplete { get; private set; }
        public float PartingStart { get; private set; }
        public float PartingEnd { get; private set; }
        private void CutParting(float radius,float z)
        {
            if(!SupportsParting || PartingComplete || !Active || !SpindleRunning || !ToolMounted || Profile==null || workpiece.IsFinished) return;
            float from=z-WorkpieceSplitter.Kerf/2, to=z+WorkpieceSplitter.Kerf/2;
            if(from<gripLength) { StatusMessage="척 파지 영역 밖에서 절단하세요."; return; }
            TurningCoordinateConverter.MachineIntervalToWorkpiece(from,to,Profile.TotalLength,Orientation,out from,out to);
            WorkpieceSplitter.MaterialBounds(Profile,out float start,out float end);
            if(from-start<WorkpieceSplitter.MinimumPieceLength || end-to<WorkpieceSplitter.MinimumPieceLength)
            { StatusMessage="각 조각에 최소 5 mm가 남는 위치를 선택하세요."; return; }
            if(radius<=0)
            {
                // Keep valid source geometry until the inventory transaction succeeds.
                PartingStart=from; PartingEnd=to; PartingComplete=true; SpindleRunning=false;
                StatusMessage="절단 완료 · 인벤토리 분할 처리 중"; return;
            }
            LastMoveRemovedMaterial=Profile.RemoveMaterial(from,to,radius);
            StatusMessage=LastMoveRemovedMaterial?"절단 홈 가공 중 · 아직 연결되어 있습니다.":"절단 바이트 위치 조정";
        }
    }
}
