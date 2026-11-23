using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using BattleForYourSoul.Combat;
using BattleForYourSoul.UI;

namespace BattleForYourSoul.Tests.PlayMode
{
    public sealed class GameHudGhostTests
    {
        private PlayModeFixtures _fixtures;
        private float _previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            _fixtures = new PlayModeFixtures();
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = _previousTimeScale;
            _fixtures.Dispose();
        }

        [UnityTest]
        public IEnumerator GhostHoldsAfterDamageThenShrinksAndSnapsOnHeal()
        {
            TestPlayer player = _fixtures.CreatePlayer(withWeapon: false);
            Canvas canvas = _fixtures.NewCanvas();
            Image fill = _fixtures.NewImage("Fill", canvas.transform);
            Image ghost = _fixtures.NewImage("Ghost", canvas.transform);
            fill.rectTransform.anchorMax = Vector2.one;
            ghost.rectTransform.anchorMax = Vector2.one;

            GameObject hudObject = _fixtures.NewObject("HUD");
            GameHud hud = hudObject.AddComponent<GameHud>();
            PlayModeFixtures.SetField(hud, "healthFill", fill);
            PlayModeFixtures.SetField(hud, "healthGhost", ghost);
            PlayModeFixtures.SetField(hud, "playerHealth", player.Health);
            PlayModeFixtures.SetField(hud, "ghostDelay", 0.1f);
            PlayModeFixtures.SetField(hud, "ghostShrinkDuration", 0.2f);
            _fixtures.Activate(hudObject);

            Assert.AreEqual(1f, fill.rectTransform.anchorMax.x, 0.0001f);
            Assert.AreEqual(1f, ghost.rectTransform.anchorMax.x, 0.0001f);

            player.Health.TakeDamage(new DamageInfo(40f, Vector2.zero, Vector2.zero));
            Assert.AreEqual(0.6f, fill.rectTransform.anchorMax.x, 0.0001f);
            Assert.AreEqual(1f, ghost.rectTransform.anchorMax.x, 0.0001f);

            yield return new WaitForSeconds(0.12f);
            float shrinking = ghost.rectTransform.anchorMax.x;
            Assert.That(shrinking, Is.LessThan(1f));
            Assert.That(shrinking, Is.GreaterThan(0.6f));

            yield return new WaitForSeconds(0.25f);
            Assert.AreEqual(0.6f, ghost.rectTransform.anchorMax.x, 0.0001f);

            player.Health.Heal(40f);
            Assert.AreEqual(1f, fill.rectTransform.anchorMax.x, 0.0001f);
            Assert.AreEqual(1f, ghost.rectTransform.anchorMax.x, 0.0001f);
        }
    }
}
