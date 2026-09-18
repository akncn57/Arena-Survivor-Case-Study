using ArenaSurvivor.Core.Enemies;
using UnityEngine;

namespace ArenaSurvivor.Unity.Views
{
    /// <summary>
    /// Draws one enemy: position, facing and walk/attack/death animation.
    /// Pooled by <see cref="ViewRegistry{TModel,TView}"/> and synced by the bootstrap; has no Update() of its own.
    /// </summary>
    public sealed class EnemyView : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        [Tooltip("Length of the attack clip, used to play it exactly once per attack interval.")]
        [SerializeField, Min(0.01f)] private float attackClipLength = 2.63f;

        private bool _inRange;

        /// <summary>Called when the view is taken from the pool for a newly spawned enemy.</summary>
        public void Begin(Enemy enemy, float attackInterval)
        {
            _inRange = false;
            SetFrozen(false);
            animator.SetBool(AnimatorIds.InRange, false);
            animator.ResetTrigger(AnimatorIds.Dead);
            // Speed the attack clip up or down so one swing lasts exactly one attack interval.
            animator.SetFloat(AnimatorIds.AttackSpeed, attackClipLength / attackInterval);
            // Random start point in the walk cycle, so a wave does not walk in lockstep.
            animator.Play(AnimatorIds.Walk, 0, Random.value);
            Sync(enemy);
        }

        public void Sync(Enemy enemy)
        {
            transform.SetPositionAndRotation(enemy.Position, Quaternion.LookRotation(enemy.Forward, Vector3.up));

            // Only touch the Animator when the value changes.
            if (enemy.IsInAttackRange != _inRange)
            {
                _inRange = enemy.IsInAttackRange;
                animator.SetBool(AnimatorIds.InRange, _inRange);
            }
        }

        public void PlayDeath()
        {
            animator.SetTrigger(AnimatorIds.Dead);
        }

        /// <summary>Pauses the animation in its current pose, e.g. behind the result screen.</summary>
        public void SetFrozen(bool frozen)
        {
            animator.speed = frozen ? 0f : 1f;
        }
    }
}
