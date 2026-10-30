using System;
using LastMachinist.Machining.Turning;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastMachinist.Core
{
    public sealed class GameplaySystemMenu : MonoBehaviour
    {
        public static GameplaySystemMenu Current { get; private set; }
        public static bool BlocksGameplay => Current != null && Current.IsOpen;
        private Func<bool> save;
        private Action quit;
        private Func<bool> canOpen;
        private LathePrototype lathe;
        private FirstPersonExplorer explorer;
        private bool priorLatheInput;
        private CursorLockMode priorLock;
        private bool priorCursor;
        public bool IsOpen { get; private set; }
        public bool ExitConfirmation { get; private set; }
        public bool SettingsOpen { get; private set; }
        public bool ReturnConfirmation { get; private set; }
        public bool CanReturn => ExplorationSession.IsExploring;
        public void RequestReturn() { if (IsOpen && CanReturn) ReturnConfirmation = true; }
        public void CancelReturn() => ReturnConfirmation = false;
        public bool ConfirmReturn()
        {
            if (!IsOpen || !ReturnConfirmation || !CanReturn) return false;
            if (!ExplorationSession.RequestWorkshopReturn()) return false;
            IsOpen = false; ReturnConfirmation = false; explorer = null;
            return true;
        }
        public string Message { get; private set; }

        public void Bind(Func<bool> saveManual, Action quitGame, Func<bool> gameplayReady, LathePrototype station)
        { save = saveManual; quit = quitGame; canOpen = gameplayReady; lathe = station; Current = this; }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame ||
                (canOpen != null && !canOpen())) return;
            explorer ??= FindFirstObjectByType<FirstPersonExplorer>();
            if (explorer != null && (explorer.InspectionOpen || explorer.LastInspectionCloseFrame == Time.frameCount)) return;
            if (!IsOpen) OpenMenu();
            else if (ExitConfirmation) ExitConfirmation = false;
            else if (ReturnConfirmation) ReturnConfirmation = false;
            else if (SettingsOpen) SettingsOpen = false;
            else CloseMenu();
        }

        public void OpenMenu()
        {
            if (IsOpen || (canOpen != null && !canOpen())) return;
            explorer = FindFirstObjectByType<FirstPersonExplorer>();
            priorLock = Cursor.lockState; priorCursor = Cursor.visible;
            priorLatheInput = lathe != null && lathe.Router != null && lathe.Router.IsActive;
            IsOpen = true; ExitConfirmation = false; SettingsOpen = false; Message = null;
            ReturnConfirmation = false;
            if (lathe?.Router != null) lathe.Router.SetActive(false);
            if (explorer != null) explorer.SetSystemBlocked(true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        public void CloseMenu()
        {
            if (!IsOpen) return;
            IsOpen = false; ExitConfirmation = false; SettingsOpen = false;
            ReturnConfirmation = false;
            if (explorer != null) { explorer.SetMenu(false); explorer.SetSystemBlocked(false); }
            else { Cursor.lockState = priorLock; Cursor.visible = priorCursor; }
            if (priorLatheInput && lathe != null && lathe.isActiveAndEnabled && lathe.CurrentPage == HudPage.Lathe && !lathe.DialogueBlocked)
                lathe.Router.SetActive(true);
            explorer = null;
        }
        public void RequestExit() { if (IsOpen) { SettingsOpen = false; ExitConfirmation = true; } }
        public void CancelExit() { ExitConfirmation = false; }
        public bool SaveManual()
        {
            if (!IsOpen) return false;
            bool okay = save != null && save();
            Message = okay ? "저장했습니다." : "저장하지 못했습니다.";
            return okay;
        }
        public bool SaveAndExit()
        {
            if (!IsOpen || !ExitConfirmation || !SaveManual()) return false;
            quit?.Invoke(); return true;
        }
        public bool ExitWithoutSave()
        {
            if (!IsOpen || !ExitConfirmation) return false;
            quit?.Invoke(); return true;
        }

        private void OnGUI()
        {
            if (!IsOpen) return;
            GUI.depth = -100;
            MvpUiScale.DrawLetterbox();
            var old = GUI.matrix; GUI.matrix = MvpUiScale.Apply1280x800();
            MvpUiScale.DrawOpaqueBackground(new Rect(0, 0, 1280, 800));
            var panel = new Rect(390, 135, 500, 530);
            GUI.Box(panel, GUIContent.none);
            var heading = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            var button = new GUIStyle(GUI.skin.button) { fontSize = 19 };
            if (ReturnConfirmation)
            {
                GUI.Label(new Rect(420, 195, 440, 100), "탐색을 종료하고 작업장으로 돌아가시겠습니까?", new GUIStyle(heading) { wordWrap = true });
                if (GUI.Button(new Rect(450, 340, 380, 58), "취소", button)) CancelReturn();
                if (GUI.Button(new Rect(450, 420, 380, 58), "복귀", button)) ConfirmReturn();
            }
            else if (ExitConfirmation)
            {
                GUI.Label(new Rect(420, 195, 440, 70), "게임을 종료하시겠습니까?", heading);
                if (GUI.Button(new Rect(450, 305, 380, 58), "취소", button)) CancelExit();
                if (GUI.Button(new Rect(450, 381, 380, 58), "저장 후 종료", button)) SaveAndExit();
                if (GUI.Button(new Rect(450, 457, 380, 58), "저장하지 않고 종료", button)) ExitWithoutSave();
            }
            else if (SettingsOpen)
            {
                GUI.Label(new Rect(420, 205, 440, 60), "설정", heading);
                GUI.Label(new Rect(450, 315, 380, 65), "설정 항목 준비 중", heading);
                if (GUI.Button(new Rect(450, 495, 380, 58), "돌아가기", button)) SettingsOpen = false;
            }
            else
            {
                GUI.Label(new Rect(420, 175, 440, 65), "시스템 메뉴", heading);
                float y = 255;
                if (GUI.Button(new Rect(450, y, 380, 52), "계속하기", button)) CloseMenu();
                y += 62;
                if (CanReturn) { if (GUI.Button(new Rect(450, y, 380, 52), "작업장으로 복귀", button)) RequestReturn(); y += 62; }
                if (GUI.Button(new Rect(450, y, 380, 52), "저장", button)) SaveManual(); y += 62;
                if (GUI.Button(new Rect(450, y, 380, 52), "설정", button)) SettingsOpen = true; y += 62;
                if (GUI.Button(new Rect(450, y, 380, 52), "게임 종료", button)) RequestExit();
            }
            GUI.Label(new Rect(430, 590, 420, 48), Message ?? "", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 17 });
            GUI.matrix = old;
        }
        private void OnDestroy() { if (Current == this) Current = null; }
    }

    public static class GameQuit
    {
        public static void Perform()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
