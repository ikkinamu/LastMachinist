using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using LastMachinist.Machining.Turning;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using LastMachinist.Core;
using LastMachinist.Quest;
using LastMachinist.Save;
using NUnit.Framework;
using UnityEngine;

namespace LastMachinist.Tests
{
    public sealed class PrologueRevisionTests
    {
        [UnityTest] public IEnumerator NewGameThenCompletedContinueRestoresDayOneWorkshop()
        {
            yield return new EnterPlayMode();
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Boot.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            var boot = Object.FindAnyObjectByType<GameBootstrap>(); boot.enabled = false;
            var overlay = Object.FindAnyObjectByType<MvpStartupOverlay>();
            var lathe = Object.FindAnyObjectByType<LathePrototype>();
            overlay.BindPersistence(null, null, () => true, null, () => false, () => false);
            overlay.RequestNewGame();
            Assert.That(boot.Quest01.Quest.Data.Prologue, Is.EqualTo(PrologueStatus.InProgress));
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var cursor = (ProloguePlayback)typeof(MvpStartupOverlay).GetField("prologue", flags).GetValue(overlay);
            var advance = typeof(MvpStartupOverlay).GetMethod("AdvancePrologue", flags);
            while (!cursor.Completed)
            {
                yield return null; // Draw each beat using the real shared UI.
                string choice = cursor.Current.Dialogue.Choices?.Count > 0 ? "promise" : null;
                advance.Invoke(overlay, new object[] {choice});
            }
            Assert.That(boot.Quest01.Quest.Data.Prologue, Is.EqualTo(PrologueStatus.Completed));
            overlay.Resume(boot.Quest01); // Completed save before DAY1 must skip prologue.
            var step = typeof(MvpStartupOverlay).GetField("storyStep", flags);
            Assert.That(step.GetValue(overlay), Is.EqualTo(1));
            step.SetValue(overlay, 2); yield return null;
            Assert.That(overlay.CompleteDayOneIntro("intro_basic"), Is.True);
            Assert.That(lathe.isActiveAndEnabled, Is.True);
            Assert.That(overlay.GameStarted, Is.True);
            Assert.That(boot.Quest01.Quest.Data.State, Is.EqualTo(QuestState.Offered));
            yield return null;
            yield return new ExitPlayMode();
        }
        [TestCase(1920, 1080)]
        [TestCase(1920, 1200)]
        [TestCase(2520, 1080)]
        public void MapImageAndMarkerStayAlignedAtSupportedAspects(int width, int height)
        {
            var image = ExplorationMapLayout.ImageRect(ExplorationMapLayout.MapArea, 16f/9);
            var marker = ExplorationMapLayout.Marker(image, new Vector2(.25f, .75f));
            var screenImage = MvpUiScale.ToScreenRect(image, width, height);
            var screenMarker = MvpUiScale.ToScreenRect(marker, width, height);
            Assert.That(screenImage.width / screenImage.height, Is.EqualTo(16f/9).Within(.001));
            Assert.That((screenMarker.center.x - screenImage.x)/screenImage.width, Is.EqualTo(.25f).Within(.001));
            Assert.That((screenMarker.center.y - screenImage.y)/screenImage.height, Is.EqualTo(.25f).Within(.001));
        }
        [TestCase("promise")]
        [TestCase("silence")]
        public void BothChoicesVisitAllEightCutsAndCannotBeSkipped(string choice)
        {
            var art = Resources.Load<PrologueDefinition>("PrologueArt");
            Assert.That(art, Is.Not.Null);
            var play = new ProloguePlayback(art.Build());
            var cuts = new List<int>();
            int choices = 0;
            while (!play.Completed)
            {
                if (!cuts.Contains(play.Current.Cut)) cuts.Add(play.Current.Cut);
                if (play.Current.Dialogue.Choices?.Count > 0)
                {
                    int before = play.Index;
                    Assert.That(play.Advance(), Is.False);
                    Assert.That(play.Advance("invalid"), Is.False);
                    Assert.That(play.Index, Is.EqualTo(before));
                    Assert.That(play.Advance(choice), Is.True); choices++;
                }
                else Assert.That(play.Advance(), Is.True);
            }
            CollectionAssert.AreEqual(new[] {1,2,3,4,5,6,7,8}, cuts);
            Assert.That(choices, Is.EqualTo(1));
            Assert.That(new ProloguePlayback(art.Build()).Index, Is.Zero);
        }
        [Test] public void PrologueCompletionSurvivesSaveAndRestore()
        {
            var data = new SaveGameData(); data.Quest.Prologue = PrologueStatus.Completed;
            var restored = SaveGameComposer.Restore(JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(data)));
            Assert.That(restored.Quest.Data.Prologue, Is.EqualTo(PrologueStatus.Completed));
            var host = new GameObject("continue test");
            try {
                var overlay = host.AddComponent<MvpStartupOverlay>(); overlay.Resume(restored);
                Assert.That(restored.Quest.Data.Prologue, Is.EqualTo(PrologueStatus.Completed));
            } finally { Object.DestroyImmediate(host); }
        }
        [Test] public void WorkshopMenuCannotOfferOrConfirmExplorationReturn()
        {
            var host = new GameObject("menu test");
            try {
                var menu = host.AddComponent<GameplaySystemMenu>(); menu.Bind(() => true, () => {}, () => true, null);
                menu.OpenMenu(); menu.RequestReturn();
                Assert.That(menu.CanReturn || menu.ReturnConfirmation || menu.ConfirmReturn(), Is.False);
                Assert.That(menu.IsOpen, Is.True); menu.CloseMenu();
            } finally { Object.DestroyImmediate(host); }
        }
    }
}
