using UnityEngine;

namespace ArenaSurvivor.Core.Weapons
{
    /// <summary>Asset wrapper for <see cref="WeaponConfig"/>. Holds no logic.</summary>
    [CreateAssetMenu(fileName = "Weapon", menuName = "Arena Survivor/Weapon Definition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField] private WeaponConfig config = new WeaponConfig();

        public WeaponConfig Config => config;
    }
}
