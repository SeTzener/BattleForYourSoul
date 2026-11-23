using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BattleForYourSoul.Combat;
using BattleForYourSoul.Progression;
using BattleForYourSoul.Weapons;

namespace BattleForYourSoul.Tests.PlayMode
{
    public sealed class UpgradeTests
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
        public void UpgradesAccumulateAndPushToHealthWeaponMovementAndDash()
        {
            TestPlayer player = _fixtures.CreatePlayer(withWeapon: true);
            UpgradeDefinition upgrade = CreateUpgrade(
                new StatModifier(UpgradeStat.MaxHealthFlat, 20f),
                new StatModifier(UpgradeStat.DamageFlat, 5f),
                new StatModifier(UpgradeStat.DamagePercent, 10f),
                new StatModifier(UpgradeStat.FireRatePercent, 50f),
                new StatModifier(UpgradeStat.MoveSpeedPercent, 20f),
                new StatModifier(UpgradeStat.InstantDashCharges, 1f));

            player.Stats.Apply(upgrade);

            Assert.AreEqual(120f, player.Health.Max, 0.0001f);
            Assert.AreEqual(120f, player.Health.Current, 0.0001f);

            WeaponModifiers modifiers = PlayModeFixtures.GetField<WeaponModifiers>(player.Weapon, "_modifiers");
            Assert.AreEqual(5f, modifiers.DamageFlat, 0.0001f);
            Assert.AreEqual(1.1f, modifiers.DamageMultiplier, 0.0001f);
            Assert.AreEqual(1.5f, modifiers.FireRateMultiplier, 0.0001f);
            Assert.AreEqual(1.2f, PlayModeFixtures.GetField<float>(player.Movement, "_speedMultiplier"), 0.0001f);
            Assert.AreEqual(1, PlayModeFixtures.GetField<int>(player.Dash, "_grantedInstantCharges"));

            // The same choice can be offered twice; its effects must stack, not replace.
            player.Stats.Apply(upgrade);
            Assert.AreEqual(140f, player.Health.Max, 0.0001f);
            Assert.AreEqual(140f, player.Health.Current, 0.0001f);
            modifiers = PlayModeFixtures.GetField<WeaponModifiers>(player.Weapon, "_modifiers");
            Assert.AreEqual(1.21f, modifiers.DamageMultiplier, 0.0001f);
            Assert.AreEqual(2, PlayModeFixtures.GetField<int>(player.Dash, "_grantedInstantCharges"));
        }

        [Test]
        public void SwappingWeaponKeepsAccumulatedModifiers()
        {
            TestPlayer player = _fixtures.CreatePlayer(withWeapon: true);
            UpgradeDefinition upgrade = CreateUpgrade(new StatModifier(UpgradeStat.DamagePercent, 25f));
            player.Stats.Apply(upgrade);

            WeaponDefinition replacement = _fixtures.CreateAsset<WeaponDefinition>();
            player.Weapon.SetDefinition(replacement);

            WeaponModifiers modifiers = PlayModeFixtures.GetField<WeaponModifiers>(player.Weapon, "_modifiers");
            Assert.AreEqual(1.25f, modifiers.DamageMultiplier, 0.0001f);
        }

        [Test]
        public void NullUpgradeIsIgnored()
        {
            TestPlayer player = _fixtures.CreatePlayer(withWeapon: false);
            player.Stats.Apply(null);

            Assert.AreEqual(100f, player.Health.Max, 0.0001f);
            Assert.AreEqual(1f, PlayModeFixtures.GetField<float>(player.Movement, "_speedMultiplier"), 0.0001f);
        }

        private UpgradeDefinition CreateUpgrade(params StatModifier[] modifiers)
        {
            UpgradeDefinition upgrade = _fixtures.CreateAsset<UpgradeDefinition>();
            PlayModeFixtures.SetField(upgrade, "modifiers", new List<StatModifier>(modifiers));
            return upgrade;
        }
    }

    public sealed class PlayerDashTests
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
        public IEnumerator DashUsesTheNormalChargeAndBlocksTheNextPress()
        {
            TestPlayer player = _fixtures.CreatePlayer(
                withWeapon: false,
                dashCooldown: 5f,
                dashDuration: 0.05f,
                dashGrace: 0.1f);

            player.Input.PressDash();
            Assert.IsTrue(player.Dash.IsDashing);
            Assert.IsTrue(player.Movement.IsDashing);
            Assert.IsTrue(player.Health.IsDamageImmune);

            player.Health.TakeDamage(new DamageInfo(50f, Vector2.zero, Vector2.zero));
            Assert.AreEqual(100f, player.Health.Current, 0.0001f);

            yield return new WaitForSeconds(0.08f);
            Assert.IsFalse(player.Dash.IsDashing);

            // Normal charge is recharging and no instant charge was granted.
            player.Input.PressDash();
            Assert.IsFalse(player.Dash.IsDashing);
            Assert.IsTrue(player.Health.IsDamageImmune);

            yield return new WaitForSeconds(0.2f);
            Assert.IsFalse(player.Health.IsDamageImmune);
        }

        [UnityTest]
        public IEnumerator InstantChargeRefillsAfterEveryDash()
        {
            TestPlayer player = _fixtures.CreatePlayer(
                withWeapon: false,
                dashCooldown: 5f,
                dashDuration: 0.05f,
                dashGrace: 0f);
            player.Dash.SetInstantCharges(1);

            player.Input.PressDash(); // normal charge
            yield return new WaitForSeconds(0.08f);
            Assert.IsFalse(player.Dash.IsDashing);

            player.Input.PressDash(); // granted instant charge
            Assert.IsTrue(player.Dash.IsDashing);
            yield return new WaitForSeconds(0.08f);
            Assert.IsFalse(player.Dash.IsDashing);

            player.Input.PressDash(); // instant charge refilled at the end of the previous dash
            Assert.IsTrue(player.Dash.IsDashing);
            yield return new WaitForSeconds(0.08f);
        }

        [Test]
        public void TeleportCancelsDashAndGrace()
        {
            TestPlayer player = _fixtures.CreatePlayer(
                withWeapon: false,
                dashDuration: 5f,
                dashGrace: 0.2f);

            player.Input.PressDash();
            Assert.IsTrue(player.Dash.IsDashing);
            Assert.IsTrue(player.Health.IsDamageImmune);

            player.Movement.TeleportTo(new Vector2(2f, 3f));

            Assert.IsFalse(player.Dash.IsDashing);
            Assert.IsFalse(player.Movement.IsDashing);
            Assert.IsFalse(player.Health.IsDamageImmune);
            Assert.AreEqual(2f, player.GameObject.transform.position.x, 0.0001f);
            Assert.AreEqual(3f, player.GameObject.transform.position.y, 0.0001f);
        }

        [Test]
        public void DashCannotStartWhileTimeIsFrozen()
        {
            TestPlayer player = _fixtures.CreatePlayer(withWeapon: false);
            Time.timeScale = 0f;

            player.Input.PressDash();
            Assert.IsFalse(player.Dash.IsDashing);
            Assert.IsFalse(player.Health.IsDamageImmune);
        }
    }
}
