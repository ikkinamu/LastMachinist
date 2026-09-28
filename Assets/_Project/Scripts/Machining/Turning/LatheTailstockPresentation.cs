using UnityEngine;

namespace LastMachinist.Machining.Turning
{
    public sealed partial class LathePrototype
    {
        private Transform tailstockBody, tailstockQuill, tailstockHolder, tailstockTool, centerPilot;

        public TailstockToolKind VisibleTailstockTool { get; private set; }
        public static readonly Rect TailstockControls = new Rect(758,648,474,142);
        private void CreateTailstockVisuals()
        {
            tailstockBody=Block("심압대 본체",Vector3.zero,new Vector3(.8f,1.5f,1),settings.MachineMaterial);
            tailstockQuill=Block("심압대 퀼",Vector3.zero,new Vector3(.7f,.24f,.24f),settings.ToolMaterial);
            tailstockHolder=Block("심압대 공구 홀더",Vector3.zero,new Vector3(.3f,.45f,.45f),settings.MachineMaterial);
            tailstockTool=CreateAxialCylinder("심압대 장착 공구");
            centerPilot=CreateAxialCylinder("센터드릴 파일럿");
        }
        private Transform CreateAxialCylinder(string name)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cylinder); obj.name=name; obj.transform.SetParent(transform,false);
            obj.transform.localRotation=Quaternion.Euler(0,0,90);
            obj.GetComponent<Renderer>().sharedMaterial=settings.ToolMaterial;
            if (Application.isPlaying) Destroy(obj.GetComponent<Collider>()); else DestroyImmediate(obj.GetComponent<Collider>());
            return obj.transform;
        }
        private void UpdateTailstockVisuals()
        {
            if (tailstockBody==null) return;
            var state=controller.Tailstock; float length=controller.Profile.TotalLength;
            float X(float z) => (z-length/2)*.05f;
            var definition=state.Definition;
            float toolLength=(definition?.MaximumDepth ?? 8)+4;
            tailstockBody.localPosition=new Vector3(X(state.AxialPosition+100),-.4f,0);
            tailstockHolder.localPosition=new Vector3(X(state.TipPosition+toolLength+3),0,0);
            float holder=state.TipPosition+toolLength+6, body=state.AxialPosition+100;
            tailstockQuill.localPosition=new Vector3(X((holder+body)/2),0,0);
            tailstockQuill.localScale=new Vector3((body-holder)*.05f,.24f,.24f);
            VisibleTailstockTool=state.MountedTool;
            tailstockTool.gameObject.SetActive(definition!=null); centerPilot.gameObject.SetActive(state.MountedTool==TailstockToolKind.CenterDrill);
            if (definition!=null)
            {
                tailstockTool.name=definition.DisplayName;
                bool center=definition.Visual==DrillVisualKind.SteppedCenter;
                float diameter=center?8:definition.Diameter;
                tailstockTool.localPosition=new Vector3(X(state.TipPosition+(center?(toolLength+3)/2:toolLength/2)),0,0);
                tailstockTool.localScale=new Vector3(diameter*.05f,(center?toolLength-3:toolLength)*.025f,diameter*.05f);
                centerPilot.localPosition=new Vector3(X(state.TipPosition+1.5f),0,0);
                centerPilot.localScale=new Vector3(.2f,.075f,.2f);
            }
            if (controller.SelectedTool==ToolKind.Boring)
            {
                tool.name="내경 바이트";
                tool.localScale=new Vector3(1.3f,.12f,.12f);
                tool.localPosition=new Vector3(X(controller.ToolZ)+.65f,controller.ToolX*.05f-.06f,0);
            }
            tool.gameObject.SetActive(controller.ToolMounted && !state.Active);
            foreach(var part in new[]{tailstockBody,tailstockQuill,tailstockHolder,tailstockTool,centerPilot}) part.GetComponent<Renderer>().enabled=TailstockVisualVisible && hudPage==HudPage.Lathe && isActiveAndEnabled && !DialogueBlocked;
        }
    }
}
