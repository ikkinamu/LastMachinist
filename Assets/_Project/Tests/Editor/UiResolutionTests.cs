using LastMachinist.Core;
using NUnit.Framework;
using UnityEngine;

namespace LastMachinist.Tests
{
    public sealed class UiResolutionTests
    {
        [TestCase(1920, 1080)] [TestCase(1920, 1200)] [TestCase(2560, 1080)]
        public void QuestWindowHasSeparateAnchoredRegions(int width, int height)
        {
            Rect content = MvpUiScale.ContentRect(width, height);
            var regions = new[] { MvpPanelLayout.QuestList, MvpPanelLayout.QuestDetail, MvpPanelLayout.QuestAccept, MvpPanelLayout.SessionActions };
            var save = MvpPanelLayout.Cell(MvpPanelLayout.SessionActions, 0, 3);
            var load = MvpPanelLayout.Cell(MvpPanelLayout.SessionActions, 1, 3);
            var close = MvpPanelLayout.Cell(MvpPanelLayout.SessionActions, 2, 3);
            Assert.That(save.xMax, Is.LessThan(load.xMin));
            Assert.That(load.xMax, Is.LessThan(close.xMin));
            Assert.That(save.height, Is.EqualTo(close.height));
            Assert.That(save.width, Is.EqualTo(close.width));
            for (int i = 0; i < regions.Length; i++)
            {
                var screen = MvpUiScale.ToScreenRect(regions[i], width, height);
                Assert.That(screen.xMin, Is.GreaterThanOrEqualTo(content.xMin));
                Assert.That(screen.yMin, Is.GreaterThanOrEqualTo(content.yMin));
                Assert.That(screen.xMax, Is.LessThanOrEqualTo(content.xMax));
                Assert.That(screen.yMax, Is.LessThanOrEqualTo(content.yMax));
                for (int j = i + 1; j < regions.Length; j++) Assert.That(regions[i].Overlaps(regions[j]), Is.False);
            }
        }
        [TestCase(1280, 800)]
        [TestCase(1920, 1080)]
        [TestCase(1024, 768)]
        [TestCase(2560, 1080)]
        [TestCase(800, 1280)]
        public void CameraAndInputStayInsideProportionalLayout(int width, int height)
        {
            Rect content = MvpUiScale.ContentRect(width, height);
            Assert.That(content.width / content.height, Is.EqualTo(1.6f).Within(0.0001));
            Assert.That(content.center.x, Is.EqualTo(width / 2f).Within(0.001));
            Assert.That(content.center.y, Is.EqualTo(height / 2f).Within(0.001));
            Rect screen = MvpUiScale.ToScreenRect(new Rect(384, 120, 896, 520), width, height);
            float scale = content.width / 1280f;
            Assert.That(screen.xMin, Is.EqualTo(content.x + 384 * scale).Within(0.001));
            Assert.That(height - screen.yMax, Is.EqualTo(content.y + 120 * scale).Within(0.001));
            Assert.That(screen.xMax, Is.EqualTo(content.xMax).Within(0.001));
            Assert.That(screen.width / screen.height, Is.EqualTo(896f / 520).Within(0.0001));
            Assert.That(screen.Contains(screen.center), Is.True);
            Assert.That(screen.Contains(new Vector2(content.x + 20 * scale, height / 2f)), Is.False);
        }

        [TestCase(1920, 1080)]
        [TestCase(1920, 1200)]
        [TestCase(2560, 1080)]
        public void EnlargedHandwheelsStayWithinContentAtSupportedAspectRatios(int width, int height)
        {
            Rect content = MvpUiScale.ContentRect(width, height);
            foreach (var layout in new[] { new Rect(408, 648, 164, 142), new Rect(582, 648, 164, 142) })
            {
                Rect wheel = MvpUiScale.ToScreenRect(layout, width, height);
                Assert.That(wheel.xMin, Is.GreaterThanOrEqualTo(content.xMin));
                Assert.That(wheel.xMax, Is.LessThanOrEqualTo(content.xMax));
                Assert.That(wheel.yMin, Is.GreaterThanOrEqualTo(content.yMin));
                Assert.That(wheel.yMax, Is.LessThanOrEqualTo(content.yMax));
                if (width == 1920 && height == 1080)
                    Assert.That(wheel.width, Is.GreaterThanOrEqualTo(164 * 1.35f));
            }
        }
    }
}
