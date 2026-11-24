using NUnit.Framework;
using UnityEngine;
using BattleForYourSoul.Combat;
using BattleForYourSoul.Progression;

namespace BattleForYourSoul.Tests.PlayMode
{
    public sealed class HealthTests
    {
        private PlayModeFixtures _fixtures;

        [SetUp]
        public void SetUp()
        {
            _fixtures = new PlayModeFixtures();
        }

        [TearDown]
        public void TearDown()
        {
            _fixtures.Dispose();
        }

        [Test]
        public void DamageClampsAtZeroAndDeathFiresOnce()
        {
            Health health = CreateHealth(100f);
            int changes = 0;
            int deaths = 0;
            health.Changed += _ => changes++;
            health.Died += _ => deaths++;

            health.TakeDamage(new DamageInfo(40f, Vector2.zero, Vector2.zero));
            Assert.AreEqual(60f, health.Current, 0.0001f);
            Assert.AreEqual(0.6f, health.Normalized, 0.0001f);
            Assert.IsTrue(health.IsAlive);
            Assert.AreEqual(1, changes);
            Assert.AreEqual(0, deaths);

            health.TakeDamage(new DamageInfo(999f, Vector2.zero, Vector2.zero));
            Assert.AreEqual(0f, health.Current, 0.0001f);
            Assert.IsFalse(health.IsAlive);
            Assert.AreEqual(1, deaths);

            health.TakeDamage(new DamageInfo(10f, Vector2.zero, Vector2.zero));
            Assert.AreEqual(0f, health.Current, 0.0001f);
            Assert.AreEqual(1, deaths);
        }

        [Test]
        public void NonPositiveDamageAndHealAreIgnored()
        {
            Health health = CreateHealth(50f);
            int changes = 0;
            health.Changed += _ => changes++;

            health.TakeDamage(new DamageInfo(0f, Vector2.zero, Vector2.zero));
            health.TakeDamage(new DamageInfo(-5f, Vector2.zero, Vector2.zero));
            health.Heal(0f);
            Assert.AreEqual(50f, health.Current, 0.0001f);
            Assert.AreEqual(0, changes);
        }

        [Test]
        public void ImmunityBlocksAllDamageUntilCleared()
        {
            Health health = CreateHealth(100f);
            health.SetDamageImmune(true);
            Assert.IsTrue(health.IsDamageImmune);

            health.TakeDamage(new DamageInfo(30f, Vector2.zero, Vector2.zero));
            Assert.AreEqual(100f, health.Current, 0.0001f);

            health.SetDamageImmune(false);
            health.TakeDamage(new DamageInfo(30f, Vector2.zero, Vector2.zero));
            Assert.AreEqual(70f, health.Current, 0.0001f);
        }

        [Test]
        public void HealingClampsAtMaxAndDoesNotRaiseTheDead()
        {
            Health health = CreateHealth(100f);
            health.TakeDamage(new DamageInfo(40f, Vector2.zero, Vector2.zero));
            health.Heal(15f);
            Assert.AreEqual(75f, health.Current, 0.0001f);
            health.Heal(1000f);
            Assert.AreEqual(100f, health.Current, 0.0001f);

            health.TakeDamage(new DamageInfo(100f, Vector2.zero, Vector2.zero));
            health.Heal(50f);
            Assert.AreEqual(0f, health.Current, 0.0001f);
        }

        [Test]
        public void MaxHealthChangeCanHealTheDifferenceAndResetRearmsDeath()
        {
            Health health = CreateHealth(100f);
            health.TakeDamage(new DamageInfo(40f, Vector2.zero, Vector2.zero));

            health.SetMax(120f, healDifference: true);
            Assert.AreEqual(120f, health.Max, 0.0001f);
            Assert.AreEqual(80f, health.Current, 0.0001f);

            health.SetMax(150f, healDifference: false);
            Assert.AreEqual(150f, health.Max, 0.0001f);
            Assert.AreEqual(80f, health.Current, 0.0001f);

            health.SetDamageImmune(true);
            health.ResetToFull();
            Assert.AreEqual(150f, health.Current, 0.0001f);
            Assert.IsFalse(health.IsDamageImmune);

            int deaths = 0;
            health.Died += _ => deaths++;
            health.TakeDamage(new DamageInfo(150f, Vector2.zero, Vector2.zero));
            Assert.AreEqual(1, deaths);
        }

        private Health CreateHealth(float max)
        {
            GameObject go = _fixtures.NewObject("Actor");
            Health health = go.AddComponent<Health>();
            PlayModeFixtures.SetField(health, "maxHealth", max);
            _fixtures.Activate(go);
            return health;
        }
    }

    public sealed class ExperienceTests
    {
        private PlayModeFixtures _fixtures;

        [SetUp]
        public void SetUp()
        {
            _fixtures = new PlayModeFixtures();
        }

        [TearDown]
        public void TearDown()
        {
            _fixtures.Dispose();
        }

        [Test]
        public void XpAccumulatesAndLevelsUpOncePerThreshold()
        {
            Experience experience = CreateExperience(1, 5f, 2f);
            var levels = new System.Collections.Generic.List<int>();
            int changes = 0;
            experience.LeveledUp += level => levels.Add(level);
            experience.Changed += _ => changes++;

            experience.AddXp(12f);
            Assert.AreEqual(2, experience.Level);
            Assert.AreEqual(7f, experience.CurrentXp, 0.0001f);
            Assert.AreEqual(10f, experience.XpForNextLevel, 0.0001f);
            Assert.AreEqual(0.7f, experience.Normalized, 0.0001f);
            Assert.AreEqual(1, levels.Count);
            Assert.AreEqual(2, levels[0]);
            Assert.AreEqual(1, changes);

            experience.AddXp(3f);
            Assert.AreEqual(3, experience.Level);
            Assert.AreEqual(0f, experience.CurrentXp, 0.0001f);
            Assert.AreEqual(20f, experience.XpForNextLevel, 0.0001f);
            Assert.AreEqual(2, levels.Count);
            Assert.AreEqual(3, levels[1]);
        }

        [Test]
        public void NonPositiveXpAndStartingLevelAreHandled()
        {
            Experience experience = CreateExperience(4, 10f);
            Assert.AreEqual(4, experience.Level);
            Assert.AreEqual(10f, experience.XpForNextLevel, 0.0001f);

            int changes = 0;
            experience.Changed += _ => changes++;
            experience.AddXp(-5f);
            experience.AddXp(0f);
            Assert.AreEqual(4, experience.Level);
            Assert.AreEqual(0f, experience.CurrentXp, 0.0001f);
            Assert.AreEqual(0, changes);
        }

        [Test]
        public void LargeXpGrantResolvesMultipleLevelsInOneCall()
        {
            Experience experience = CreateExperience(1, 5f, 2f);
            var levels = new System.Collections.Generic.List<int>();
            experience.LeveledUp += level => levels.Add(level);

            // Thresholds are 5, 10 and 20, so one grant crosses all three exactly.
            experience.AddXp(35f);
            Assert.AreEqual(4, experience.Level);
            Assert.AreEqual(3, levels.Count);
            Assert.AreEqual(2, levels[0]);
            Assert.AreEqual(3, levels[1]);
            Assert.AreEqual(4, levels[2]);
            Assert.AreEqual(0f, experience.CurrentXp, 0.0001f);
        }

        private Experience CreateExperience(int startingLevel, float baseXp, float growth = 1f)
        {
            ProgressionDefinition progression = _fixtures.CreateAsset<ProgressionDefinition>(
                ("startingLevel", startingLevel),
                ("baseXp", baseXp),
                ("growth", growth));

            GameObject go = _fixtures.NewObject("Experience");
            Experience experience = go.AddComponent<Experience>();
            PlayModeFixtures.SetField(experience, "progression", progression);
            _fixtures.Activate(go);
            return experience;
        }
    }
}
