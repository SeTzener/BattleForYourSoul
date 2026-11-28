using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using BattleForYourSoul.Combat;
using BattleForYourSoul.Player;
using BattleForYourSoul.Progression;
using BattleForYourSoul.Superpowers;

namespace BattleForYourSoul.UI
{
    /// <summary>
    /// Presents the upgrade choice: it pauses the run, offers a random set of distinct upgrades,
    /// applies the picked one, and resumes. Level-ups and door-picked stage-clear rewards share one
    /// queue, so a category choice never interrupts an already-open panel.
    ///
    /// XP level-ups and stage-clear rewards draw from separate <see cref="UpgradeSource"/> pools.
    /// A queued category restricts the stage pool; it never pulls level-up cards into a stage reward.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelUpController : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private Experience experience;
        [SerializeField] private PlayerStats stats;
        [SerializeField] private Health playerHealth;
        [SerializeField] private GameObject panel;
        [SerializeField] private UpgradeCardView[] cards;
        [SerializeField] private UpgradePool pool;
        [SerializeField] private SuperpowerSelection superpowerSelection;
        [SerializeField] private RunUpgradeState runUpgrades;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text hintLabel;

        private readonly List<UpgradeDefinition> _choices = new List<UpgradeDefinition>();
        private readonly List<UpgradeDefinition> _scratch = new List<UpgradeDefinition>();

        // A null category is a normal XP level-up; a non-null one is a door-picked clear reward.
        private readonly Queue<UpgradeCategoryDefinition> _queue = new Queue<UpgradeCategoryDefinition>();
        private bool _showing;
        private bool _dead;

        private void Awake()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }

            ClearCards();
        }

        private void OnEnable()
        {
            if (experience != null)
            {
                experience.LeveledUp += HandleLeveledUp;
            }

            if (playerHealth != null)
            {
                playerHealth.Died += HandlePlayerDied;
            }
        }

        private void OnDisable()
        {
            if (experience != null)
            {
                experience.LeveledUp -= HandleLeveledUp;
            }

            if (playerHealth != null)
            {
                playerHealth.Died -= HandlePlayerDied;
            }
        }

        /// <summary>
        /// Clears any pending reward, closes the panel and resumes time. Called when a fresh Samsara
        /// stage starts so a reward picked at the previous exit cannot leak into the new run.
        /// </summary>
        public void ResetForNewRun()
        {
            _queue.Clear();
            _showing = false;
            _dead = false;
            ClearCards();

            if (panel != null)
            {
                panel.SetActive(false);
            }

            Time.timeScale = 1f;
        }

        /// <summary>
        /// Queues a filtered choice for clearing a stage after the player picked this category at a
        /// door. If a panel is already open, the category reward appears after it is answered.
        /// </summary>
        public void QueueCategoryReward(UpgradeCategoryDefinition category)
        {
            if (category == null || _dead)
            {
                return;
            }

            Enqueue(category);
        }

        private void Enqueue(UpgradeCategoryDefinition category)
        {
            if (_dead)
            {
                return;
            }

            _queue.Enqueue(category);
            if (!_showing)
            {
                ShowNext();
            }
        }

        private void HandleLeveledUp(int _)
        {
            Enqueue(null);
        }

        private void HandlePlayerDied(Health _)
        {
            _dead = true;
            _queue.Clear();
            _showing = false;
            ClearCards();

            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void ShowNext()
        {
            if (_queue.Count == 0)
            {
                Hide();
                return;
            }

            UpgradeCategoryDefinition category = _queue.Dequeue();
            UpgradeSource source = category != null ? UpgradeSource.StageClear : UpgradeSource.LevelUp;
            SuperpowerDefinition superpower = superpowerSelection != null ? superpowerSelection.SelectedSuperpower : null;
            BuildChoices(category, source, superpower);

            if (_choices.Count == 0)
            {
                Hide();
                return;
            }

            _showing = true;
            Time.timeScale = 0f;

            if (titleLabel != null)
            {
                titleLabel.text = category != null
                    ? "STAGE UPGRADE"
                    : "LEVEL UP";
            }

            if (hintLabel != null)
            {
                hintLabel.text = category != null
                    ? $"Choose a stage upgrade ({category.DisplayName})"
                    : "Choose an upgrade";
            }

            if (panel != null)
            {
                panel.SetActive(true);
            }

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null)
                {
                    continue;
                }

                if (i < _choices.Count)
                {
                    int index = i;
                    cards[i].Bind(_choices[index], () => Choose(_choices[index]));
                }
                else
                {
                    cards[i].Clear();
                }
            }

            // Select the first card so a gamepad can navigate the choice immediately.
            if (EventSystem.current != null && cards.Length > 0 && cards[0] != null && cards[0].gameObject.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(cards[0].gameObject);
            }
        }

        private void Choose(UpgradeDefinition upgrade)
        {
            // Record the pick before applying it: PlayerStats pushes the resulting modifiers into the
            // superpower controller, which reads action overrides back from this run state. A pick that
            // became invalid (for example a form that was taken by another queued card) is rejected.
            bool accepted = runUpgrades == null || runUpgrades.MarkTaken(upgrade);
            if (accepted && stats != null)
            {
                stats.Apply(upgrade);
            }

            if (_queue.Count > 0)
            {
                ShowNext();
            }
            else
            {
                Hide();
            }
        }

        private void Hide()
        {
            _showing = false;
            ClearCards();

            if (panel != null)
            {
                panel.SetActive(false);
            }

            if (!_dead)
            {
                Time.timeScale = 1f;
            }
        }

        private void BuildChoices(UpgradeCategoryDefinition category, UpgradeSource source, SuperpowerDefinition superpower)
        {
            _choices.Clear();
            Collect(source, category, superpower);

            // A door may advertise a category with no authored cards. Fall back inside the same
            // source so a stage reward never spills into the level-up pool.
            if (_choices.Count == 0 && source == UpgradeSource.StageClear)
            {
                Collect(source, null, superpower);
            }
        }

        private void Collect(UpgradeSource source, UpgradeCategoryDefinition category, SuperpowerDefinition superpower)
        {
            _scratch.Clear();

            if (pool == null)
            {
                return;
            }

            for (int i = 0; i < pool.Count; i++)
            {
                UpgradeDefinition upgrade = pool.Get(i);
                if (upgrade == null)
                {
                    continue;
                }

                if (upgrade.Source != source)
                {
                    continue;
                }

                if (category != null && upgrade.Category != category)
                {
                    continue;
                }

                if (!upgrade.AppliesTo(superpower))
                {
                    continue;
                }

                // Unique and mutually exclusive upgrades disappear once taken; prerequisites hide
                // until their parent upgrade has been picked.
                if (runUpgrades != null && !runUpgrades.CanOffer(upgrade))
                {
                    continue;
                }

                _scratch.Add(upgrade);
            }

            int want = Mathf.Min(cards.Length, _scratch.Count);
            for (int i = 0; i < want; i++)
            {
                int pick = Random.Range(i, _scratch.Count);
                UpgradeDefinition chosen = _scratch[pick];
                _scratch[pick] = _scratch[i];
                _scratch[i] = chosen;
                _choices.Add(chosen);
            }
        }

        private void ClearCards()
        {
            if (cards == null)
            {
                return;
            }

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null)
                {
                    cards[i].Clear();
                }
            }
        }
    }
}
