using UnityEngine;

namespace ArenaSurvivor.Unity.Views
{
    /// <summary>
    /// Animator parameter and state names, hashed once. Setting parameters by hash avoids a
    /// string lookup per call, which adds up with many enemies.
    /// </summary>
    public static class AnimatorIds
    {
        // Parameters
        public static readonly int Speed = Animator.StringToHash("Speed");
        public static readonly int InRange = Animator.StringToHash("InRange");
        public static readonly int AttackSpeed = Animator.StringToHash("AttackSpeed");
        public static readonly int Dead = Animator.StringToHash("Dead");

        // States
        public static readonly int Locomotion = Animator.StringToHash("Locomotion");
        public static readonly int Walk = Animator.StringToHash("Walk");
        public static readonly int Death = Animator.StringToHash("Death");
    }
}
