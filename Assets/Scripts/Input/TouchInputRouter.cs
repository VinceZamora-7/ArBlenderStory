using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ARLearning.Input
{
    public sealed class TouchInputRouter : MonoBehaviour
    {
        const float TapTravelPixels = 24f;
        Vector2 _start;
        bool _pressed;
        public Vector2 PointerPosition { get; private set; }
            public event Action<Vector2> TouchStarted;
            public event Action<Vector2> TouchMoved;
            public event Action<Vector2> TouchEnded;
        public event Action<Vector2> Tap;
        public event Action<float> PinchDelta;
        float _previousPinchDistance;

        void Update()
        {
            UpdatePinch();
            var touch = Touchscreen.current?.primaryTouch;
            if (touch != null && touch.press.isPressed)
            {
                PointerPosition = touch.position.ReadValue();
                if (!_pressed) { _pressed = true; _start = PointerPosition; TouchStarted?.Invoke(PointerPosition); }
                else TouchMoved?.Invoke(PointerPosition);
                return;
            }

            if (_pressed)
            {
                _pressed = false;
                TouchEnded?.Invoke(PointerPosition);
                if ((PointerPosition - _start).sqrMagnitude <= TapTravelPixels * TapTravelPixels) Tap?.Invoke(PointerPosition);
            }

#if UNITY_EDITOR
            if (Mouse.current != null)
            {
                PointerPosition = Mouse.current.position.ReadValue();
                if (Mouse.current.leftButton.wasPressedThisFrame) { _pressed = true; _start = PointerPosition; TouchStarted?.Invoke(PointerPosition); }
                if (_pressed && Mouse.current.leftButton.isPressed) TouchMoved?.Invoke(PointerPosition);
                if (_pressed && Mouse.current.leftButton.wasReleasedThisFrame)
                {
                    _pressed = false; TouchEnded?.Invoke(PointerPosition);
                    if ((PointerPosition - _start).sqrMagnitude <= TapTravelPixels * TapTravelPixels) Tap?.Invoke(PointerPosition);
                }
            }
#endif
        }

        void UpdatePinch()
        {
            var screen = Touchscreen.current;
            if (screen == null) return;
            var touches = screen.touches;
            if (touches.Count < 2 || !touches[0].press.isPressed || !touches[1].press.isPressed)
            {
                _previousPinchDistance = 0f;
                return;
            }
            var distance = Vector2.Distance(touches[0].position.ReadValue(), touches[1].position.ReadValue());
            if (_previousPinchDistance > 0f) PinchDelta?.Invoke(distance - _previousPinchDistance);
            _previousPinchDistance = distance;
        }
    }
}
