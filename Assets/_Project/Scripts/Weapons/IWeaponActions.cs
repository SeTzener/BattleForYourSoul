using System;

namespace BattleForYourSoul.Weapons
{
    /// <summary>
    /// The two attack intents a weapon understands. Implemented by the input-acquisition component
    /// so gameplay code never touches devices, keys or the Input System directly.
    /// </summary>
    public interface IWeaponActions
    {
        event Action PrimaryPressed;
        event Action SecondaryPressed;
    }
}
