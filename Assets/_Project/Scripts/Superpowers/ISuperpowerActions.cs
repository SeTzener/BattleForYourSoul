using System;

namespace BattleForYourSoul.Superpowers
{
    /// <summary>
    /// The two attack intents a superpower understands. Implemented by the input-acquisition component
    /// so gameplay code never touches devices, keys or the Input System directly.
    /// </summary>
    public interface ISuperpowerActions
    {
        event Action PrimaryPressed;
        event Action SecondaryPressed;
    }
}
