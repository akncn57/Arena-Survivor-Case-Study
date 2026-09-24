namespace ArenaSurvivor.Core.Upgrades
{
    /// <summary>What an upgrade card changes. The effect of each kind is applied by <see cref="UpgradeSystem"/>.</summary>
    public enum UpgradeKind
    {
        /// <summary>Damage per projectile, +value (fraction) per level.</summary>
        Damage,

        /// <summary>Shots per second, +value (fraction) per level.</summary>
        AttackSpeed,

        /// <summary>Extra projectiles per shot, +value per level.</summary>
        Multishot,

        /// <summary>Targeting range and projectile distance, +value (fraction) per level.</summary>
        Range,

        /// <summary>Max health, +value points per level (also heals by that much).</summary>
        MaxHealth,

        /// <summary>Movement speed, +value (fraction) per level.</summary>
        MoveSpeed,

        /// <summary>Pickup magnet radius, +value (fraction) per level.</summary>
        Magnet,

        /// <summary>Restores value (fraction) of max health. Offered only when too few other cards are left.</summary>
        Heal
    }
}
