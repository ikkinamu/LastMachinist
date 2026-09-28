using UnityEngine;
namespace LastMachinist.Machining.Turning
{
    public enum LatheMiniTab { None, Tools, Tailstock, Measurement, Camera }
    public sealed partial class LathePrototype
    {
        public LatheMiniTab OpenMiniTab { get; private set; }
        public bool TailstockVisualVisible { get; private set; } = true;
        public static readonly Rect SpindleControlRect=new Rect(1018,652,214,50);
        public static readonly Rect MiniPanelRect=new Rect(30,550,320,232);
        public bool ShowsCarriageX => !controller.Tailstock.Active;
        public void ToggleMiniTab(LatheMiniTab tab) => OpenMiniTab=OpenMiniTab==tab?LatheMiniTab.None:tab;
        public void SetTailstockVisualVisible(bool visible) { TailstockVisualVisible=visible; UpdateVisuals(); }
        private void DrawFixedSpindle()
        {
            var button=new GUIStyle(GUI.skin.button){fontSize=19};
            if(GUI.Button(SpindleControlRect,controller.SpindleRunning?"주축 정지 ■":"주축 회전 ▶",button)) router.SetSpindleRunning(!controller.SpindleRunning);
            for(int i=0;i<3;i++)
            { int scale=i==0?1:i==1?10:100;
                if(GUI.Button(new Rect(1018+i*73,714,68,42),"×"+scale,button)) router.SetMovementScale((MovementScale)scale); }
        }
        private void DrawQuillReadout()
        {
            var label=new GUIStyle(GUI.skin.label){fontSize=17,wordWrap=true};
            GUI.Label(new Rect(410,665,158,96),"심압대 활성\n전용 퀼 Z\n휠 회전 / 드래그",label);
            GUI.Label(new Rect(760,670,250,90),$"퀼 표시 {controller.Tailstock.QuillExtension-controller.Tailstock.ZeroExtension:0.###} mm\n배율 ×{(int)controller.Scale}\n공구대 Z와 별도 이송",label);
        }
        private void DrawLatheMiniUi(GUIStyle label,GUIStyle button)
        {
            string[] names={"공구","심압대","측정","카메라"};
            for(int i=0;i<4;i++)
            {
                var old=GUI.backgroundColor; if((int)OpenMiniTab==i+1) GUI.backgroundColor=new Color(.85f,.7f,.35f);
                if(GUI.Button(new Rect(30+(i%2)*164,450+(i/2)*48,156,42),names[i],button)) ToggleMiniTab((LatheMiniTab)(i+1));
                GUI.backgroundColor=old;
            }
            GUILayout.BeginArea(MiniPanelRect,GUI.skin.box);
            var miniButton=new GUIStyle(button){fixedHeight=38,fontSize=16};
            switch(OpenMiniTab)
            {
                case LatheMiniTab.Tools:
                    ToolKind[] kinds={ToolKind.Roughing,ToolKind.Finishing,ToolKind.Chamfer,ToolKind.Parting,ToolKind.Boring};
                    string[] tools={"황삭 바이트","정삭 바이트","모따기 바이트 · 45°","절단 바이트 · 폭 2 mm","내경 바이트"};
                    for(int i=0;i<kinds.Length;i++)
                    {
                        var color=GUI.backgroundColor; if(!controller.Tailstock.Active && controller.SelectedTool==kinds[i]) GUI.backgroundColor=new Color(.85f,.7f,.35f);
                        if(GUILayout.Button(tools[i],miniButton)) router.SelectTool(kinds[i]); GUI.backgroundColor=color;
                    }
                    break;
                case LatheMiniTab.Tailstock:
                    if(GUILayout.Button("센터드릴 Ø4",miniButton)) router.SelectTailstockTool(TailstockToolKind.CenterDrill);
                    if(GUILayout.Button("드릴 Ø10",miniButton)) router.SelectTailstockTool(TailstockToolKind.Drill10);
                    if(GUILayout.Button("드릴 Ø16",miniButton)) router.SelectTailstockTool(TailstockToolKind.Drill16);
                    if(GUILayout.Button(TailstockVisualVisible?"심압대 표시: 켜짐":"심압대 표시: 꺼짐",miniButton)) SetTailstockVisualVisible(!TailstockVisualVisible);
                    if(GUILayout.Button("심압대 공구 분리",miniButton)) router.SelectTailstockTool(TailstockToolKind.None);
                    break;
                case LatheMiniTab.Measurement:
                    if(GUILayout.Button("측정 화면",miniButton)) OpenPage(HudPage.Measurement);
                    if(GUILayout.Button("기록 목록",miniButton)) OpenPage(HudPage.Records);
                    if(GUILayout.Button("공작물 재고정",miniButton)) router.RequestReclamp();
                    GUILayout.Label("주축을 정지하고 측정하세요.",label); break;
                case LatheMiniTab.Camera:
                    string[] cameras={"정면","평면","우측면","등각"};
                    for(int i=0;i<4;i++) if(GUILayout.Button(cameras[i],miniButton)) SetCameraView((LatheViewMode)i);
                    break;
                default: DrawRepeatableTracker(label); break;
            }
            GUILayout.EndArea();
        }
    }
}
