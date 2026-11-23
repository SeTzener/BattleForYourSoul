using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BattleForYourSoul.Combat;
using BattleForYourSoul.Infrastructure;
using BattleForYourSoul.VFX;
using BattleForYourSoul.Weapons;

namespace BattleForYourSoul.Tests.PlayMode
{
    /// <summary>
    /// Weapon and projectile mechanics on minimal in-code fixtures. Physics queries use the real
    /// 2D layer setup, but no generated scene or authored content is involved.
    /// </summary>
    public sealed class CombatWeaponsTests
    {
        private const int EnemyLayer = 9;
        private const int PlayerLayer = 8;

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
        public IEnumerator PrimaryAttackRespectsFireRateCooldown()
        {
            TestPlayer player = CreateArmedPlayer();
            WeaponActionDefinition action = _fixtures.CreateWeaponAction(
                WeaponAttackShape.MeleeArc,
                cooldown: 0.4f,
                damage: 7f,
                range: 3f,
                arcDegrees: 90f);
            player.Weapon.SetDefinition(_fixtures.CreateWeapon(action));
            Health target = _fixtures.CreateTarget("Front", new Vector2(1.5f, 0f), EnemyLayer);

            yield return FaceRight(player);

            player.Weapon.TryUsePrimary();
            Assert.AreEqual(93f, target.Current, 0.0001f);

            // Same frame: still cooling down, so the second press does nothing.
            player.Weapon.TryUsePrimary();
            Assert.AreEqual(93f, target.Current, 0.0001f);

            yield return new WaitForSeconds(0.45f);
            player.Weapon.TryUsePrimary();
            Assert.AreEqual(86f, target.Current, 0.0001f);
        }

        [UnityTest]
        public IEnumerator MeleeArcFiltersByLayerAndFacingAndAppliesModifiers()
        {
            TestPlayer player = CreateArmedPlayer();
            WeaponActionDefinition action = _fixtures.CreateWeaponAction(
                WeaponAttackShape.MeleeArc,
                damage: 4f,
                range: 3f,
                arcDegrees: 60f);
            player.Weapon.SetDefinition(_fixtures.CreateWeapon(action));

            WeaponModifiers modifiers = WeaponModifiers.Default;
            modifiers.DamageFlat = 2f;
            modifiers.DamageMultiplier = 1.5f;
            player.Weapon.SetModifiers(modifiers);

            Health front = _fixtures.CreateTarget("Front", new Vector2(1.5f, 0f), EnemyLayer);
            Health behind = _fixtures.CreateTarget("Behind", new Vector2(-1.5f, 0f), EnemyLayer);
            Health wrongLayer = _fixtures.CreateTarget("WrongLayer", new Vector2(1.5f, 1f), PlayerLayer);

            yield return FaceRight(player);

            player.Weapon.TryUsePrimary();

            // (4 flat + 2) * 1.5 = 9 damage, and only the enemy in front is in the arc and mask.
            Assert.AreEqual(91f, front.Current, 0.0001f);
            Assert.AreEqual(100f, behind.Current, 0.0001f);
            Assert.AreEqual(100f, wrongLayer.Current, 0.0001f);
        }

        [UnityTest]
        public IEnumerator ImpactRadiusReachesNeighboursOutsideTheInitialArc()
        {
            TestPlayer player = CreateArmedPlayer();
            WeaponActionDefinition action = _fixtures.CreateWeaponAction(
                WeaponAttackShape.MeleeArc,
                damage: 4f,
                range: 3f,
                arcDegrees: 60f);
            player.Weapon.SetDefinition(_fixtures.CreateWeapon(action));

            WeaponModifiers modifiers = WeaponModifiers.Default;
            modifiers.ImpactRadius = 2f;
            player.Weapon.SetModifiers(modifiers);

            Health primary = _fixtures.CreateTarget("Primary", new Vector2(3f, 0f), EnemyLayer, radius: 0.4f);
            Health neighbour = _fixtures.CreateTarget("Neighbour", new Vector2(3f, 1.8f), EnemyLayer, radius: 0.4f);
            Health distant = _fixtures.CreateTarget("Distant", new Vector2(-3f, 0f), EnemyLayer);

            yield return FaceRight(player);

            player.Weapon.TryUsePrimary();

            Assert.AreEqual(96f, primary.Current, 0.0001f);
            Assert.AreEqual(96f, neighbour.Current, 0.0001f);
            Assert.AreEqual(100f, distant.Current, 0.0001f);
        }

        [Test]
        public void ProjectileActionFiresExtraShotsWithSpeedModifier()
        {
            TestPlayer player = CreateArmedPlayer();
            Sprite sprite = _fixtures.CreateSprite("Bolt");
            WeaponActionDefinition action = _fixtures.CreateWeaponAction(
                WeaponAttackShape.Projectile,
                cooldown: 1f,
                damage: 3f,
                projectileSprite: sprite,
                projectilesPerShot: 1,
                spreadDegrees: 30f,
                projectileSpeed: 10f,
                projectileLifetime: 1f);
            player.Weapon.SetDefinition(_fixtures.CreateWeapon(action));

            WeaponModifiers modifiers = WeaponModifiers.Default;
            modifiers.ExtraProjectiles = 2f;
            modifiers.ProjectileSpeedMultiplier = 1.5f;
            player.Weapon.SetModifiers(modifiers);

            player.Weapon.TryUsePrimary();

            Transform poolRoot = player.Weapon.transform.Find("Projectiles");
            Assert.That(poolRoot, Is.Not.Null);
            Assert.AreEqual(3, poolRoot.childCount);

            Vector2 firstDirection = Vector2.zero;
            Vector2 lastDirection = Vector2.zero;
            for (int i = 0; i < poolRoot.childCount; i++)
            {
                Transform shot = poolRoot.GetChild(i);
                Assert.IsTrue(shot.gameObject.activeSelf);
                Rigidbody2D body = shot.GetComponent<Rigidbody2D>();
                Assert.AreEqual(15f, body.linearVelocity.magnitude, 0.0001f);

                if (i == 0)
                {
                    firstDirection = body.linearVelocity.normalized;
                }
                else if (i == poolRoot.childCount - 1)
                {
                    lastDirection = body.linearVelocity.normalized;
                }
            }

            Assert.That(Vector2.Distance(firstDirection, lastDirection), Is.GreaterThan(0.1f));
        }

        [UnityTest]
        public IEnumerator ProjectileLifetimeReturnsItToThePoolForReuse()
        {
            Projectile prefab = _fixtures.CreateProjectilePrefab();
            GameObject poolObject = _fixtures.NewObject("Pool");
            _fixtures.Activate(poolObject);
            Transform parent = poolObject.transform;
            int releases = 0;
            PrefabPool<Projectile> pool = null;
            pool = new PrefabPool<Projectile>(prefab, parent, 1, 4, p => p.SetReleaseCallback(shotFromPool =>
            {
                releases++;
                pool.Release(shotFromPool);
            }));

            Projectile shot = pool.Get();
            shot.Launch(new Vector2(1f, 2f), Vector2.right, 5f, 2f, 0.1f, null);
            Assert.AreEqual(1.2f, shot.transform.position.x, 0.0001f);
            Assert.AreEqual(5f, shot.GetComponent<Rigidbody2D>().linearVelocity.magnitude, 0.0001f);

            yield return new WaitForSeconds(0.15f);

            Assert.AreEqual(1, releases);
            Assert.IsFalse(shot.gameObject.activeSelf);
            Assert.AreEqual(0f, shot.GetComponent<Rigidbody2D>().linearVelocity.magnitude, 0.0001f);

            Projectile reused = pool.Get();
            Assert.AreSame(shot, reused);
            reused.Launch(Vector2.zero, Vector2.up, 7f, 2f, 1f, null);
            Assert.IsTrue(reused.gameObject.activeSelf);
            Assert.AreEqual(7f, reused.GetComponent<Rigidbody2D>().linearVelocity.magnitude, 0.0001f);
        }

        [UnityTest]
        public IEnumerator AttackVfxSweepsFadesAndReturnsToThePool()
        {
            AttackVfx prefab = _fixtures.CreateVfxPrefab(1.5f);
            GameObject poolObject = _fixtures.NewObject("VfxPool");
            _fixtures.Activate(poolObject);
            Transform parent = poolObject.transform;
            int releases = 0;
            PrefabPool<AttackVfx> pool = null;
            pool = new PrefabPool<AttackVfx>(prefab, parent, 1, 4, v => v.SetReleaseCallback(vfxFromPool =>
            {
                releases++;
                pool.Release(vfxFromPool);
            }));

            AttackVfx vfx = pool.Get();
            SpriteRenderer renderer = vfx.GetComponent<SpriteRenderer>();
            vfx.Play(new Vector2(2f, 3f), Vector2.right, null, new Color(1f, 0f, 0f, 1f), 2f, 0.2f, 90f);

            Assert.AreEqual(new Vector2(2f, 3f), (Vector2)vfx.transform.position);
            Assert.AreEqual(2f, vfx.transform.localScale.x, 0.0001f);
            Assert.AreEqual(-45f, Mathf.DeltaAngle(0f, vfx.transform.eulerAngles.z), 0.0001f);
            Assert.AreEqual(1f, renderer.color.a, 0.0001f);

            yield return new WaitForSeconds(0.1f);

            Assert.That(renderer.color.a, Is.EqualTo(0.5f).Within(0.05f));
            Assert.That(vfx.transform.localScale.x, Is.GreaterThan(2f));
            Assert.That(Mathf.DeltaAngle(0f, vfx.transform.eulerAngles.z), Is.GreaterThan(-45f));

            yield return new WaitForSeconds(0.15f);

            Assert.AreEqual(1, releases);
            Assert.IsFalse(vfx.gameObject.activeSelf);

            Assert.AreSame(vfx, pool.Get());
        }

        private TestPlayer CreateArmedPlayer()
        {
            return _fixtures.CreatePlayer(withWeapon: true, enemyMask: 1 << EnemyLayer);
        }

        private IEnumerator FaceRight(TestPlayer player)
        {
            player.Input.Move = Vector2.right;
            yield return null;

            // Keep the player still for the rest of the test: a held move direction would carry the
            // kinematic body out of melee range while the test waits out a cooldown.
            player.Input.Move = Vector2.zero;
            player.Movement.TeleportTo(Vector2.zero);
            Assert.AreEqual(Vector2.right, player.Movement.Facing);
            Physics2D.SyncTransforms();
        }
    }
}
