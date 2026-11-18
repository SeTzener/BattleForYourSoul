using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using BattleForYourSoul.Combat;
using BattleForYourSoul.Player;
using BattleForYourSoul.Progression;

namespace BattleForYourSoul.UI
{
    /// <summary>
    /// Presents the level-up choice: it pauses the run, offers a random set of distinct upgrades,
    /// applies the picked one, and resumes. Several level-ups gained at once are queued and shown
    /// one after another.
    ///
    /// It reacts to <see cref="Experience.LeveledUp"/> and owns all time-scale changes for the
    /// choice, and yields to <see cref="GameOverController"/> if the player dies.
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
        [SerializeField] private UpgradeDefinition[] upgrades;

        private readonly List<UpgradeDefinition> _choices = new List<UpgradeDefinition>();
        private readonly List<UpgradeDefinition> _scratch = new List<UpgradeDefinition>();
        private int _pending;
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

        private void HandleLeveledUp(int _)
        {
            _pending++;
            if (!_showing && !_dead)
            {
                ShowNext();
            }
        }

        private void HandlePlayerDied(Health _)
        {
            _dead = true;
            _pending = 0;
            _showing = false;
            ClearCards();

            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void ShowNext()
        {
            if (_pending <= 0)
            {
                Hide();
                return;
            }

            _pending--;
            BuildChoices();

            if (_choices.Count == 0)
            {
                Hide();
                return;
            }

            _showing = true;
            Time.timeScale = 0f;

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
            if (stats != null)
            {
                stats.Apply(upgrade);
            }

            if (_pending > 0)
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

        private void BuildChoices()
        {
            _choices.Clear();
            _scratch.Clear();

            if (upgrades == null)
            {
                return;
            }

            for (int i = 0; i < upgrades.Length; i++)
            {
                if (upgrades[i] != null)
                {
                    _scratch.Add(upgrades[i]);
                }
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
