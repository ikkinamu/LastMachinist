using System;
using System.Globalization;
using LastMachinist.Core;
using LastMachinist.Machining.BenchDrill;
using LastMachinist.Machining.Core;
using LastMachinist.Quest;
using UnityEngine;

namespace LastMachinist.Machining.Turning
{
    public sealed partial class LathePrototype
    {
        private BenchDrillSession benchDrill;
        public BenchDrillSession BenchDrill => benchDrill;
        public WorkshopStory WorkshopStory => questIntegration?.Story;
        private Vector2 benchListScroll, storyListScroll;
        private float benchStep = 1;
        private int inspectedHole, inspectedDimension;
        private bool benchInspect;
        private string benchRecord = "", deliveryId;
        private static void BenchFill(Rect rect, Color color)
        { var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
        public static Vector2 BenchLocalToView(Rect plate, GeneralWorkpieceGeometry geometry, float x, float y)
            => new Vector2(plate.x + x / geometry.Width * plate.width, plate.yMax - y / geometry.Length * plate.height);
        private static void BenchCircle(Vector2 centre, float radius, Color color)
        {
            for (float y = -radius; y <= radius; y += 1)
            { float half = Mathf.Sqrt(Mathf.Max(0, radius * radius - y * y)); BenchFill(new Rect(centre.x - half, centre.y + y, half * 2, 1.2f), color); }
        }
        private void BenchButton(Rect rect, string text, BenchDrillCommand command, float amount, GUIStyle button)
        { if (GUI.Button(rect, text, button)) { benchInspect = false; benchDrill.Execute(command, amount); } }
        private void DrawBenchDrill(GUIStyle title, GUIStyle label, GUIStyle button)
        {
            GUI.Label(new Rect(400, 28, 770, 42), "탁상드릴", title);
            GUI.Label(new Rect(400, 78, 830, 45), "가공 종료 시 형상 보존 · 불러오면 주축 정지 / 장착 해제", label);
            if (benchDrill.Active == null)
            {
                GUILayout.BeginArea(new Rect(405, 140, 800, 540), GUI.skin.box);
                benchListScroll = GUILayout.BeginScrollView(benchListScroll);
                GUILayout.Label("실제 보유 공작물을 선택하세요. 판재 · 각재 · 직사각 봉재를 고정할 수 있습니다.", label);
                foreach (var item in PlayerInventory.Workpieces)
                    if (GUILayout.Button(LastMachinist.Inventory.WorkpieceInventoryPresentation.ListEntry(item, ResolveWorkpieceDefinition(item.WorkpieceDefinitionId)) + " · " + item.InstanceId.Substring(0, Math.Min(8, item.InstanceId.Length)), button))
                    { benchDrill.TryLoad(item); inspectedHole = 0; benchInspect = false; }
                GUILayout.EndScrollView(); GUILayout.EndArea();
                GUI.Label(new Rect(405, 695, 810, 80), benchDrill.Message ?? "소재가 없으면 폐작업장을 탐색해 회수하세요.", label);
                return;
            }
            var active = benchDrill.Active;
            GUI.Label(new Rect(410, 128, 480, 52), "공작물 " + active.InstanceId.Substring(0, Math.Min(8, active.InstanceId.Length)) +
                (benchDrill.Fixed ? " · 고정됨" : " · 고정 안 됨"), label);
            BenchButton(new Rect(935, 130, 275, 42), "가공 종료 · 보관", BenchDrillCommand.Finish, 0, button);
            if (benchDrill.Active == null) return;
            DrawBenchView(new Rect(430, 218, 445, 250), active.MachiningWorkpiece.Geometry, label);
            GUI.Label(new Rect(410, 176, 470, 34), "평면도 · 왼쪽 아래 모서리 기준 X → / Y ↑", label);
            BenchButton(new Rect(935, 183, 132, 42), "바이스 고정", BenchDrillCommand.Clamp, 0, button);
            BenchButton(new Rect(1075, 183, 135, 42), "고정 풀기", BenchDrillCommand.Unclamp, 0, button);
            GUI.Label(new Rect(935, 231, 280, 28), "드릴 " + (benchDrill.Bit == null ? "미장착" : "Ø" + benchDrill.Bit.Diameter), label);
            for (int i = 0; i < BenchDrillBit.Presets.Length; i++)
                BenchButton(new Rect(935 + i * 70, 263, 64, 42), "Ø" + BenchDrillBit.Presets[i].Diameter, BenchDrillCommand.SelectBit, i, button);
            BenchButton(new Rect(935, 315, 132, 42), "주축 ON", BenchDrillCommand.SpindleOn, 0, button);
            BenchButton(new Rect(1075, 315, 135, 42), "주축 OFF", BenchDrillCommand.SpindleOff, 0, button);
            GUI.Label(new Rect(935, 363, 275, 28), benchDrill.SpindleRunning ? "● 회전 중" : "○ 정지", label);
            GUI.Label(new Rect(935, 396, 275, 28), $"상대 X {benchDrill.ReadingX:0.##} / Y {benchDrill.ReadingY:0.##}", label);
            if (GUI.Button(new Rect(935, 427, 275, 38), $"핸드휠 1클릭: {benchStep:0.##} mm", button)) benchStep = benchStep == 1 ? 5 : benchStep == 5 ? .1f : 1;
            BenchButton(new Rect(935, 475, 85, 42), "X −", BenchDrillCommand.MoveX, -benchStep, button);
            BenchButton(new Rect(1028, 475, 85, 42), "X +", BenchDrillCommand.MoveX, benchStep, button);
            BenchButton(new Rect(1121, 475, 89, 42), "X 영점", BenchDrillCommand.ZeroX, 0, button);
            BenchButton(new Rect(935, 524, 85, 42), "Y −", BenchDrillCommand.MoveY, -benchStep, button);
            BenchButton(new Rect(1028, 524, 85, 42), "Y +", BenchDrillCommand.MoveY, benchStep, button);
            BenchButton(new Rect(1121, 524, 89, 42), "Y 영점", BenchDrillCommand.ZeroY, 0, button);
            GUI.Label(new Rect(935, 576, 275, 28), $"퀼 Z {benchDrill.QuillDepth:0.##} · 표면 0", label);
            BenchButton(new Rect(935, 610, 132, 42), "퀼 ↑ 1 mm", BenchDrillCommand.Feed, -1, button);
            BenchButton(new Rect(1075, 610, 135, 42), "퀼 ↓ 1 mm", BenchDrillCommand.Feed, 1, button);
            GUI.enabled = !benchDrill.SpindleRunning && benchDrill.QuillDepth < 0;
            if (GUI.Button(new Rect(935, 663, 275, 42), "측정 / 기록", button)) benchInspect = !benchInspect;
            GUI.enabled = true;
            GUI.Label(new Rect(410, 733, 800, 60), benchDrill.Message ?? "", label);
            if (benchInspect) DrawBenchInspection(label, button);
            else GUI.Label(new Rect(420, 580, 485, 128), "고정 → 공구 → X/Y → 주축 ON → 퀼 내리기\n다음 위치로 이동하기 전에 퀼을 표면 위로 올리세요.\n측정하려면 주축을 멈추고 퀼을 올리세요.", label);
        }
        private void DrawBenchView(Rect bounds, WorkpieceGeometry geometry, GUIStyle label)
        {
            var g = geometry.General;
            float scale = Mathf.Min(bounds.width / g.Width, bounds.height / g.Length);
            var plate = new Rect(bounds.center.x - g.Width * scale / 2, bounds.center.y - g.Length * scale / 2, g.Width * scale, g.Length * scale);
            BenchFill(new Rect(bounds.x - 16, bounds.y - 16, bounds.width + 32, bounds.height + 32), new Color(.18f, .23f, .25f));
            BenchFill(plate, new Color(.64f, .67f, .66f));
            var jaw = benchDrill.Fixed ? new Color(.36f, .44f, .31f) : new Color(.43f, .34f, .26f);
            BenchFill(new Rect(plate.x, plate.y, 5 * scale, plate.height), jaw);
            BenchFill(new Rect(plate.xMax - 5 * scale, plate.y, 5 * scale, plate.height), jaw);
            for (int i = 0; i < geometry.HoleFeatures.Count; i++)
            {
                var h = geometry.HoleFeatures[i];
                var centre = BenchLocalToView(plate, g, h.CenterX, h.CenterY);
                BenchCircle(centre, h.Diameter * scale / 2, h.IsThrough ? Color.black : new Color(.25f, .27f, .28f));
                GUI.Label(new Rect(centre.x + 8, centre.y - 22, 80, 24), "구멍 " + (i + 1), label);
            }
            var bit = BenchLocalToView(plate, g, benchDrill.X, benchDrill.Y);
            BenchFill(new Rect(bit.x - 12, bit.y, 24, 1), Color.yellow);
            BenchFill(new Rect(bit.x, bit.y - 12, 1, 24), Color.yellow);
            if (benchDrill.Bit != null)
                BenchCircle(bit, benchDrill.Bit.Diameter * scale / 2, new Color(.8f, .65f, .25f, .45f));
            BenchFill(new Rect(415, 548, 230, 9), new Color(.3f, .4f, .4f));
            BenchFill(new Rect(652, 537, 40, 10), new Color(.65f, .68f, .65f));
            float z = Mathf.Clamp(benchDrill.QuillDepth, -5, benchDrill.Thickness);
            BenchFill(new Rect(645, 548, 55, 9), Color.gray);
            BenchFill(new Rect(663, 490 + z * 2, 20, 22), Color.gray);
            BenchFill(new Rect(670, 512 + z * 2, 5, 25), benchDrill.SpindleRunning ? Color.yellow : Color.white);
            GUI.Label(new Rect(710, 541, 185, 30), "척 / 퀼 / 드릴", label);
        }
        private void DrawBenchInspection(GUIStyle label, GUIStyle button)
        {
            var holes = benchDrill.Active.MachiningWorkpiece.Geometry.HoleFeatures;
            if (holes.Count == 0) { GUI.Label(new Rect(420, 580, 480, 70), "측정할 구멍이 없습니다.", label); return; }
            inspectedHole = Mathf.Clamp(inspectedHole, 0, holes.Count - 1);
            if (GUI.Button(new Rect(415, 578, 180, 38), "구멍 " + (inspectedHole + 1) + " 선택", button)) inspectedHole = (inspectedHole + 1) % holes.Count;
            if (GUI.Button(new Rect(605, 578, 270, 38), new[] { "내경", "모서리 → 중심 X", "모서리 → 중심 Y" }[inspectedDimension], button)) inspectedDimension = (inspectedDimension + 1) % 3;
            var reading = benchDrill.Inspect(inspectedHole, inspectedDimension);
            if (reading == null) return;
            float range = inspectedDimension == 0 ? 12 : inspectedDimension == 1 ? benchDrill.Active.MachiningWorkpiece.Geometry.General.Width : benchDrill.Active.MachiningWorkpiece.Geometry.General.Length;
            float pixels = 445 / range;
            BenchFill(new Rect(430, 630, 445, 2), Color.white);
            for (int i = 0; i <= range; i++)
            {
                float x = 430 + i * pixels;
                BenchFill(new Rect(x, 630, 1, i % 5 == 0 ? 14 : 7), Color.white);
                if (i % (range > 20 ? 10 : 2) == 0) GUI.Label(new Rect(x - 8, 644, 35, 24), i.ToString(), label);
            }
            BenchFill(new Rect(430 + reading.Value * pixels, 619, 2, 13), Color.yellow);
            benchRecord = GUI.TextField(new Rect(420, 683, 120, 34), benchRecord, 12);
            if (GUI.Button(new Rect(550, 681, 150, 38), "측정값 기록", button) && float.TryParse(benchRecord, NumberStyles.Float, CultureInfo.InvariantCulture, out float entered))
                benchDrill.Record(inspectedHole, inspectedDimension, entered);
            GUI.Label(new Rect(708, 680, 195, 46), "간이 검사자 · 1 mm\n노란 지침을 읽으세요", label);
        }
        private static string StoryStatus(WorkshopQuestStatus state) => state == WorkshopQuestStatus.Offered ? "수락 가능" : state == WorkshopQuestStatus.Active ? "진행 중" : state == WorkshopQuestStatus.Completed ? "완료" : "미수락";
        private void DrawWorkshopStoryQuests(GUIStyle label, GUIStyle button)
        {
            var story = WorkshopStory;
            GUILayout.BeginArea(new Rect(405, 148, 810, 595), GUI.skin.box);
            storyListScroll = GUILayout.BeginScrollView(storyListScroll);
            GUILayout.Label("멈춰버린 펌프 · 완료", label);
            GUILayout.Space(12);
            GUILayout.Label("버려진 작업장 · " + StoryStatus(story.Data.Quest02), label);
            if (story.Data.Quest02 == WorkshopQuestStatus.NotStarted) GUILayout.Label("작업장에서 박성철에게 소식을 들으세요.", label);
            if (story.Data.Quest02 == WorkshopQuestStatus.Offered && GUILayout.Button("버려진 작업장 · 수락", button)) story.Accept02();
            if (story.Data.Quest02 == WorkshopQuestStatus.Active)
                GUILayout.Label(story.Data.BenchAcquired ? "작업장으로 돌아가 회수한 설비를 설치하세요." : story.Data.BenchDiscovered ? "발견한 탁상드릴을 회수하세요." : "지도의 폐작업장을 탐색하고 쓸 만한 물건을 조사하세요.", label);
            GUILayout.Space(20);
            if (story.Data.Quest03 != WorkshopQuestStatus.NotStarted)
            {
                GUILayout.Label("고정판 · " + StoryStatus(story.Data.Quest03), label);
                GUILayout.Label(LastMachinist.Quest.WorkshopStory.Drawing, label);
                if (story.Data.Quest03 == WorkshopQuestStatus.Offered && GUILayout.Button("고정판 · 수락", button)) story.Accept03();
                if (story.Data.Quest03 == WorkshopQuestStatus.Active)
                {
                    if (GUILayout.Button("탁상드릴 · 공작물 선택", button)) OpenPage(HudPage.BenchDrill);
                    GUILayout.Label("가공을 종료한 실제 공작물을 선택해 납품하세요.", label);
                    foreach (var item in PlayerInventory.Workpieces)
                        if (item.MachiningWorkpiece?.Geometry.Type == GeometryType.Plate && GUILayout.Button((deliveryId == item.InstanceId ? "▶ " : "") +
                            LastMachinist.Inventory.WorkpieceInventoryPresentation.ListEntry(item, ResolveWorkpieceDefinition(item.WorkpieceDefinitionId)) + " · " + item.InstanceId.Substring(0, Math.Min(8, item.InstanceId.Length)), button)) deliveryId = item.InstanceId;
                    if (GUILayout.Button("선택 공작물 검사 · 합격 시 납품", button)) story.Deliver(deliveryId);
                }
            }
            else if (story.Data.BenchInstalled) GUILayout.Label("작업장에서 박성철의 새 의뢰를 들으세요.", label);
            GUILayout.Label(story.Feedback ?? "", label);
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
    }
}
