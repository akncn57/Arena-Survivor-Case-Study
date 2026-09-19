using ArenaSurvivor.Core.Player;
using UnityEngine;

namespace ArenaSurvivor.Unity.Views
{
    /// <summary>
    /// Draws the player: copies position from the simulation, turns smoothly towards its facing,
    /// and drives the idle/run blend and the death animation.
    /// Has no Update(); the bootstrap calls <see cref="Sync"/> once per frame.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        [Tooltip("Degrees per second the model turns towards the simulated facing.")]
        [SerializeField, Min(0f)] private float turnSpeed = 900f;

        [Tooltip("Smoothing time of the idle/run blend, so a joystick flick does not snap the pose.")]
        [SerializeField, Min(0f)] private float speedDampTime = 0.08f;

        [Tooltip("Flash at the rifle muzzle, shown briefly on every shot.")]
        [SerializeField] private GameObject muzzleFlash;
        [SerializeField, Min(0.01f)] private float muzzleFlashSeconds = 0.05f;

        private float _muzzleFlashLeft;

        public void Sync(PlayerCharacter player, float deltaTime)
        {
            transform.position = player.Position;

            Quaternion target = Quaternion.LookRotation(player.Forward, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * deltaTime);

            animator.SetFloat(AnimatorIds.Speed, player.SpeedFraction, speedDampTime, deltaTime);

            if (_muzzleFlashLeft > 0f)
            {
                _muzzleFlashLeft -= deltaTime;
                if (_muzzleFlashLeft <= 0f)
                {
                    muzzleFlash.SetActive(false);
                }
            }
        }

        /// <summary>Connected to <c>Weapon.Fired</c>.</summary>
        public void OnFired()
        {
            if (muzzleFlash == null)
            {
                return;
            }

            // A random roll around the barrel, so consecutive flashes do not look identical.
            muzzleFlash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            muzzleFlash.SetActive(true);
            _muzzleFlashLeft = muzzleFlashSeconds;
        }

        /// <summary>Jumps straight to the simulated state and resets the animation, e.g. at the start of a run.</summary>
        public void Snap(PlayerCharacter player)
        {
            transform.SetPositionAndRotation(player.Position, Quaternion.LookRotation(player.Forward, Vector3.up));
            animator.ResetTrigger(AnimatorIds.Dead);
            animator.SetFloat(AnimatorIds.Speed, 0f);
            animator.Play(AnimatorIds.Locomotion, 0, 0f);
            _muzzleFlashLeft = 0f;
            if (muzzleFlash != null)
            {
                muzzleFlash.SetActive(false);
            }
        }

        public void PlayDeath()
        {
            animator.SetTrigger(AnimatorIds.Dead);
        }
    }
}
