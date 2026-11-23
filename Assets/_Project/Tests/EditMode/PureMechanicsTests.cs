using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using BattleForYourSoul.Combat;
using BattleForYourSoul.Core;
using BattleForYourSoul.Progression;
using BattleForYourSoul.Weapons;

namespace BattleForYourSoul.Tests.EditMode
{
    /// <summary>
    /// Mechanics that are pure maths or authoring-time data. These need no scene and no MonoBehaviour
    /// lifecycle, so they stay in fast EditMode tests.
    /// </summary>
    public sealed class PureMechanicsTests
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        [Test]
        public void IsometricCellToWorldRoundTrips()
        {
            Vector2 cell = new Vector2(3f, 1f);

            Vector2 world = IsometricUtility.CellToWorld(cell);
            Assert.AreEqual(1f, world.x, 0.0001f);
            Assert.AreEqual(1f, world.y, 0.0001f);

            Vector2 restored = IsometricUtility.WorldToCell(world);
            Assert.AreEqual(cell.x, restored.x, 0.0001f);
            Assert.AreEqual(cell.y, restored.y, 0.0001f);
        }

        [Test]
        public void IsometricDirectionMappingAndDepthAreIdentity()
        {
            Vector2 direction = new Vector2(0.6f, -0.8f);
            Assert.AreEqual(direction, IsometricUtility.ScreenDirectionToWorld(direction));
            Assert.AreEqual(direction, IsometricUtility.WorldDirectionToScreen(direction));
            Assert.AreEqual(4f, IsometricUtility.DepthKey(new Vector2(2f, 4f)), 0.0001f);
        }

        [Test]
        public void MonsterScalingTreatsLevelOneAsBaseline()
        {
            MonsterScaling scaling = CreateScaling(0.4f, 0.25f, 0.03f, 1f);

            Assert.AreEqual(1f, scaling.HealthMultiplier(1), 0.0001f);
            Assert.AreEqual(1f, scaling.DamageMultiplier(1), 0.0001f);
            Assert.AreEqual(1f, scaling.SpeedMultiplier(1), 0.0001f);
            Assert.AreEqual(1f, scaling.XpMultiplier(1), 0.0001f);

            Assert.AreEqual(1f, scaling.HealthMultiplier(0), 0.0001f);
            Assert.AreEqual(1f, scaling.XpMultiplier(-5), 0.0001f);
            Object.DestroyImmediate(scaling);
        }

        [Test]
        public void MonsterScalingGrowsPerLevelAboveOne()
        {
            MonsterScaling scaling = CreateScaling(0.4f, 0.25f, 0.03f, 1f);

            Assert.AreEqual(1.8f, scaling.HealthMultiplier(3), 0.0001f);
            Assert.AreEqual(1.5f, scaling.DamageMultiplier(3), 0.0001f);
            Assert.AreEqual(1.06f, scaling.SpeedMultiplier(3), 0.0001f);
            Assert.AreEqual(3f, scaling.XpMultiplier(3), 0.0001f);
            Object.DestroyImmediate(scaling);
        }

        [Test]
        public void ProgressionCurveStartsAtConfiguredLevelAndGrows()
        {
            ProgressionDefinition progression = ScriptableObject.CreateInstance<ProgressionDefinition>();
            SetField(progression, "startingLevel", 3);
            SetField(progression, "baseXp", 10f);
            SetField(progression, "growth", 2f);

            Assert.AreEqual(3, progression.StartingLevel);
            Assert.AreEqual(10f, progression.XpRequiredFor(3), 0.0001f);
            Assert.AreEqual(20f, progression.XpRequiredFor(4), 0.0001f);
            Assert.AreEqual(40f, progression.XpRequiredFor(5), 0.0001f);
            Assert.AreEqual(10f, progression.XpRequiredFor(2), 0.0001f);
            Object.DestroyImmediate(progression);
        }

        [Test]
        public void DefaultWeaponModifiersAreNeutral()
        {
            WeaponModifiers modifiers = WeaponModifiers.Default;

            Assert.AreEqual(1f, modifiers.DamageMultiplier, 0.0001f);
            Assert.AreEqual(1f, modifiers.FireRateMultiplier, 0.0001f);
            Assert.AreEqual(1f, modifiers.ProjectileSpeedMultiplier, 0.0001f);
            Assert.AreEqual(0f, modifiers.DamageFlat, 0.0001f);
            Assert.AreEqual(0f, modifiers.ExtraProjectiles, 0.0001f);
            Assert.AreEqual(0f, modifiers.ImpactRadius, 0.0001f);
        }

        [Test]
        public void UpgradeWeaponFilterOnlyAllowsListedWeapons()
        {
            WeaponDefinition sword = ScriptableObject.CreateInstance<WeaponDefinition>();
            WeaponDefinition bow = ScriptableObject.CreateInstance<WeaponDefinition>();
            UpgradeDefinition upgrade = ScriptableObject.CreateInstance<UpgradeDefinition>();
            SetField(upgrade, "weaponFilter", new[] { sword });

            Assert.IsTrue(upgrade.AppliesTo(sword));
            Assert.IsFalse(upgrade.AppliesTo(bow));
            Assert.IsFalse(upgrade.AppliesTo(null));

            SetField(upgrade, "weaponFilter", new WeaponDefinition[0]);
            Assert.IsTrue(upgrade.AppliesTo(bow));
            Assert.IsTrue(upgrade.AppliesTo(null));

            Object.DestroyImmediate(upgrade);
            Object.DestroyImmediate(sword);
            Object.DestroyImmediate(bow);
        }

        private static MonsterScaling CreateScaling(float health, float damage, float speed, float xp)
        {
            MonsterScaling scaling = ScriptableObject.CreateInstance<MonsterScaling>();
            SetField(scaling, "healthGrowthPerLevel", health);
            SetField(scaling, "damageGrowthPerLevel", damage);
            SetField(scaling, "speedGrowthPerLevel", speed);
            SetField(scaling, "xpGrowthPerLevel", xp);
            return scaling;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, Fields);
            Assert.That(field, Is.Not.Null, $"Field {name} was not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
