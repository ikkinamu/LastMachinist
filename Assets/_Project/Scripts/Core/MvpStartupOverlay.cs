using UnityEngine;
using LastMachinist.Integration;
using LastMachinist.Machining.Turning;
using LastMachinist.Quest;

namespace LastMachinist.Core
{
    // Small startup bridge so the implemented MVP systems are visible when Boot runs.
    public sealed class MvpStartupOverlay : MonoBehaviour
    {
        private Quest01Integration quest;
        private GUIStyle title, body;
        private LathePrototype lathe;
        private MvpWorldPanel world;
        private bool started;
        private QuestFlowController questFlow;
        private CharacterIllustrationBinding[] illustrationBindings;
        public void BindDialogueFlow(QuestFlowController value) { questFlow = value; }
        public void BindDialogueIllustrations(CharacterIllustrationBinding[] bindings) { illustrationBindings = bindings; }
        private int storyStep;
        private System.Action saveGame, loadGame;
        private System.Func<bool> hasSave;
        public string PersistenceMessage { get; set; }
        public void BindPersistence(System.Action save, System.Action load, System.Func<bool> exists)
        { saveGame = save; loadGame = load; hasSave = exists; }
        public void Resume(Quest01Integration value)
        {
            quest = value; started = value.Quest.Data.State != QuestState.NotStarted; storyStep = 0;
            if (world != null) world.enabled = started;
            bool workshop = world == null || world.CurrentLocation != "Farm";
            if (lathe != null)
            {
                lathe.gameObject.SetActive(started && workshop);
                lathe.enabled = started && workshop;
                if (lathe.Router != null) lathe.Router.SetActive(started && workshop);
                if (started && workshop) lathe.ResumeWorkshop();
            }
        }
        public void Bind(Quest01Integration value, LathePrototype prototype, MvpWorldPanel worldPanel) { quest = value; lathe = prototype; world = worldPanel; if (lathe != null) lathe.enabled = false; if (world != null) world.enabled = false; }
        private void OnGUI()
        {
            if (quest == null) return;
            GUI.depth = -20;
            MvpUiScale.DrawLetterbox();
            var oldMatrix = GUI.matrix;
            GUI.matrix = MvpUiScale.Apply1280x800();
            if (!started)
            {
                title ??= new GUIStyle(GUI.skin.label) { fontSize = 42, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                body ??= new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                GUI.color = new Color(0.025f, 0.035f, 0.05f, 1f);
                GUI.DrawTexture(new Rect(0, 0, 1280, 800), Texture2D.whiteTexture);
                GUI.color = Color.white;
                if (storyStep == 0)
                {
                    GUI.Label(new Rect(280, 205, 720, 70), "LAST MACHINIST", title);
                    if (GUI.Button(new Rect(500, 430, 280, 64), "시작", new GUIStyle(GUI.skin.button) { fontSize = 24 })) storyStep = 1;
                    if (hasSave != null && hasSave() && GUI.Button(new Rect(500, 510, 280, 64), "이어하기")) loadGame?.Invoke();
                    GUI.Label(new Rect(300, 595, 680, 60), PersistenceMessage ?? "", body);
                }
                else if (storyStep == 1)
                {
                    GUI.Label(new Rect(250, 150, 780, 60), "DAY 1  ·  삼촌의 작업장", title);
                    GUI.Label(new Rect(300, 260, 680, 180), "작업장을 맡은 첫날입니다.\n\n박성철이 작업장 입구에서 기다리고 있습니다.", body);
                    if (GUI.Button(new Rect(500, 500, 280, 64), "작업장으로", new GUIStyle(GUI.skin.button) { fontSize = 22 }))
                    {
                        storyStep = 2;
                    }
                }
                else
                {
                    world?.DrawIntroEnvironment();
                    var intro = questFlow?.IntroDialogue;
                    DialoguePage.Draw(intro, null, choiceId =>
                    {
                        if (questFlow.CompleteIntro(choiceId))
                        {
                            started = true;
                            if (lathe != null) lathe.enabled = true;
                            if (world != null) world.enabled = true;
                        }
                    }, CharacterIllustrationResolver.Resolve(intro, illustrationBindings));
                }
                GUI.matrix = oldMatrix;
                return;
            }
            title ??= new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            body ??= new GUIStyle(GUI.skin.label) { fontSize = 12 };
            title.fontSize = 16; title.alignment = TextAnchor.UpperLeft;
            body.fontSize = 12; body.alignment = TextAnchor.UpperLeft; body.wordWrap = false;
            bool questActivated = quest.Quest.IsActive;
            bool showQuest = questActivated && !(world?.DialogueOpen ?? false) &&
                (world?.CurrentLocation == "Farm" || lathe == null || !lathe.HideQuestOverlay);
            if (showQuest)
            {
            var trackerColor = GUI.color;
            if (world != null && world.ObjectiveHighlighted) GUI.color = new Color(1f, 0.83f, 0.5f);
            GUI.Box(new Rect(690, 12, 285, 64), GUIContent.none);
            GUI.color = trackerColor;
            if (questActivated) GUI.Label(new Rect(702, 19, 260, 22), "멈춰버린 펌프", title);
            body.wordWrap = true;
            GUI.Label(new Rect(702, 43, 260, 30), questFlow?.CurrentObjective ?? StateLabel(quest.Quest.Data.State), body);
            }
            var actions = MvpPanelLayout.SessionActions;
            if (GUI.Button(MvpPanelLayout.Cell(actions, 0, 3), "저장")) saveGame?.Invoke();
            if (GUI.Button(MvpPanelLayout.Cell(actions, 1, 3), "불러오기")) loadGame?.Invoke();
            GUI.enabled = lathe != null && lathe.isActiveAndEnabled && lathe.CurrentPage != HudPage.Workshop && !lathe.DialogueBlocked;
            if (GUI.Button(MvpPanelLayout.Cell(actions, 2, 3), "닫기")) lathe.OpenPage(HudPage.Workshop);
            GUI.enabled = true;
            GUI.Label(new Rect(actions.x, actions.yMax + 3, actions.width, 45), PersistenceMessage ?? "", body);
            if (!(world?.DialogueOpen ?? false)) world?.DrawNotifications();
            GUI.matrix = oldMatrix;
        }

        private static string StateLabel(QuestState state)
        {
            switch (state)
            {
                case QuestState.NotStarted: return "시작 전";
                case QuestState.FarmerArrived: return "농장 소식 도착";
                case QuestState.GoingToFarm: return "농장으로 이동";
                case QuestState.InspectingPump: return "펌프 점검";
                case QuestState.BrokenShaftFound: return "고장난 축 발견";
                case QuestState.ReturnedToWorkshop: return "작업장 복귀";
                case QuestState.RulerAttempt: return "금속 자 측정";
                case QuestState.VernierReceived: return "버니어 획득";
                case QuestState.InspectingBrokenShaft: return "고장난 축 측정";
                case QuestState.ShapeInferred: return "원래 형상 추론";
                case QuestState.MaterialSelected: return "소재 선택";
                case QuestState.Machining: return "가공 중";
                case QuestState.FinalInspection: return "최종 검사";
                case QuestState.ReadyToDeliver: return "납품 준비";
                case QuestState.Installing: return "설치 중";
                case QuestState.Completed: return "완료";
                default: return "진행 중";
            }
        }
    }
}
