using System;
using UnityEngine;

namespace MechaFind3D
{
    /// <summary>
    /// Manages the player's persistent coin balance using PlayerPrefs.
    /// Provides thread-safe and game-wide access to coin balance, spending, and rewards.
    /// </summary>
    public static class CoinManager
    {
        private const string PrefsKey = "MechaFind3D_PlayerCoins";
        private const string InitializedKey = "MechaFind3D_CoinsInitialized";
        public const int DefaultStartingCoins = 10;

        public static event Action<int> OnCoinsChanged;

        static CoinManager()
        {
            EnsureInitialized();
        }

        private static void EnsureInitialized()
        {
            if (PlayerPrefs.GetInt(InitializedKey, 0) == 0)
            {
                PlayerPrefs.SetInt(PrefsKey, DefaultStartingCoins);
                PlayerPrefs.SetInt(InitializedKey, 1);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Current coin balance of the player.
        /// </summary>
        public static int Coins
        {
            get
            {
                EnsureInitialized();
                return PlayerPrefs.GetInt(PrefsKey, DefaultStartingCoins);
            }
            set
            {
                int clamped = Mathf.Max(0, value);
                PlayerPrefs.SetInt(PrefsKey, clamped);
                PlayerPrefs.SetInt(InitializedKey, 1);
                PlayerPrefs.Save();
                OnCoinsChanged?.Invoke(clamped);
            }
        }

        /// <summary>
        /// Checks if the player has at least the required amount of coins.
        /// </summary>
        public static bool HasCoins(int amount)
        {
            if (amount <= 0) return true;
            return Coins >= amount;
        }

        /// <summary>
        /// Attempts to deduct the specified amount of coins.
        /// Returns true if successful, false if insufficient coins.
        /// </summary>
        public static bool TrySpendCoins(int amount)
        {
            if (amount <= 0) return true;

            int current = Coins;
            if (current >= amount)
            {
                Coins = current - amount;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Adds coins to the player's balance and persists changes.
        /// </summary>
        public static void AddCoins(int amount)
        {
            if (amount <= 0) return;
            Coins += amount;
        }

        /// <summary>
        /// Resets the coin balance to a specific amount (useful for debugging/testing).
        /// </summary>
        public static void ResetCoins(int amount = DefaultStartingCoins)
        {
            PlayerPrefs.SetInt(PrefsKey, Mathf.Max(0, amount));
            PlayerPrefs.SetInt(InitializedKey, 1);
            PlayerPrefs.Save();
            OnCoinsChanged?.Invoke(Coins);
        }
    }
}
