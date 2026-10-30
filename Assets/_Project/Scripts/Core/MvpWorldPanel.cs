using UnityEngine;
using LastMachinist.Integration;
using LastMachinist.Machining.Turning;
using LastMachinist.Quest;

namespace LastMachinist.Core
{
    public sealed class PumpPresentation : MonoBehaviour
    {
        [SerializeField] private Texture2D broken, repaired;
        public void Bind(Texture2D brokenImage, Texture2D repairedImage)
        {
            broken = brokenImage; repaired = repairedImage;
            RestoreMissingArt();
        }
        private void RestoreMissingArt()
        {
            if (broken != null && repaired != null) return;
            var art = EnvironmentArtDefinition.LoadDefault();
            if (art == null) return;
            if (broken == null) broken = art.pumpBroken;
            if (repaired == null) repaired = art.pumpRepaired;
        }
        public Texture2D Visual(bool isRepaired)
        { RestoreMissingArt(); return isRepaired ? repaired : broken; }
    }
    public sealed class MvpWorldPanel : MonoBehaviour
    {
        private Quest01Integration quest;
        private LathePrototype lathe;
        private QuestFlowController questFlow;
        private CharacterIllustrationBinding[] illustrationBindings;
        private Texture2D workshopBackground, farmBackground;
        private PumpPresentation pumpPresentation;
        public void BindEnvironmentArt(Texture2D workshop, Texture2D farmImage, Texture2D broken, Texture2D repaired)
        {
            workshopBackground = workshop; farmBackground = farmImage;
            if (pumpPresentation == null)
            {
                var prop = new GameObject("Pump Interactive Prop");
                prop.transform.SetParent(transform, false);
                pumpPresentation = prop.AddComponent<PumpPresentation>();
            }
            pumpPresentation.Bind(broken, repaired);
            RestoreEnvironmentArt();
        }
        private void OnEnable() => RestoreEnvironmentArt();
        private void RestoreEnvironmentArt()
        {
            if (workshopBackground != null && farmBackground != null) return;
            var art = EnvironmentArtDefinition.LoadDefault();
            if (art == null) return;
            if (workshopBackground == null) workshopBackground = art.workshop;
            if (farmBackground == null) farmBackground = art.farm;
        }
        private static void DrawBackground(Texture2D image)
        {
            if (image != null) GUI.DrawTexture(new Rect(385, 95, 845, 525), image, ScaleMode.ScaleAndCrop, true);
        }
        private bool farm;
        private float installationStartedAt = -1f;
        private float pumpTime;
        private AudioSource pumpAudio;
        private AudioClip pumpClip;
        private DialogueNode workshopDialogue;
        private System.Action workshopDialogueDone;
        public bool DialogueOpen => workshopDialogue != null || questFlow?.CurrentDialogue != null;
        private void BeginWorkshopDialogue(string text, System.Action done)
        { workshopDialogue = new DialogueNode { SpeakerId = "park_seongcheol", Text = text }; workshopDialogueDone = done; lathe?.SynchronizeInputContext(); }
        public bool EnvironmentVisible => farm || lathe == null || lathe.CurrentPage == HudPage.Workshop || DialogueOpen;
        private string toast;
        private float toastUntil;
        private float objectiveHighlightUntil;
        public bool ObjectiveHighlighted => Time.unscaledTime < objectiveHighlightUntil;
        public void ShowToast(string text) { toast = text; toastUntil = Time.unscaledTime + 3.5f; }
        private void OnQuestChanged(QuestState previous, QuestState current)
        {
            if (previous != current) objectiveHighlightUntil = Time.unscaledTime + 2.5f;
            if (current == QuestState.Offered) ShowToast("퀘스트 창에 수락 가능한 의뢰가 추가되었습니다.");
            else if (current == QuestState.FarmerArrived) ShowToast("의뢰 수락 · 멈춰버린 펌프");
            else if (current == QuestState.Completed) ShowToast("의뢰 완료 · 멈춰버린 펌프");
            else if (current == QuestState.BrokenShaftFound) ShowToast("마모된 펌프 축을 회수했습니다.");
            else if (current == QuestState.VernierReceived) ShowToast("낡은 버니어 캘리퍼스를 받았습니다.");
            else if (current == QuestState.MaterialSelected) ShowToast("탄소강 원형봉 1개를 사용했습니다.");
            lathe?.SynchronizeInputContext();
        }
        private void OnDialogueChanged(DialogueNode node) => lathe?.SynchronizeInputContext();
        private void Update()
        {
            bool running = farm && quest != null && quest.PumpRepaired;
            if (running)
            {
                pumpTime += Time.unscaledDeltaTime;
                if (pumpAudio == null) CreatePumpAudio();
                if (!pumpAudio.isPlaying) pumpAudio.Play();
            }
            else if (pumpAudio != null && pumpAudio.isPlaying) pumpAudio.Stop();
        }
        private void OnDisable() { if (pumpAudio != null) pumpAudio.Stop(); }
        private void OnDestroy()
        {
            if (quest != null) quest.Quest.StateChanged -= OnQuestChanged;
            if (lathe != null) lathe.Notification -= ShowToast;
            if (questFlow != null) questFlow.DialogueChanged -= OnDialogueChanged;
            if (pumpClip != null) Destroy(pumpClip);
        }
        private void CreatePumpAudio()
        {
            // Temporary generated motor/water loop; replace with authored audio later.
            const int sampleRate = 22050;
            var samples = new float[sampleRate];
            var noise = new System.Random(17);
            for (int i = 0; i < samples.Length; i++)
            {
                float time = (float)i / sampleRate;
                samples[i] = 0.18f * Mathf.Sin(2 * Mathf.PI * 80 * time) +
                    0.06f * Mathf.Sin(2 * Mathf.PI * 160 * time) + (float)(noise.NextDouble() - 0.5) * 0.06f;
            }
            pumpClip = AudioClip.Create("Temporary pump motor and water", sampleRate, 1, sampleRate, false);
            pumpClip.SetData(samples, 0);
            pumpAudio = gameObject.AddComponent<AudioSource>();
            pumpAudio.clip = pumpClip; pumpAudio.loop = true; pumpAudio.playOnAwake = false;
            pumpAudio.spatialBlend = 0; pumpAudio.volume = 0.25f;
        }
        private static void Fill(Rect rect, Color color)
        { GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = Color.white; }
        private static void Line(Vector2 from, Vector2 to, float width, Color color)
        {
            var matrix = GUI.matrix;
            var delta = to - from;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
            Fill(new Rect(from.x, from.y - width / 2, delta.magnitude, width), color);
            GUI.matrix = matrix;
        }
        private void DrawWorkshopEnvironment()
        {
            if (workshopBackground != null)
            {
                DrawBackground(workshopBackground);
                // Scene art supplies the furniture. Interaction remains a separate layer.
                if (enabled && !DialogueOpen && lathe != null)
                {
                    if (GUI.Button(new Rect(440, 560, 150, 38), "가공")) lathe.OpenPage(HudPage.MachineSelect);
                    if (GUI.Button(new Rect(600, 560, 150, 38), "작업대 · 측정")) lathe.OpenPage(HudPage.Measurement);
                    if (GUI.Button(new Rect(760, 560, 150, 38), "소재 보관")) lathe.OpenPage(HudPage.Inventory);
                    if (GUI.Button(new Rect(920, 560, 240, 38), "김영호의 작업 기록")) lathe.OpenPage(HudPage.Memo);
                }
                return;
            }
            Fill(new Rect(385, 95, 845, 525), new Color(0.12f, 0.13f, 0.14f));
            // Dedicated place overview; these illustrations never draw over machining or measurement.
            Fill(new Rect(400, 102, 810, 12), new Color(0.31f, 0.28f, 0.23f));
            for (int i = 0; i < 8; i++) Fill(new Rect(395 + i * 109, 115, 2, 450), new Color(0.18f, 0.19f, 0.19f));
            Fill(new Rect(820, 125, 154, 112), new Color(0.37f, 0.42f, 0.37f));
            Fill(new Rect(890, 125, 7, 112), new Color(0.13f, 0.17f, 0.16f));
            Fill(new Rect(820, 175, 154, 7), new Color(0.13f, 0.17f, 0.16f));
            Fill(new Rect(385, 570, 845, 50), new Color(0.22f, 0.20f, 0.17f));
            // Art replaces the room layer; station, bench, materials and notes stay separate.
            DrawBackground(workshopBackground);
            Fill(new Rect(410, 395, 150, 22), new Color(0.42f, 0.28f, 0.18f));
            Fill(new Rect(425, 417, 12, 145), new Color(0.28f, 0.20f, 0.15f));
            Fill(new Rect(530, 417, 12, 145), new Color(0.28f, 0.20f, 0.15f));
            Fill(new Rect(430, 370, 35, 22), new Color(0.55f, 0.58f, 0.58f));
            Fill(new Rect(475, 365, 54, 28), new Color(0.32f, 0.36f, 0.37f));
            Fill(new Rect(1080, 220, 112, 205), new Color(0.25f, 0.20f, 0.16f));
            Fill(new Rect(1090, 245, 92, 5), new Color(0.58f, 0.42f, 0.25f));
            Fill(new Rect(1090, 305, 92, 5), new Color(0.58f, 0.42f, 0.25f));
            for (int i = 0; i < 5; i++)
            {
                float x = 1098 + i * 18;
                Line(new Vector2(x, 258), new Vector2(x + 8, 294), 4, new Color(0.72f, 0.72f, 0.65f));
                Line(new Vector2(x, 318), new Vector2(x - 7, 355), 4, new Color(0.66f, 0.68f, 0.62f));
            }
            Fill(new Rect(1080, 450, 112, 16), new Color(0.35f, 0.30f, 0.24f));
            for (int i = 0; i < 4; i++) Fill(new Rect(1090, 470 + i * 23, 90, 9), new Color(0.50f, 0.54f, 0.56f));
            Fill(new Rect(590, 120, 150, 62), new Color(0.55f, 0.48f, 0.35f));
            GUI.Label(new Rect(602, 130, 128, 45), "김영호 작업 기록\n축 형상 / 측정 메모", body);
            GUI.Label(new Rect(402, 602, 250, 24), "작업대 · 공구 보관 · 소재 보관", body);
            // Lathe landmark with headstock, exposed shaft, bed and carriage.
            Fill(new Rect(635, 370, 390, 30), new Color(0.30f, 0.39f, 0.36f));
            Fill(new Rect(643, 395, 42, 135), new Color(0.25f, 0.32f, 0.30f));
            Fill(new Rect(968, 395, 42, 135), new Color(0.25f, 0.32f, 0.30f));
            Fill(new Rect(620, 278, 83, 94), new Color(0.30f, 0.41f, 0.38f));
            Fill(new Rect(698, 290, 25, 64), new Color(0.59f, 0.62f, 0.57f));
            Fill(new Rect(723, 311, 211, 20), new Color(0.70f, 0.72f, 0.65f));
            Fill(new Rect(822, 346, 93, 24), new Color(0.48f, 0.49f, 0.41f));
            for (int i = 0; i < 12; i++) Fill(new Rect(738 + i * 18, 381, 2, 7), new Color(0.62f, 0.65f, 0.59f));
            Line(new Vector2(460, 200), new Vector2(515, 200), 7, new Color(0.83f, 0.70f, 0.42f));
            Fill(new Rect(445, 215, 80, 176), new Color(0.95f, 0.79f, 0.39f, 0.07f));
        }

        private void DrawFarmContext()
        {
            if (farmBackground != null)
            {
                DrawBackground(farmBackground);
                return;
            }
            Fill(new Rect(385, 95, 845, 525), new Color(0.12f, 0.22f, 0.16f));
            Fill(new Rect(400, 500, 810, 120), new Color(0.28f, 0.24f, 0.16f));
            for (int row = 0; row < 4; row++)
            {
                float y = 245 + row * 58;
                Line(new Vector2(420, y), new Vector2(570, y + 10), 8, new Color(0.26f, 0.42f, 0.18f));
                for (int i = 0; i < 8; i++) Fill(new Rect(435 + i * 17, y - 14, 7, 18), new Color(0.38f, 0.62f, 0.24f));
            }
            DrawBackground(farmBackground);
            Fill(new Rect(1030, 185, 135, 18), new Color(0.46f, 0.50f, 0.48f));
            Fill(new Rect(1045, 203, 12, 120), new Color(0.38f, 0.42f, 0.40f));
            Fill(new Rect(1140, 203, 12, 120), new Color(0.38f, 0.42f, 0.40f));
            Fill(new Rect(1042, 320, 115, 50), new Color(0.30f, 0.39f, 0.43f));
            Line(new Vector2(1010, 375), new Vector2(1170, 375), 12, new Color(0.45f, 0.50f, 0.48f));
            Line(new Vector2(1170, 375), new Vector2(1170, 530), 12, new Color(0.45f, 0.50f, 0.48f));
            GUI.Label(new Rect(405, 115, 270, 35), "이서연의 농장 · 물 공급 시설", body);
        }
        private void DrawPump()
        {
            bool running = quest.PumpRepaired;
            var metal = new Color(0.36f, 0.46f, 0.49f);
            var water = new Color(0.3f, 0.75f, 0.95f);
            GUI.Label(new Rect(425, 125, 740, 40), running ? "의뢰 완료 — 멈춰버린 펌프" : "농장의 멈춰버린 펌프", title);
            GUI.Label(new Rect(425, 178, 730, 60), running ? "새 축이 회전하고 배관에서 물이 흐릅니다." : "회전부가 멈췄고 배관에는 물이 흐르지 않습니다.", body);
            Texture2D pumpArt = pumpPresentation != null ? pumpPresentation.Visual(running) : null;
            if (pumpArt == null)
            {
            Fill(new Rect(460, 550, 620, 18), new Color(0.25f, 0.28f, 0.23f));
            Fill(new Rect(620, 340, 195, 175), metal);
            Fill(new Rect(630, 510, 30, 40), metal); Fill(new Rect(770, 510, 30, 40), metal);
            Fill(new Rect(790, 315, 210, 36), metal); Fill(new Rect(964, 340, 36, 150), metal);
            Fill(new Rect(935, 585, 190, 35), new Color(0.18f, 0.26f, 0.28f));
            var centre = new Vector2(640, 410);
            for (int i = 0; i < 40; i++)
            {
                float a = i * Mathf.PI * 2 / 40, b = (i + 1) * Mathf.PI * 2 / 40;
                Line(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 85,
                    centre + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 85, 10, new Color(0.64f, 0.68f, 0.61f));
            }
            for (int i = 0; i < 4; i++)
            {
                float angle = (running ? pumpTime * 2.8f : 0.35f) + i * Mathf.PI / 2;
                Line(centre, centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 80, 9, metal);
            }
            Fill(new Rect(630, 400, 20, 20), running ? water : new Color(0.75f, 0.3f, 0.2f));
            // Motor fins, coupling, inlet and bolted frame distinguish the pump assemblies.
            Fill(new Rect(815, 402, 58, 13), new Color(0.70f, 0.71f, 0.63f));
            Fill(new Rect(840, 395, 17, 27), new Color(0.50f, 0.38f, 0.24f));
            Fill(new Rect(873, 365, 93, 84), new Color(0.29f, 0.38f, 0.35f));
            for (int i = 0; i < 8; i++) Fill(new Rect(881 + i * 10, 372, 3, 65), new Color(0.15f, 0.22f, 0.21f));
            Line(new Vector2(585, 450), new Vector2(585, 550), 19, metal);
            Line(new Vector2(585, 550), new Vector2(460, 550), 19, metal);
            Fill(new Rect(451, 462, 64, 82), new Color(0.30f, 0.39f, 0.43f));
            for (int i = 0; i < 6; i++) Fill(new Rect(642 + (i % 3) * 70, 355 + (i / 3) * 135, 8, 8), new Color(0.75f, 0.73f, 0.63f));
            }
            else GUI.DrawTexture(new Rect(670, 240, 380, 380), pumpArt, ScaleMode.ScaleToFit, true);
            // Water remains a runtime state layer even when the pump is supplied as art.
            if (running)
            {
                for (int i = 0; i < 14; i++)
                {
                    float progress = Mathf.Repeat(pumpTime * 0.9f + i / 14f, 1);
                    float x = pumpArt != null ? 1038 + progress * 70 : 969;
                    float y = pumpArt != null ? 304 + progress * progress * 282 : 488 + progress * 98;
                    Fill(new Rect(x + Mathf.Sin(i * 2.3f) * 3, y, 6, 12), water);
                }
                Fill(new Rect(pumpArt != null ? 1070 : 942, 588, pumpArt != null ? 75 : 176, 7), water);
            }
        }
        public string CurrentLocation => farm ? "Farm" : "Workshop";
        public void RestoreLocation(string location)
        {
            farm = location == "Farm";
            installationStartedAt = farm && quest?.Quest.Data.State == QuestState.Installing ? Time.unscaledTime : -1f;
            lathe.gameObject.SetActive(!farm);
            lathe.enabled = !farm;
            lathe.Router.SetActive(!farm && lathe.CurrentPage == HudPage.Lathe && !DialogueOpen);
        }
        private GUIStyle title, body, button;
        public void Bind(Quest01Integration value, LathePrototype prototype, QuestFlowController flow = null)
        {
            if (quest != null) quest.Quest.StateChanged -= OnQuestChanged;
            if (lathe != null) lathe.Notification -= ShowToast;
            if (questFlow != null) questFlow.DialogueChanged -= OnDialogueChanged;
            quest = value; lathe = prototype; questFlow = flow;
            workshopDialogue = null; workshopDialogueDone = null;
            quest.Quest.StateChanged += OnQuestChanged;
            lathe.Notification += ShowToast;
            lathe.IsDialogueOpen = () => DialogueOpen;
            if (questFlow != null) questFlow.DialogueChanged += OnDialogueChanged;
            installationStartedAt = value?.Quest.Data.State == QuestState.Installing ? Time.unscaledTime : -1f;
        }
        public void BindDialogueIllustrations(CharacterIllustrationBinding[] bindings) { illustrationBindings = bindings; }
        private void DrawDialogue()
        {
            if (workshopDialogue != null)
            {
                DialoguePage.Draw(workshopDialogue, () => { var done = workshopDialogueDone; workshopDialogue = null; workshopDialogueDone = null; done?.Invoke(); lathe?.SynchronizeInputContext(); },
                    null, CharacterIllustrationResolver.Resolve(workshopDialogue, illustrationBindings));
                return;
            }
            var node = questFlow?.CurrentDialogue;
            if (node == null) return;
            DialoguePage.Draw(node, questFlow.AcknowledgeDialogue, questFlow.ChooseDialogue,
                CharacterIllustrationResolver.Resolve(node, illustrationBindings));
        }
        public void DrawIntroEnvironment()
        {
            RestoreEnvironmentArt();
            body ??= new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            DrawWorkshopEnvironment();
        }
        private void OnGUI()
        {
            if (quest == null || !enabled || GameplaySystemMenu.BlocksGameplay) return;
            GUI.depth = DialogueOpen ? -10 : 20;
            var oldMatrix = GUI.matrix;
            GUI.matrix = MvpUiScale.Apply1280x800();
            title ??= new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            body ??= new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            button ??= new GUIStyle(GUI.skin.button) { fontSize = 16, fixedHeight = 42 };
            if (!EnvironmentVisible) { GUI.matrix = oldMatrix; return; }
            MvpUiScale.DrawPageBackground(false);
            var state = quest.Quest.Data.State;
            if (farm)
            {
                DrawFarmContext();
                DrawPump();
            }
            else
            {
                DrawWorkshopEnvironment();
            }
            GUI.enabled = !DialogueOpen;
            // Shared sidebar draws in front of the world. Keep progression controls
            // in the free band below the scene, outside that sidebar's opaque area.
            GUI.BeginGroup(new Rect(380, 0, 900, 800));
            GUI.Box(new Rect(20, 620, 800, 170), GUIContent.none);
            GUI.Label(new Rect(38, 632, 730, 30), farm ? "농장 / 펌프 현장" : "작업장", title);
            GUI.Label(new Rect(38, 670, 730, 40), questFlow?.CurrentObjective ?? "다음 행동을 선택하세요", body);
            if (!farm && state == QuestState.FarmerArrived && GUI.Button(new Rect(38, 720, 145, 38), "농장으로 이동", button))
            { quest.GoToFarm(); farm = true; if (lathe != null) lathe.gameObject.SetActive(false); }
            else if (farm && state == QuestState.GoingToFarm && GUI.Button(new Rect(190, 720, 145, 38), "펌프 도착", button))
                quest.InspectPump();
            else if (farm && state == QuestState.InspectingPump && GUI.Button(new Rect(38, 720, 145, 38), "고장 축 회수", button))
                quest.RecoverPrototypeBrokenShaft();
            else if (!farm && state == QuestState.ReturnedToWorkshop && GUI.Button(new Rect(38, 720, 145, 38), "자로 측정 시도", button))
                quest.AttemptRuler();
            else if (!farm && state == QuestState.RulerAttempt && GUI.Button(new Rect(38, 720, 145, 38), "버니어 받기", button))
                quest.AcquireVernier();
            else if (!farm && state == QuestState.VernierReceived && GUI.Button(new Rect(38, 720, 145, 38), "고장 축 측정", button))
                quest.BeginBrokenShaftInspection();
            else if (!farm && state == QuestState.ShapeInferred && GUI.Button(new Rect(38, 720, 145, 38), "가공 방식 선택", button))
                lathe.OpenPage(HudPage.MachineSelect);
            else if (!farm && state == QuestState.ReadyToDeliver && GUI.Button(new Rect(38, 720, 145, 38), "농장으로 납품", button))
            { farm = true; if (lathe != null) lathe.gameObject.SetActive(false); }
            else if (farm && state == QuestState.ReadyToDeliver && GUI.Button(new Rect(190, 720, 145, 38), "부품 설치", button))
            { if (quest.DeliverAndInstall()) installationStartedAt = Time.unscaledTime; }
            else if (farm && state == QuestState.Installing)
            {
                if (installationStartedAt < 0) installationStartedAt = Time.unscaledTime;
                if (Time.unscaledTime - installationStartedAt < 1.5f)
                    GUI.Label(new Rect(38, 720, 300, 44), "박성철이 새 축을 고정하고 있습니다…", body);
                else if (GUI.Button(new Rect(38, 720, 145, 38), "펌프 가동", button))
                    quest.CompleteInstallation();
            }
            if (farm && (state == QuestState.BrokenShaftFound || state == QuestState.ReturnedToWorkshop) && GUI.Button(new Rect(190, 720, 145, 38), "작업장 복귀", button))
            { quest.ReturnToWorkshop(); RestoreLocation("Workshop"); lathe.OpenPage(HudPage.Workshop); }
            if (state == QuestState.Completed && GUI.Button(new Rect(38, 720, 290, 38), farm ? "작업장으로 돌아가기" : "수리한 펌프 확인", button))
                RestoreLocation(farm ? "Workshop" : "Farm");
            if (!farm && state == QuestState.Completed)
            {
                var story = quest.Story;
                if (story.Data.Quest02 == WorkshopQuestStatus.NotStarted && GUI.Button(new Rect(350, 720, 360, 38), "박성철에게 위치 정보 듣기", button))
                    BeginWorkshopDialogue("예전에 저쪽에 작은 작업장이 있었지. 쓸 만한 것이 남아 있을지도 몰라. 지도에 표시해 두겠네.",
                        () => { quest.LearnAbandonedWorkshopLocation(); story.Offer02(); });
                else if (story.Data.BenchAcquired && !story.Data.BenchInstalled && GUI.Button(new Rect(350, 720, 360, 38), "회수한 탁상드릴 정비·설치", button))
                    BeginWorkshopDialogue("구동부를 정비하고 작업대에 단단히 고정했네. 이제 판재와 각재에 구멍을 낼 수 있겠어.", () => story.InstallBench());
                else if (story.Data.Quest02 == WorkshopQuestStatus.Completed && story.Data.Quest03 == WorkshopQuestStatus.NotStarted && GUI.Button(new Rect(350, 720, 360, 38), "박성철 · 새 의뢰 듣기", button))
                    BeginWorkshopDialogue("작은 설비를 붙잡아 줄 고정판이 필요하네. 도면대로 두 곳에 구멍을 뚫어 주겠나? 의뢰서를 남겨 둘 테니 준비되면 수락하게.", () => story.Offer03());
            }
            if (farm)
            {
                GUI.color = quest.PumpRepaired ? new Color(0.2f, 0.9f, 0.4f) : new Color(0.9f, 0.35f, 0.25f);
                GUI.Label(new Rect(38, 695, 300, 24), quest.PumpRepaired ? "펌프 상태: 작동 중" : "펌프 상태: 고장", body);
                GUI.color = Color.white;
            }
            GUI.enabled = true;
            GUI.EndGroup();
            DrawDialogue();
            GUI.matrix = oldMatrix;
        }
        public void DrawNotifications()
        {
            if (Time.unscaledTime >= toastUntil || string.IsNullOrEmpty(toast)) return;
            Fill(new Rect(425, 88, 760, 42), new Color(0.13f, 0.19f, 0.18f, 0.96f));
            GUI.Label(new Rect(442, 94, 724, 32), toast, new GUIStyle(GUI.skin.label) { fontSize = 19, alignment = TextAnchor.MiddleCenter });
        }
    }
}
