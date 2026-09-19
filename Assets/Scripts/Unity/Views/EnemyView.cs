using ArenaSurvivor.Core.Enemies;
using UnityEngine;

namespace ArenaSurvivor.Unity.Views
{
    /// <summary>
    /// Draws one enemy: position, facing, walk/attack/death animation and a short flash when it is hit.
    /// Pooled by <see cref="ViewRegistry{TModel,TView}"/> and synced by the bootstrap; has no Update() of its own.
    /// </summary>
    public sealed class EnemyView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Animator animator;

        [Tooltip("Length of the attack clip, used to play it exactly once per attack interval.")]
        [SerializeField, Min(0.01f)] private float attackClipLength = 2.63f;

        [Header("Hit flash")]
        [Tooltip("Base color multiplier while flashing. Values above 1 brighten the texture.")]
        [SerializeField] private Color flashColor = new Color(2.4f, 1.7f, 1.7f, 1f);
        [SerializeField, Min(0.01f)] private float flashSeconds = 0.1f;

        private SkinnedMeshRenderer[] _renderers;
        private MaterialPropertyBlock _flashBlock;
        private bool _inRange;
        private int _lastHealth;
        private float _flashLeft;

        private void Awake()
        {
            // Only the character meshes flash, not the blob shadow.
            _renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            _flashBlock = new MaterialPropertyBlock();
            _flashBlock.SetColor(BaseColorId, flashColor);
        }

        /// <summary>Called when the view is taken from the pool for a newly spawned enemy.</summary>
        public void Begin(Enemy enemy, float attackInterval)
        {
            _inRange = false;
            _lastHealth = enemy.Health.Current;
            StopFlash();
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

            // A drop in health since the last frame means a bullet hit. Reading the value avoids subscribing
            // to the enemy's events, which would have to be undone correctly every time the view is pooled.
            int health = enemy.Health.Current;
            if (health < _lastHealth)
            {
                StartFlash();
            }

            _lastHealth = health;
            UpdateFlash(Time.deltaTime);
        }

        public void PlayDeath()
        {
            // The killing hit never reaches Sync (the enemy is removed the same frame), so flash here.
            StartFlash();
            animator.SetTrigger(AnimatorIds.Dead);
        }

        /// <summary>Advances the flash; also called for corpses, which are no longer synced.</summary>
        public void UpdateFlash(float deltaTime)
        {
            if (_flashLeft <= 0f)
            {
                return;
            }

            _flashLeft -= deltaTime;
            if (_flashLeft <= 0f)
            {
                StopFlash();
            }
        }

        /// <summary>Pauses the animation in its current pose, e.g. behind the result screen.</summary>
        public void SetFrozen(bool frozen)
        {
            animator.speed = frozen ? 0f : 1f;
        }

        private void StartFlash()
        {
            _flashLeft = flashSeconds;
            foreach (SkinnedMeshRenderer r in _renderers)
            {
                r.SetPropertyBlock(_flashBlock);
            }
        }

        private void StopFlash()
        {
            _flashLeft = 0f;
            // Clearing the block puts the renderer back into the SRP Batcher; a property block only
            // breaks batching for the 0.1 s the flash lasts.
            foreach (SkinnedMeshRenderer r in _renderers)
            {
                r.SetPropertyBlock(null);
            }
        }
    }
}
