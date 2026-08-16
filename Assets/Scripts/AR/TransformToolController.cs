using System.Collections;
using System.Collections.Generic;
using ARLearning.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ARLearning.AR
{
    /// <summary>
    /// A small mobile transform layer: it operates on the whole placed object,
    /// rather than attempting Blender-style mesh-element editing.
    /// </summary>
    [RequireComponent(typeof(ARRaycastManager), typeof(TouchInputRouter))]
    public sealed class TransformToolController : MonoBehaviour
    {
        public enum Tool { Select, Move, Rotate, Scale }

        // A full-screen swipe produces a predictable half turn on every phone,
        // instead of making high-resolution screens rotate much faster.
        const float RotationDegreesPerFullSwipe = 180f;
        const float MaximumRotationDegreesPerFrame = 12f;
        const float MinimumScale = 0.05f;
        const float MaximumScale = 0.5f;
        static readonly List<ARRaycastHit> s_Hits = new();

        [SerializeField] ARPlacementManager m_Placement;
        ARRaycastManager _raycasts;
        TouchInputRouter _input;
        CubeMeshToolController _meshTools;
        Camera _camera;
        Transform _selected;
        Tool _tool;
        Vector2 _lastPointer;
        bool _dragging;
        Coroutine _spinRoutine;

        public Tool ActiveTool => _tool;
        public bool HasSelection => _selected != null;
        public bool IsSpinning => _spinRoutine != null;
        public event System.Action<Tool> ToolChanged;
        public event System.Action<bool> SelectionChanged;

        void Awake()
        {
            _raycasts = GetComponent<ARRaycastManager>();
            _input = GetComponent<TouchInputRouter>();
            _meshTools = GetComponent<CubeMeshToolController>();
            _camera = Camera.main;
        }

        void OnEnable()
        {
            _input.Tap += SelectAt;
            _input.TouchStarted += BeginDrag;
            _input.TouchMoved += Drag;
            _input.TouchEnded += EndDrag;
            _input.PinchDelta += ScaleByPinch;
            m_Placement.ObjectPlaced += SelectPlacedObject;
            m_Placement.ObjectReset += ClearSelection;
        }

        void OnDisable()
        {
            _input.Tap -= SelectAt;
            _input.TouchStarted -= BeginDrag;
            _input.TouchMoved -= Drag;
            _input.TouchEnded -= EndDrag;
            _input.PinchDelta -= ScaleByPinch;
            m_Placement.ObjectPlaced -= SelectPlacedObject;
            m_Placement.ObjectReset -= ClearSelection;
        }

        public void SetTool(int tool) => SetTool((Tool)tool);
        public void ToggleTool(int tool)
        {
            var requested = (Tool)tool;
            SetTool(_tool == requested ? Tool.Select : requested);
        }
        public void SetTool(Tool tool)
        {
            StopSpin();
            _tool = tool;
            ToolChanged?.Invoke(_tool);
        }

        public void SpinSelected()
        {
            if (_selected == null) return;
            if (_spinRoutine != null) StopCoroutine(_spinRoutine);
            _spinRoutine = StartCoroutine(SpinOneTurn(_selected));
        }

        public void ToggleSpin()
        {
            if (_spinRoutine != null)
            {
                StopSpin();
                return;
            }
            SetTool(Tool.Select);
            SpinSelected();
        }

        void StopSpin()
        {
            if (_spinRoutine == null) return;
            StopCoroutine(_spinRoutine);
            _spinRoutine = null;
        }

        public void ResetView()
        {
            if (_selected == null) return;
            StopSpin();
            _selected.rotation = Quaternion.identity;
            SetTool(Tool.Select);
        }

        public void SetOrientation(string view)
        {
            if (_selected == null) return;
            StopSpin();
            _selected.rotation = view switch
            {
                "Top" => Quaternion.Euler(-90f, 0f, 0f),
                "Bottom" => Quaternion.Euler(90f, 0f, 0f),
                "Left" => Quaternion.Euler(0f, 90f, 0f),
                "Right" => Quaternion.Euler(0f, -90f, 0f),
                "Back" => Quaternion.Euler(0f, 180f, 0f),
                _ => Quaternion.identity
            };
        }

        void SelectPlacedObject(GameObject placedObject)
        {
            _selected = placedObject.transform;
            SelectionChanged?.Invoke(true);
        }

        void ClearSelection()
        {
            _selected = null;
            _dragging = false;
            SelectionChanged?.Invoke(false);
        }

        void SelectAt(Vector2 screenPosition)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (_meshTools != null && (_meshTools.IsDraggingBevelHandle || _meshTools.IsDraggingToolHandle || _meshTools.IsPointerOverBevelDragger(screenPosition) || _meshTools.IsPointerOverToolDragger(screenPosition))) return;
            if (_camera == null || m_Placement.PlacedObject == null) return;
            var ray = _camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit)) return;
            if (hit.transform == m_Placement.PlacedObject.transform || hit.transform.IsChildOf(m_Placement.PlacedObject.transform))
                SelectPlacedObject(m_Placement.PlacedObject);
        }

        void BeginDrag(Vector2 screenPosition)
        {
            if (_meshTools != null && (_meshTools.IsDraggingBevelHandle || _meshTools.IsDraggingToolHandle || _meshTools.IsPointerOverBevelDragger(screenPosition) || _meshTools.IsPointerOverToolDragger(screenPosition)))
            {
                _dragging = false;
                return;
            }
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (_selected == null || _tool == Tool.Select) return;
            _lastPointer = screenPosition;
            _dragging = true;
        }

        void Drag(Vector2 screenPosition)
        {
            if (!_dragging || _selected == null || (_meshTools != null && (_meshTools.IsDraggingBevelHandle || _meshTools.IsDraggingToolHandle))) return;
            var delta = screenPosition - _lastPointer;
            _lastPointer = screenPosition;
            switch (_tool)
            {
                case Tool.Move: MoveToPlane(screenPosition); break;
                case Tool.Rotate: RotateFromSwipe(delta); break;
                case Tool.Scale: ScaleByDrag(delta.y); break;
            }
        }

        void EndDrag(Vector2 _) => _dragging = false;

        void MoveToPlane(Vector2 screenPosition)
        {
            var rotation = _selected.rotation;
            if (_raycasts.Raycast(screenPosition, s_Hits, TrackableType.PlaneWithinPolygon))
            {
                _selected.SetPositionAndRotation(s_Hits[0].pose.position, rotation);
                return;
            }

            // In non-AR preview, drag across a camera-facing plane at the
            // object's current viewing distance.
            if (_camera == null) return;
            var distance = Vector3.Dot(_selected.position - _camera.transform.position, _camera.transform.forward);
            _selected.SetPositionAndRotation(_camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, Mathf.Max(0.2f, distance))), rotation);
        }

        void RotateFromSwipe(Vector2 delta)
        {
            if (_meshTools != null && (_meshTools.IsDraggingBevelHandle || _meshTools.IsDraggingToolHandle)) return;
            if (_camera == null) return;
            if (delta.sqrMagnitude < 1f) return;

            // Normalize by the actual display dimensions so the same physical
            // gesture feels consistent in the Editor and across Android phones.
            var yaw = -delta.x / Mathf.Max(1f, Screen.width) * RotationDegreesPerFullSwipe;
            var pitch = delta.y / Mathf.Max(1f, Screen.height) * RotationDegreesPerFullSwipe;
            yaw = Mathf.Clamp(yaw, -MaximumRotationDegreesPerFrame, MaximumRotationDegreesPerFrame);
            pitch = Mathf.Clamp(pitch, -MaximumRotationDegreesPerFrame, MaximumRotationDegreesPerFrame);

            // Combine both camera-relative axes into one update. Diagonal swipes
            // therefore feel like one continuous grab rather than two rotations.
            var rotationDelta = Quaternion.AngleAxis(yaw, _camera.transform.up) *
                                Quaternion.AngleAxis(pitch, _camera.transform.right);
            _selected.rotation = rotationDelta * _selected.rotation;
        }

        void ScaleByPinch(float delta)
        {
            if (_selected == null || _tool != Tool.Scale) return;
            ScaleByDrag(delta);
        }

        void ScaleByDrag(float delta)
        {
            var size = Mathf.Clamp(_selected.localScale.x + delta * 0.001f, MinimumScale, MaximumScale);
            _selected.localScale = Vector3.one * size;
        }

        IEnumerator SpinOneTurn(Transform target)
        {
            const float duration = 0.65f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                var degrees = 360f * Time.deltaTime / duration;
                target.Rotate(Vector3.up, degrees, Space.World);
                elapsed += Time.deltaTime;
                yield return null;
            }
            _spinRoutine = null;
        }
    }
}
