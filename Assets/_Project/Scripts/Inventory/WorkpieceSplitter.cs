using System;
using LastMachinist.Machining.Core;
using LastMachinist.Machining.Turning;

namespace LastMachinist.Inventory
{
    public static class WorkpieceSplitter
    {
        public const float Kerf = 2;
        public const float MinimumPieceLength = 5;
        public static void MaterialBounds(TurningProfile p, out float start, out float end)
        {
            p.GetMaterialBounds(out start, out end);
        }
        public static TurningProfile Slice(TurningProfile source,float from,float to)
        {
            if(from<0 || to>source.TotalLength || to<=from) throw new ArgumentException("Invalid slice.");
            var p=new TurningProfile { TotalLength=to-from };
            foreach(var s in source.SurfaceSpans())
            {
                float a=Math.Max(from,s.StartZ), b=Math.Min(to,s.EndZ); if(b<=a) continue;
                float At(float z)=>s.StartRadius+(s.EndRadius-s.StartRadius)*(z-s.StartZ)/(s.EndZ-s.StartZ);
                float ra=At(a), rb=At(b);
                p.Segments.Add(new TurningSegment { StartZ=a-from,EndZ=b-from,Radius=Math.Max(ra,rb) });
                if(ra!=rb) p.LinearCuts.Add(new TurningLinearCut { StartZ=a-from,EndZ=b-from,StartRadius=ra,EndRadius=rb });
            }
            foreach(var h in source.InnerProfile.Segments)
            {
                float a=Math.Max(from,h.StartZ), b=Math.Min(to,h.EndZ); if(b<=a) continue;
                p.InnerProfile.Segments.Add(new HoleSegment { StartZ=a-from,EndZ=b-from,Radius=h.Radius });
            }
            p.InnerProfile.CenteredLeft=p.InnerProfile.RadiusAt(0)>0;
            p.InnerProfile.CenteredRight=p.InnerProfile.RadiusAt(p.TotalLength)>0;
            foreach(var o in source.InnerProfile.OperationHistory)
            {
                float a=o.Position==0?0:source.TotalLength-o.Depth, b=o.Position==0?o.Depth:source.TotalLength;
                float overlap=Math.Min(b,to)-Math.Max(a,from); if(overlap<=0) continue;
                p.InnerProfile.OperationHistory.Add(new HoleOperation { ToolId=o.ToolId, Position=o.Position==0?0:p.TotalLength,Diameter=o.Diameter,Depth=overlap });
            }
            p.Validate(); return p;
        }
        private static WorkpieceInstance Piece(WorkpieceInstance source,float from,float to)
        {
            var p=source.Copy(); p.ParentInstanceId=source.InstanceId; p.InstanceId=Guid.NewGuid().ToString("N");
            p.Availability=WorkpieceAvailability.Available; p.MachiningMeasurements.Clear();
            p.MachiningWorkpiece.InstanceId=p.InstanceId;
            p.MachiningWorkpiece.Geometry.RoundProfile=Slice(source.MachiningWorkpiece.Geometry.RoundProfile,from,to);
            p.MachiningWorkpiece.ProcessHistory.Add("parting"); p.Validate(); return p;
        }
        public static bool TrySplit(InventoryState inventory,LatheController controller,out WorkpieceInstance a,out WorkpieceInstance b)
        {
            a=null;b=null;
            var source=inventory.FindWorkpiece(inventory.ActiveMachiningInstanceId);
            if(source==null || !controller.PartingComplete || !ReferenceEquals(source.MachiningWorkpiece,controller.Workpiece)) return false;
            try
            {
                inventory.Validate(); var p=controller.Profile;
                MaterialBounds(p,out float start,out float end);
                float from=controller.PartingStart, to=controller.PartingEnd;
                if(from-start<MinimumPieceLength || end-to<MinimumPieceLength || Math.Abs(to-from-Kerf)>.0001f) return false;
                var first=Piece(source,start,from); var second=Piece(source,to,end);
                if(Math.Abs(first.MachiningWorkpiece.Geometry.RoundProfile.TotalLength+Kerf+second.MachiningWorkpiece.Geometry.RoundProfile.TotalLength-(end-start))>.001f) return false;
                // Construct and validate both pieces before replacing the source atomically.
                int index=inventory.Workpieces.IndexOf(source);
                inventory.Workpieces[index]=first; inventory.Workpieces.Insert(index+1,second);
                inventory.ActiveMachiningInstanceId=null; a=first;b=second; return true;
            }
            catch(ArgumentException) { return false; }
        }
    }
}
