using ArenaSurvivor.Core.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ArenaSurvivor.Unity.Input
{
    /// <summary>
    /// Floating on-screen joystick. Sits on a transparent touch area; the base appears where the
    /// finger touches down and hides on release. All maths live in <see cref="JoystickModel"/>;
    /// this component only converts pointer events to canvas space and moves the two images.
    /// </summary>
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Tooltip("Transparent area that receives touches (usually the lower part of the screen).")]
        [SerializeField] private RectTransform touchArea;

        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;

        [Tooltip("How far the handle can move from the centre, in canvas units.")]
        [SerializeField, Min(1f)] private float radius = 110f;

        [Tooltip("Fraction of the radius ignored around the centre.")]
        [SerializeField, Range(0f, 0.9f)] private float deadZone = 0.1f;

        private JoystickModel _model;

        /// <summary>Movement input with length 0..1.</summary>
        public Vector2 Value => _model?.Value ?? Vector2.zero;

        public bool IsPressed => _model != null && _model.IsPressed;

        private void Awake()
        {
            _model = new JoystickModel(radius, deadZone);
            SetVisible(false);
        }

        private void OnDisable()
        {
            // A disabled joystick must not keep the player walking.
            _model?.Release();
            SetVisible(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!TryGetLocalPoint(eventData, out Vector2 point))
            {
                return;
            }

            _model.Press(point);
            background.anchoredPosition = point;
            handle.anchoredPosition = Vector2.zero;
            SetVisible(true);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!TryGetLocalPoint(eventData, out Vector2 point))
            {
                return;
            }

            _model.Drag(point);
            handle.anchoredPosition = _model.HandleOffset;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _model.Release();
            SetVisible(false);
        }

        private bool TryGetLocalPoint(PointerEventData eventData, out Vector2 point)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                touchArea, eventData.position, eventData.pressEventCamera, out point);
        }

        private void SetVisible(bool visible)
        {
            if (background != null)
            {
                background.gameObject.SetActive(visible);
            }
        }
    }
}
