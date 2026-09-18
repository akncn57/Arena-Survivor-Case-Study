using ArenaSurvivor.Core.Player;
using UnityEngine;

namespace ArenaSurvivor.Unity.Views
{
    /// <summary>
    /// Draws the player: copies position from the simulation and turns smoothly towards its facing.
    /// Has no Update(); the bootstrap calls <see cref="Sync"/> once per frame.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        [Tooltip("Degrees per second the model turns towards the simulated facing.")]
        [SerializeField, Min(0f)] private float turnSpeed = 900f;

        public void Sync(PlayerCharacter player, float deltaTime)
        {
            transform.position = player.Position;

            Quaternion target = Quaternion.LookRotation(player.Forward, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * deltaTime);
        }

        /// <summary>Jumps straight to the simulated state, e.g. at the start of a run.</summary>
        public void Snap(PlayerCharacter player)
        {
            transform.SetPositionAndRotation(player.Position, Quaternion.LookRotation(player.Forward, Vector3.up));
        }
    }
}
