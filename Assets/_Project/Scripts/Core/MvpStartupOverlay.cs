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
        private PrologueDefinition prologueArt;
        private ProloguePlayback prologue;
        private int previousCut;
        private float cutStarted, titleStarted;
        private System.Action prologueCompleted;
        public void BindPrologue(System.Action completed)
        { prologueArt = Resources.Load<PrologueDefinition>("PrologueArt"); prologueCompleted = completed; }
        private void StartPrologue()
        {
            prologueArt ??= Resources.Load<PrologueDefinition>("PrologueArt");
            prologueArt?.EnsureExternalSlots();
            prologue = new ProloguePlayback(prologueArt.Build());
            if (quest != null) quest.Quest.Data.Prologue = PrologueStatus.InProgress;
            storyStep = -1; previousCut = 0; cutStarted = Time.unscaledTime;
        }
        private void AdvancePrologue(string choice = null)
        {
            int oldCut = prologue.Current.Cut;
            if (!prologue.Advance(choice)) return;
            if (prologue.Completed)
            {
                quest.Quest.Data.Prologue = PrologueStatus.Completed;
                storyStep = -2; titleStarted = Time.unscaledTime;
                prologueCompleted?.Invoke();
            }
            else if (oldCut != prologue.Current.Cut)
            { previousCut = oldCut; cutStarted = Time.unscaledTime; }
        }
        private void DrawCut(int cut, float alpha, float elapsed)
        {
            var image = prologueArt.Cut(cut);
            var old = GUI.color;
            GUI.color = new Color(1, 1, 1, alpha);
            if (image != null)
            {
                GUI.BeginGroup(new Rect(0, 0, 1280, 800));
                float zoom = 1 + Mathf.Min(elapsed, 15) * .003f;
                GUI.DrawTexture(new Rect(-640 * (zoom-1) + Mathf.Sin(elapsed*.08f)*8, -400*(zoom-1), 1280*zoom, 800*zoom), image, ScaleMode.ScaleAndCrop);
                GUI.EndGroup();
            }
            else
            {
                GUI.color = new Color(.035f + cut*.008f, .055f + cut*.006f, .07f, alpha);
                GUI.DrawTexture(new Rect(0, 0, 1280, 800), Texture2D.whiteTexture);
                GUI.color = new Color(.6f, .65f, .7f, alpha);
                GUI.Label(new Rect(40, 35, 900, 40), $"CUT {cut:00} · 외부 일러스트 교체 예정", body);
            }
            GUI.color = old;
        }
        private System.Action saveGame, quitGame;
        private System.Func<bool> continueGame, beginNewGame, hasSave, hasAnySave;
        private bool confirmNewGame, confirmTitleExit, settingsOpen;
        public string PersistenceMessage { get; set; }
        public bool GameStarted => started;
        public bool CompleteDayOneIntro(string choiceId)
        {
            if (storyStep != 2 || questFlow == null || !questFlow.CompleteIntro(choiceId)) return false;
            started = true;
            if (lathe != null)
            {
                lathe.gameObject.SetActive(true); lathe.enabled = true;
                lathe.ResumeWorkshop();
            }
            if (world != null) world.enabled = true;
            return true;
        }
        public bool NewGameConfirmationOpen => confirmNewGame;
        public bool TitleExitConfirmationOpen => confirmTitleExit;
        public bool TitleHasContinue => hasSave != null && hasSave();
        public void RequestNewGame()
        {
            if (hasAnySave != null && hasAnySave()) confirmNewGame = true;
            else StartPrologue();
        }
        public void CancelNewGame() => confirmNewGame = false;
        public bool ConfirmNewGame()
        {
            if (!confirmNewGame || beginNewGame == null || !beginNewGame()) return false;
            confirmNewGame = false; StartPrologue(); return true;
        }
        public void RequestTitleExit() => confirmTitleExit = true;
        public void CancelTitleExit() => confirmTitleExit = false;
        public void ConfirmTitleExit() { if (confirmTitleExit) quitGame?.Invoke(); }
        public void BindPersistence(System.Action save, System.Func<bool> continueFromSave,
            System.Func<bool> newGame, System.Action quit, System.Func<bool> validSave, System.Func<bool> anySave)
        { saveGame = save; continueGame = continueFromSave; beginNewGame = newGame; quitGame = quit; hasSave = validSave; hasAnySave = anySave; }
        public void Resume(Quest01Integration value)
        {
            quest = value; started = value.Quest.Data.State != QuestState.NotStarted; storyStep = 0;
            if (!started)
            {
                if (value.Quest.Data.Prologue == PrologueStatus.Completed) storyStep = 1;
                else StartPrologue();
            }
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
            if (started && GameplaySystemMenu.BlocksGameplay) return;
            if (started && (world?.DialogueOpen ?? false)) return;
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
                    prologueArt ??= Resources.Load<PrologueDefinition>("PrologueArt");
                    prologueArt?.EnsureExternalSlots();
                    if (prologueArt != null && prologueArt.TitleBackgroundImage != null)
                    {
                        GUI.DrawTexture(new Rect(0, 0, 1280, 800), prologueArt.TitleBackgroundImage, ScaleMode.ScaleAndCrop);
                        GUI.color = new Color(0, 0, 0, .55f);
                        GUI.DrawTexture(new Rect(0, 0, 1280, 800), Texture2D.whiteTexture); GUI.color = Color.white;
                    }
                    GUI.Label(new Rect(280, 205, 720, 70), "LAST MACHINIST", title);
                    var menuButton = new GUIStyle(GUI.skin.button) { fontSize = 22 };
                    if (confirmNewGame)
                    {
                        GUI.Label(new Rect(330, 315, 620, 70), "현재 진행을 초기화하고 새 게임을 시작하시겠습니까?", body);
                        if (GUI.Button(new Rect(500, 415, 280, 55), "취소", menuButton)) CancelNewGame();
                        if (GUI.Button(new Rect(500, 485, 280, 55), "새 게임 시작", menuButton)) ConfirmNewGame();
                    }
                    else if (confirmTitleExit)
                    {
                        GUI.Label(new Rect(330, 315, 620, 70), "게임을 종료하시겠습니까?", body);
                        if (GUI.Button(new Rect(500, 415, 280, 55), "취소", menuButton)) CancelTitleExit();
                        if (GUI.Button(new Rect(500, 485, 280, 55), "종료", menuButton)) ConfirmTitleExit();
                    }
                    else if (settingsOpen)
                    {
                        GUI.Label(new Rect(330, 330, 620, 60), "설정 항목 준비 중", body);
                        if (GUI.Button(new Rect(500, 480, 280, 55), "돌아가기", menuButton)) settingsOpen = false;
                    }
                    else
                    {
                        bool valid = TitleHasContinue;
                        if (valid)
                        {
                            if (GUI.Button(new Rect(500, 345, 280, 58), "계속하기", menuButton) && continueGame != null && !continueGame())
                                PersistenceMessage = "정상 저장을 찾지 못했습니다.";
                            if (GUI.Button(new Rect(500, 415, 280, 58), "새 게임", menuButton)) RequestNewGame();
                        }
                        else if (GUI.Button(new Rect(500, 415, 280, 58), "시작", menuButton))
                            RequestNewGame();
                        if (GUI.Button(new Rect(500, 485, 280, 58), "설정", menuButton)) settingsOpen = true;
                        if (GUI.Button(new Rect(500, 555, 280, 58), "게임 종료", menuButton)) RequestTitleExit();
                        if (!valid && hasAnySave != null && hasAnySave() && string.IsNullOrEmpty(PersistenceMessage))
                            PersistenceMessage = "정상 저장을 찾지 못했습니다. 새 게임을 시작할 수 있습니다.";
                    }
                    GUI.Label(new Rect(300, 650, 680, 60), PersistenceMessage ?? "", body);
                }
                else if (storyStep == -1)
                {
                    float elapsed = Time.unscaledTime - cutStarted;
                    if (previousCut > 0) DrawCut(previousCut, 1, 10);
                    DrawCut(prologue.Current.Cut, Mathf.Clamp01(elapsed / .8f), elapsed);
                    bool inputEnabled = GUI.enabled; GUI.enabled = elapsed >= .8f;
                    DialoguePage.Draw(prologue.Current.Dialogue, () => AdvancePrologue(), id => AdvancePrologue(id));
                    GUI.enabled = inputEnabled;
                }
                else if (storyStep == -2)
                {
                    float elapsed = Time.unscaledTime - titleStarted;
                    if (elapsed < .8f) DrawCut(8, 1 - elapsed / .8f, 12);
                    GUI.color = new Color(1, 1, 1, Mathf.Clamp01(elapsed));
                    GUI.Label(new Rect(200, 320, 880, 100), "LAST MACHINIST", title);
                    GUI.color = Color.white;
                    if (elapsed >= 3) storyStep = 1;
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
                    DialoguePage.Draw(intro, null, choiceId => CompleteDayOneIntro(choiceId),
                        CharacterIllustrationResolver.Resolve(intro, illustrationBindings));
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
            if (GUI.Button(MvpPanelLayout.Cell(actions, 0, 2), "저장")) saveGame?.Invoke();
            GUI.enabled = lathe != null && lathe.isActiveAndEnabled && lathe.CurrentPage != HudPage.Workshop && !lathe.DialogueBlocked;
            if (GUI.Button(MvpPanelLayout.Cell(actions, 1, 2), "닫기")) lathe.OpenPage(HudPage.Workshop);
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
