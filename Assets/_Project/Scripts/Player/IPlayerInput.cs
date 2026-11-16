using UnityEngine;

namespace BattleForYourSoul.Player
{
    /// <summary>
    /// A source of movement intent. Gameplay reads this interface, never the Input System, so
    /// keyboard, gamepad and (later) a virtual joystick can all feed the same normalised vector.
    /// </summary>
    public interface IPlayerInput
    {
        /// <summary>Normalised movement intent (magnitude 0..1). Source-agnostic.</summary>
        Vector2 Move { get; }
    }
}
