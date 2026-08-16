using System.Collections;
using System.Collections.Generic;
using ARLearning.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ARLearning.AR
{
    /// <summary>
    /// Lightweight, visible mesh-editing lessons for the learning cube.  Each action
    /// updates the selected cube's mesh (and, where useful, adds topology guides).
    /// It intentionally stays at face/object level; vertex editing comes in a later phase.
    /// </summary>
    public sealed class CubeMeshToolController : MonoBehaviour
    {
        public enum Tool { Bevel, Extrude, Inset, Knife, LoopCut }
        public enum BevelSelectionMode { Edge, Face }
        public enum KnifeSnapMode { Off, Edge, Vertex }
        public enum LoopCutPhase { None, Preview, Sliding, Committed }

        [SerializeField] ARPlacementManager m_Placement;
        [SerializeField] TransformToolController m_TransformTools;
        readonly List<GameObject> _guides = new();
        readonly List<GameObject> _bevelEdgeGuides = new();
        GameObject _bevelDragger;
        GameObject _bevelValueLabel;
        MeshFilter _filter;
        MeshCollider _collider;
        MeshRenderer _renderer;
        TouchInputRouter _input;
        float _amount;
        [SerializeField, Min(0.1f)] float m_BevelWidthMillimetres = 10f;
        readonly float[] _bevelWidthByEdgeMm = new float[12];
        readonly Stack<float[]> _bevelUndo = new();
        readonly Stack<float[]> _bevelRedo = new();
        readonly float[] _extrudeDistanceByFaceMm = new float[6];
        readonly Stack<float[]> _extrudeUndo = new();
        readonly Stack<float[]> _extrudeRedo = new();
        readonly List<int> _extrudeTriangleFaceIds = new();
        readonly List<int> _extrudeTriangleGraphFaceIds = new();
        readonly EditableTopologyGraph _editableTopology = new();
        readonly Dictionary<int, float> _insetPercentByFace = new();
        readonly Stack<Dictionary<int, float>> _insetUndo = new();
        readonly Stack<Dictionary<int, float>> _insetRedo = new();
        readonly Dictionary<int, KnifeCutState> _knifeCutByFace = new();
        readonly Stack<Dictionary<int, KnifeCutState>> _knifeUndo = new();
        readonly Stack<Dictionary<int, KnifeCutState>> _knifeRedo = new();
        readonly Stack<LoopCutState> _loopUndo = new();
        readonly Stack<LoopCutState> _loopRedo = new();
        bool _loopCutEnabled;
        LoopCutPhase _loopCutPhase;
        int _loopCutAxis = 1;
        int _loopCutSegments = 1;
        float _loopCutSlidePercent;
        bool _loopCutValid;
        bool _loopRingClosed;
        string _loopRingStopReason = "Tap an edge to discover a loop";
        readonly List<LoopRingSpan> _loopRingSpans = new();
        readonly List<Vector3> _loopRingStops = new();
        int _loopDiscoveryFaceId = -1;
        Vector3 _loopDiscoveryPoint;
        bool _hasKnifePointA;
        int _knifePendingSemanticFaceId = -1;
        Vector3 _knifePointA;
        bool _dragSnapshotSaved;
        float _lastPointerY;
        bool _editing;
        bool _draggingBevelHandle;
        Vector3 _bevelDragDirection;
        Vector3 _bevelDragAxisOrigin;
        float _bevelDragStartAxisDistance;
        float _bevelDragStartWidth;
        Vector2 _bevelDragStartPointer;
        Vector2 _bevelDragScreenDirection;
        float _bevelPixelsPerWorldUnit;
        bool _hasActiveTool;
        int _selectedBevelEdge = -1;
        readonly bool[] _selectedBevelEdges = new bool[12];
        bool _selectedBevelFace;
        Vector3 _selectedBevelFaceNormal;
        float[] _bevelDragStartWidths;
        Coroutine _selectionPulse;
        int _bevelLeverReleaseFrame = -1;
        int _tutorialTargetEdge = -1;
        GameObject _boundObject;
        Vector3 _baseMeshSize;
        Vector3 _lastBevelLossyScale;
        int _selectedToolFaceAxis = -1;
        int _selectedToolFaceSign = 1;
        int _selectedToolTopologyFaceId = -1;
        int _selectedEditableFaceId = -1;
        Vector3 _selectedToolFaceNormal;
        Vector3 _selectedToolFaceCenter;
        GameObject _toolDragger;
        GameObject _toolValueLabel;
        GameObject _toolTutorialTargetGuide;
        bool _draggingToolHandle;
        int _toolLeverReleaseFrame = -1;
        float _toolDragStartAmount;
        Vector2 _toolDragStartPointer;
        Vector2 _toolDragScreenDirection;
        float _toolPixelsPerWorldUnit;
        float _loopDragStartSlide;
        public bool ExtrudePrecisionEnabled { get; private set; }
        public float ExtrudeSnapMillimetres { get; private set; }
        public bool InsetPrecisionEnabled { get; private set; }
        public float InsetSnapPercent { get; private set; }
        public KnifeSnapMode KnifeSnap { get; private set; }
        public bool LoopCutPrecisionEnabled { get; private set; }
        public float LoopCutSnapPercent { get; private set; }

        public Tool ActiveTool { get; private set; }
        public bool HasActiveTool => _hasActiveTool;
        public float BevelWidthMillimetres => m_BevelWidthMillimetres;
        public float EffectiveBevelWidthMillimetres { get; private set; }
        public float MaximumBevelWidthMillimetres => GetMaximumBevelWidthMillimetres();
        public bool IsBevelAtMaximum => HasBevelEdgeSelection &&
            EffectiveBevelWidthMillimetres >= MaximumBevelWidthMillimetres - .01f;
        public bool HasBevelEdgeSelection => _selectedBevelEdge >= 0;
        public bool HasBevelFaceSelection => _selectedBevelFace;
        public bool IsDraggingBevelHandle => _draggingBevelHandle;
        public bool IsDraggingToolHandle => _draggingToolHandle;
        public int SelectedBevelEdgeIndex => _selectedBevelEdge;
        public bool HasSingleBevelEdgeSelection => HasBevelEdgeSelection && !HasBevelFaceSelection;
        public BevelSelectionMode SelectionMode { get; private set; } = BevelSelectionMode.Edge;
        public bool BevelPrecisionEnabled { get; private set; }
        public float BevelSnapMillimetres { get; private set; }
        public string BevelSnapLabel => BevelSnapMillimetres <= 0f ? "Snap Off" : $"Snap {BevelSnapMillimetres:0.#}mm";
        public string BevelSelectionStatus => !HasBevelEdgeSelection ? $"{SelectionMode} mode — tap a {SelectionMode.ToString().ToLowerInvariant()}"
            : HasBevelFaceSelection ? "Face selected — 4 edges" : "Edge selected";
        public bool HasToolFaceSelection => _selectedToolFaceAxis >= 0;
        public int SelectedToolFaceAxis => _selectedToolFaceAxis;
        public int SelectedToolFaceSign => _selectedToolFaceSign;
        public int SelectedToolTopologyFaceId => _selectedToolTopologyFaceId;
        public int SelectedEditableFaceId => _selectedEditableFaceId;
        public int EditableVertexCount => _editableTopology.Vertices.Count;
        public int EditableEdgeCount => _editableTopology.Edges.Count;
        public int EditableFaceCount => _editableTopology.Faces.Count;
        public bool SelectedToolFaceIsGenerated => _selectedToolTopologyFaceId >= 6;
        public bool CanManipulateSelectedExtrudeFace => HasToolFaceSelection && !SelectedToolFaceIsGenerated;
        public string SelectedToolTopologyLabel => GetExtrudeTopologyFaceLabel(_selectedToolTopologyFaceId);
        public float EffectiveInsetPercent => ActiveTool == Tool.Inset && HasToolFaceSelection ? _amount * 45f : 0f;
        public string InsetSnapLabel => InsetSnapPercent <= 0f ? "Snap Off" : $"Snap {InsetSnapPercent:0.#}%";
        public bool IsInsetAtMaximum => ActiveTool == Tool.Inset && HasToolFaceSelection && EffectiveInsetPercent >= 44.99f;
        public string KnifeSnapLabel => $"Snap {KnifeSnap}";
        public bool HasKnifePointA => _hasKnifePointA;
        public bool LoopCutEnabled => _loopCutEnabled;
        public LoopCutPhase CurrentLoopCutPhase => _loopCutPhase;
        public bool CanConfirmLoopCut => _loopCutPhase == LoopCutPhase.Preview && _loopCutValid || _loopCutPhase == LoopCutPhase.Sliding;
        public string LoopCutConfirmLabel => _loopCutPhase == LoopCutPhase.Preview ? "Confirm Cut" : _loopCutPhase == LoopCutPhase.Sliding ? "Finish Slide" : "Committed";
        public string LoopCutPhaseLabel => _loopCutPhase.ToString();
        public int LoopCutAxis => _loopCutAxis;
        public int LoopCutSegments => _loopCutSegments;
        public float LoopCutSlidePercent => _loopCutSlidePercent;
        public bool IsLoopCutValid => _loopCutValid;
        public bool IsLoopRingClosed => _loopRingClosed;
        public string LoopRingStatus => _loopRingClosed ? $"Closed quad ring — {_loopRingSpans.Count} faces" : _loopRingStopReason;
        public string LoopCutAxisLabel => $"Axis {(_loopCutAxis == 0 ? "X" : _loopCutAxis == 1 ? "Y" : "Z")}";
        public string LoopCutSnapLabel => LoopCutSnapPercent <= 0f ? "Snap Off" : $"Snap {LoopCutSnapPercent:0.#}%";
        public int KnifePendingSemanticFaceId => _knifePendingSemanticFaceId;
        public float EffectiveExtrudeDistanceMillimetres => HasToolFaceSelection && _filter != null
            ? GetExtrudeLocalDistance() * GetSelectedAxisWorldScale() * 1000f : 0f;
        public float MinimumExtrudeDistanceMillimetres => -.25f * GetSelectedAxisWorldScale() * 1000f;
        public float MaximumExtrudeDistanceMillimetres => .75f * GetSelectedAxisWorldScale() * 1000f;
        public bool IsExtrudeAtLimit => ActiveTool == Tool.Extrude && HasToolFaceSelection &&
            (EffectiveExtrudeDistanceMillimetres <= MinimumExtrudeDistanceMillimetres + .01f ||
             EffectiveExtrudeDistanceMillimetres >= MaximumExtrudeDistanceMillimetres - .01f);
        public string ExtrudeSnapLabel => ExtrudeSnapMillimetres <= 0f ? "Snap Off" : $"Snap {ExtrudeSnapMillimetres:0.#}mm";
        public string ActiveToolInstruction => ActiveTool switch
        {
            Tool.Extrude => SelectedToolFaceIsGenerated ? $"Extrude — {SelectedToolTopologyLabel} selected; generated-face editing is prepared for the next tool integration"
                : HasToolFaceSelection ? $"Extrude — {EffectiveExtrudeDistanceMillimetres:+0.0;-0.0;0.0} mm — pull the face-normal lever" : "Extrude — tap a face",
            Tool.Inset => HasToolFaceSelection ? $"Inset — {SelectedToolTopologyLabel} — {EffectiveInsetPercent:0.0}% — pull the diagonal lever" : "Inset — tap a face",
            Tool.Knife => _hasKnifePointA ? "Knife — tap a second point on the same highlighted face to commit the cut"
                : HasToolFaceSelection ? "Knife — tap the first cut point on the selected face" : "Knife — tap a face to place the first cut point",
            Tool.LoopCut => !_loopCutEnabled ? "Loop Cut — tap near an edge to create a temporary preview"
                : _loopCutPhase == LoopCutPhase.Preview ? (_loopCutValid ? "Loop Cut Preview — configure, then Confirm Cut" : $"Loop Cut Preview — {LoopRingStatus}")
                : _loopCutPhase == LoopCutPhase.Sliding ? $"Loop Cut Slide — pull the lever, then Finish Slide — {_loopCutSlidePercent:+0;-0;0}%"
                : _loopCutValid ? $"Loop Cut — {LoopCutAxisLabel}, {_loopCutSegments} segment{(_loopCutSegments == 1 ? "" : "s")}, {_loopCutSlidePercent:+0;-0;0}% slide — {LoopRingStatus}"
                : $"Loop Cut — {LoopRingStatus}",
            _ => string.Empty
        };
        public event System.Action<Tool> ToolApplied;

        void Awake() => _input = GetComponent<TouchInputRouter>();
        void OnEnable()
        {
            _input.TouchStarted += BeginEdit;
            _input.TouchMoved += UpdateEdit;
            _input.TouchEnded += EndEdit;
            _input.Tap += SelectBevelEdgeAt;
        }
        void OnDisable()
        {
            _input.TouchStarted -= BeginEdit;
            _input.TouchMoved -= UpdateEdit;
            _input.TouchEnded -= EndEdit;
            _input.Tap -= SelectBevelEdgeAt;
        }

        void LateUpdate()
        {
            if (_filter != null && ActiveTool == Tool.Bevel && HasBevelEdgeSelection)
            {
                var scale = Absolute(_filter.transform.lossyScale);
                if ((scale - _lastBevelLossyScale).sqrMagnitude > .00000001f)
                {
                    _lastBevelLossyScale = scale;
                    ClampAllBevelWidths();
                    m_BevelWidthMillimetres = _bevelWidthByEdgeMm[_selectedBevelEdge];
                    _filter.sharedMesh = BuildBevelMesh();
                    RefreshCollider();
                    UpdateBevelDragger();
                }
            }
            else if (_filter != null && ActiveTool == Tool.Extrude)
            {
                var scale = Absolute(_filter.transform.lossyScale);
                if ((scale - _lastBevelLossyScale).sqrMagnitude > .00000001f)
                {
                    _lastBevelLossyScale = scale;
                    ClampAllExtrudeDistances();
                    if (HasToolFaceSelection) LoadSelectedExtrudeDistance();
                    _filter.sharedMesh = BuildExtrudeMesh();
                    RefreshCollider();
                    UpdateToolDragger();
                }
            }
            UpdateBevelValueLabel();
            UpdateToolValueLabel();
        }

        public void ApplyTool(int tool) => SelectTool((Tool)tool);

        public void SelectTool(Tool tool)
        {
            ActiveTool = tool;
            if (!BindToPlacedCube()) return;
            _hasActiveTool = true;
            if (m_TransformTools != null) m_TransformTools.SetTool(TransformToolController.Tool.Select);
            _amount = DefaultAmount(tool);
            ClearToolDragger();
            _hasKnifePointA = false;
            _knifePendingSemanticFaceId = -1;
            if (tool == Tool.Bevel)
            {
                ClearBevelSelection();
                _filter.sharedMesh = BuildBevelMesh();
                RefreshCollider();
                ClearBevelEdgeGuides();
            }
            else
            {
                ClearBevelEdgeGuides();
                _selectedToolFaceAxis = -1;
                _selectedToolTopologyFaceId = -1;
                _selectedEditableFaceId = -1;
                _selectedToolFaceNormal = Vector3.zero;
                _filter.sharedMesh = tool == Tool.LoopCut ? BuildStoredLoopCutTopology() : tool == Tool.Knife ? BuildStoredKnifeOnTopology() : tool == Tool.Inset ? BuildStoredInsetsOnExtrudeMesh() : tool == Tool.Extrude ? BuildExtrudeMesh() : CreateBox(_baseMeshSize.x, _baseMeshSize.y, _baseMeshSize.z);
                RefreshCollider();
                ClearGuides();
            }
            ToolApplied?.Invoke(tool);
        }

        void BeginEdit(Vector2 screenPosition)
        {
            if (!_hasActiveTool || !BindToPlacedCube()) return;
            // Give the 3D lever priority over overlay UI raycasts. This prevents
            // invisible/full-screen UI graphics from swallowing the handle press.
            if (ActiveTool == Tool.Bevel && TryBeginBevelHandleDrag(screenPosition)) return;
            if (ActiveTool is Tool.Extrude or Tool.Inset or Tool.LoopCut && TryBeginToolHandleDrag(screenPosition)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (ActiveTool == Tool.Bevel)
            {
                // Selection happens only after a completed tap. Keeping presses
                // selection-free prevents duplicate selection and lets a press on
                // the circular endpoint become a lever drag immediately.
                return;
            }
            if (ActiveTool is Tool.Knife or Tool.LoopCut) return;
            if (!HasToolFaceSelection || ActiveTool is Tool.Extrude or Tool.Inset) return;
            _lastPointerY = screenPosition.y;
            if (ActiveTool == Tool.Bevel && !_dragSnapshotSaved) { SaveBevelUndo(); _dragSnapshotSaved = true; }
            _editing = true;
        }

        void UpdateEdit(Vector2 screenPosition)
        {
            if (!_editing) return;
            var deltaY = screenPosition.y - _lastPointerY;
            if (ActiveTool == Tool.Bevel && _draggingBevelHandle)
            {
                var pointerDelta = screenPosition - _bevelDragStartPointer;
                var delta = Vector2.Dot(pointerDelta, _bevelDragScreenDirection) / Mathf.Max(1f, _bevelPixelsPerWorldUnit);
                var deltaMillimetres = delta * 1000f * (BevelPrecisionEnabled ? .25f : 1f);
                for (var i = 0; i < _selectedBevelEdges.Length; i++)
                    if (_selectedBevelEdges[i])
                        _bevelWidthByEdgeMm[i] = ClampBevelWidth(SnapBevelWidth(_bevelDragStartWidths[i] + deltaMillimetres));
                m_BevelWidthMillimetres = _bevelWidthByEdgeMm[_selectedBevelEdge];
            }
            else if (_draggingToolHandle)
            {
                var pointerDelta = screenPosition - _toolDragStartPointer;
                var worldDelta = Vector2.Dot(pointerDelta, _toolDragScreenDirection) / Mathf.Max(1f, _toolPixelsPerWorldUnit);
                if (ActiveTool == Tool.Extrude)
                {
                    var sensitivity = ExtrudePrecisionEnabled ? .25f : 1f;
                    var startMm = AmountToExtrudeMillimetres(_toolDragStartAmount);
                    SetExtrudeDistanceInternal(startMm + worldDelta * 1000f * sensitivity, true);
                }
                else if (ActiveTool == Tool.Inset)
                {
                    var sensitivity = InsetPrecisionEnabled ? .25f : 1f;
                    var percent = _toolDragStartAmount * 45f + worldDelta / Mathf.Max(.001f, _baseMeshSize.magnitude * .45f) * 45f * sensitivity;
                    SetInsetPercentInternal(percent, true);
                }
                else if (ActiveTool == Tool.LoopCut)
                {
                    var sensitivity = LoopCutPrecisionEnabled ? .25f : 1f;
                    SetLoopCutSlideInternal(_loopDragStartSlide + worldDelta / Mathf.Max(.001f, _baseMeshSize.magnitude) * 100f * sensitivity, true);
                }
                else
                    _amount = Mathf.Clamp01(_toolDragStartAmount + worldDelta / Mathf.Max(.001f, _baseMeshSize.magnitude * .45f));
            }
            else
                _amount = Mathf.Clamp01(_amount + deltaY / Mathf.Max(1f, Screen.height));
            _lastPointerY = screenPosition.y;
            RenderTool();
        }

        void EndEdit(Vector2 _)
        {
            _editing = false;
            if (_draggingBevelHandle)
            {
                SetDraggerColor(new Color(1f, .72f, .05f));
                _bevelLeverReleaseFrame = Time.frameCount;
            }
            if (_draggingToolHandle)
            {
                SetToolDraggerColor(new Color(1f, .72f, .05f));
                _toolLeverReleaseFrame = Time.frameCount;
            }
            _draggingBevelHandle = false;
            _draggingToolHandle = false;
            _dragSnapshotSaved = false;
        }

        void SelectBevelEdgeAt(Vector2 screenPosition)
        {
            if (!_hasActiveTool || _filter == null) return;
            if (_bevelLeverReleaseFrame == Time.frameCount) return;
            if (_toolLeverReleaseFrame == Time.frameCount) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (ActiveTool == Tool.Bevel)
            {
                if (!TrySelectBevelTargetAt(screenPosition)) DeselectBevel();
                return;
            }
            if (ActiveTool == Tool.Knife)
            {
                HandleKnifeTap(screenPosition);
                return;
            }
            if (!TrySelectToolFaceAt(screenPosition))
            {
                _selectedToolFaceAxis = -1;
                _selectedToolTopologyFaceId = -1;
                _selectedEditableFaceId = -1;
                _selectedToolFaceNormal = Vector3.zero;
                ClearGuides();
                ClearToolDragger();
            }
        }

        bool TrySelectToolFaceAt(Vector2 screenPosition)
        {
            var camera = Camera.main;
            if (camera == null) return false;
            RaycastHit surface = default;
            var found = false;
            foreach (var hit in Physics.RaycastAll(camera.ScreenPointToRay(screenPosition)))
            {
                if (hit.transform != _filter.transform) continue;
                surface = hit;
                found = true;
                break;
            }
            if (!found) return false;
            var normal = _filter.transform.InverseTransformDirection(surface.normal).normalized;
            _selectedToolFaceAxis = 0;
            if (Mathf.Abs(normal.y) > Mathf.Abs(normal.x)) _selectedToolFaceAxis = 1;
            if (Mathf.Abs(normal.z) > Mathf.Abs(normal[_selectedToolFaceAxis])) _selectedToolFaceAxis = 2;
            _selectedToolFaceSign = normal[_selectedToolFaceAxis] >= 0f ? 1 : -1;
            _selectedToolTopologyFaceId = ActiveTool is Tool.Extrude or Tool.Inset or Tool.Knife or Tool.LoopCut && surface.triangleIndex >= 0 && surface.triangleIndex < _extrudeTriangleFaceIds.Count
                ? _extrudeTriangleFaceIds[surface.triangleIndex]
                : GetFaceId(_selectedToolFaceAxis, _selectedToolFaceSign);
            _selectedEditableFaceId = ActiveTool is Tool.Extrude or Tool.Inset or Tool.Knife or Tool.LoopCut && surface.triangleIndex >= 0 && surface.triangleIndex < _extrudeTriangleGraphFaceIds.Count
                ? _extrudeTriangleGraphFaceIds[surface.triangleIndex]
                : -1;
            _selectedToolFaceCenter = _filter.transform.InverseTransformPoint(surface.point);
            _selectedToolFaceNormal = Vector3.zero;
            _selectedToolFaceNormal[_selectedToolFaceAxis] = _selectedToolFaceSign;
            if (ActiveTool == Tool.Extrude && CanManipulateSelectedExtrudeFace) LoadSelectedExtrudeDistance();
            if (ActiveTool == Tool.Inset) LoadSelectedInsetPercent();
            if (ActiveTool == Tool.LoopCut && !_loopCutEnabled)
            {
                _loopCutEnabled = true;
                _loopCutPhase = LoopCutPhase.Preview;
            }
            if (ActiveTool == Tool.LoopCut)
                DiscoverLoopRing(_selectedEditableFaceId, _selectedToolFaceCenter);
            RenderTool();
            CreateToolDragger();
            return true;
        }

        void HandleKnifeTap(Vector2 screenPosition)
        {
            var camera = Camera.main;
            if (camera == null) return;
            RaycastHit hit = default;
            var found = false;
            foreach (var candidate in Physics.RaycastAll(camera.ScreenPointToRay(screenPosition)))
            {
                if (candidate.transform != _filter.transform) continue;
                hit = candidate;
                found = true;
                break;
            }
            if (!found || hit.triangleIndex < 0 || hit.triangleIndex >= _extrudeTriangleFaceIds.Count)
            {
                _hasKnifePointA = false;
                _knifePendingSemanticFaceId = -1;
                DeselectToolFace();
                return;
            }

            var semanticFaceId = _extrudeTriangleFaceIds[hit.triangleIndex];
            var graphFaceId = hit.triangleIndex < _extrudeTriangleGraphFaceIds.Count ? _extrudeTriangleGraphFaceIds[hit.triangleIndex] : -1;
            var localPoint = SnapKnifePoint(_filter.transform.InverseTransformPoint(hit.point), graphFaceId);
            if (!_hasKnifePointA || semanticFaceId != _knifePendingSemanticFaceId)
            {
                TrySelectToolFaceAt(screenPosition);
                _knifePendingSemanticFaceId = semanticFaceId;
                _knifePointA = localPoint;
                _hasKnifePointA = true;
                RenderTool();
                return;
            }

            if ((localPoint - _knifePointA).sqrMagnitude < .000025f) return;
            SaveKnifeUndo();
            _knifeCutByFace[semanticFaceId] = new KnifeCutState(_knifePointA, localPoint);
            _hasKnifePointA = false;
            _knifePendingSemanticFaceId = -1;
            RenderTool();
        }

        public void ResetSelectedKnifeCut()
        {
            if (ActiveTool != Tool.Knife || !HasToolFaceSelection) return;
            SaveKnifeUndo();
            _knifeCutByFace.Remove(_selectedToolTopologyFaceId);
            _hasKnifePointA = false;
            _knifePendingSemanticFaceId = -1;
            RenderTool();
        }

        public void CancelPendingKnifePoint()
        {
            _hasKnifePointA = false;
            _knifePendingSemanticFaceId = -1;
            if (ActiveTool == Tool.Knife) RenderTool();
        }

        public void ShowKnifeTutorialTargets(int axis, int sign)
        {
            ClearToolTutorialTargetFace();
            if (!BindToPlacedCube()) return;
            GetKnifeTutorialTargetPoints(axis, sign, out var first, out var second);
            _toolTutorialTargetGuide = new GameObject("Knife Tutorial Targets");
            _toolTutorialTargetGuide.transform.SetParent(_filter.transform, false);
            foreach (var target in new[] { first, second })
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = target == first ? "Knife Target A" : "Knife Target B";
                marker.transform.SetParent(_toolTutorialTargetGuide.transform, false);
                marker.transform.localPosition = target;
                marker.transform.localScale = Vector3.one * .055f;
                Destroy(marker.GetComponent<Collider>());
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", new Color(.25f, 1f, .48f));
                marker.GetComponent<MeshRenderer>().material = material;
            }
        }

        public bool KnifePendingPointMatchesTutorialTarget(int axis, int sign)
        {
            if (!_hasKnifePointA || _knifePendingSemanticFaceId != GetFaceId(axis, sign)) return false;
            GetKnifeTutorialTargetPoints(axis, sign, out var target, out _);
            return (_knifePointA - target).magnitude <= _baseMeshSize.magnitude * .12f;
        }

        public bool KnifeCutMatchesTutorialTargets(int axis, int sign)
        {
            if (!_knifeCutByFace.TryGetValue(GetFaceId(axis, sign), out var cut)) return false;
            GetKnifeTutorialTargetPoints(axis, sign, out var first, out var second);
            var tolerance = _baseMeshSize.magnitude * .12f;
            return ((cut.PointA - first).magnitude <= tolerance && (cut.PointB - second).magnitude <= tolerance) ||
                   ((cut.PointA - second).magnitude <= tolerance && (cut.PointB - first).magnitude <= tolerance);
        }

        public bool TryGetKnifeTutorialScreenTargets(int axis, int sign, out Vector2 first, out Vector2 second)
        {
            first = second = default;
            var camera = Camera.main;
            if (_filter == null || camera == null) return false;
            GetKnifeTutorialTargetPoints(axis, sign, out var localFirst, out var localSecond);
            var screenFirst = camera.WorldToScreenPoint(_filter.transform.TransformPoint(localFirst));
            var screenSecond = camera.WorldToScreenPoint(_filter.transform.TransformPoint(localSecond));
            if (screenFirst.z <= 0f || screenSecond.z <= 0f) return false;
            first = new Vector2(screenFirst.x, screenFirst.y);
            second = new Vector2(screenSecond.x, screenSecond.y);
            return true;
        }

        void GetKnifeTutorialTargetPoints(int axis, int sign, out Vector3 first, out Vector3 second)
        {
            var normal = Vector3.zero;
            normal[axis] = sign >= 0 ? 1f : -1f;
            var half = _baseMeshSize * .5f;
            var center = Vector3.Scale(normal, half) + normal * GetStoredExtrudeLocalDistance(axis, sign >= 0 ? 1 : -1);
            var tangentAxis = axis == 0 ? 1 : 0;
            first = second = center;
            first[tangentAxis] = -half[tangentAxis];
            second[tangentAxis] = half[tangentAxis];
        }

        public void CycleKnifeSnap() => KnifeSnap = KnifeSnap == KnifeSnapMode.Off ? KnifeSnapMode.Edge : KnifeSnap == KnifeSnapMode.Edge ? KnifeSnapMode.Vertex : KnifeSnapMode.Off;
        public void UndoKnife() => RestoreKnifeHistory(_knifeUndo, _knifeRedo);
        public void RedoKnife() => RestoreKnifeHistory(_knifeRedo, _knifeUndo);

        void SaveKnifeUndo()
        {
            _knifeUndo.Push(new Dictionary<int, KnifeCutState>(_knifeCutByFace));
            _knifeRedo.Clear();
        }

        void RestoreKnifeHistory(Stack<Dictionary<int, KnifeCutState>> source, Stack<Dictionary<int, KnifeCutState>> destination)
        {
            if (source.Count == 0 || _filter == null) return;
            destination.Push(new Dictionary<int, KnifeCutState>(_knifeCutByFace));
            _knifeCutByFace.Clear();
            foreach (var pair in source.Pop()) _knifeCutByFace[pair.Key] = pair.Value;
            _hasKnifePointA = false;
            _knifePendingSemanticFaceId = -1;
            RenderTool();
        }

        Vector3 SnapKnifePoint(Vector3 point, int graphFaceId)
        {
            if (KnifeSnap == KnifeSnapMode.Off || graphFaceId < 0) return point;
            var face = _editableTopology.Faces.Find(candidate => candidate.Id == graphFaceId);
            if (face == null || face.VertexIds.Length == 0) return point;
            var best = point;
            var bestDistance = float.PositiveInfinity;
            if (KnifeSnap == KnifeSnapMode.Vertex)
            {
                foreach (var vertexId in face.VertexIds)
                {
                    var candidate = _editableTopology.Vertices[vertexId].Position;
                    var distance = (candidate - point).sqrMagnitude;
                    if (distance < bestDistance) { bestDistance = distance; best = candidate; }
                }
                return best;
            }
            for (var i = 0; i < face.VertexIds.Length; i++)
            {
                var a = _editableTopology.Vertices[face.VertexIds[i]].Position;
                var b = _editableTopology.Vertices[face.VertexIds[(i + 1) % face.VertexIds.Length]].Position;
                var candidate = ClosestPointOnSegment(point, a, b);
                var distance = (candidate - point).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = candidate; }
            }
            return best;
        }

        static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            var edge = b - a;
            var lengthSquared = edge.sqrMagnitude;
            if (lengthSquared < .0000001f) return a;
            return a + edge * Mathf.Clamp01(Vector3.Dot(point - a, edge) / lengthSquared);
        }

        bool TryBeginToolHandleDrag(Vector2 screenPosition)
        {
            var camera = Camera.main;
            if (_toolDragger == null || camera == null || (ActiveTool == Tool.Extrude && !CanManipulateSelectedExtrudeFace)) return false;
            if (!IsPointerOverToolDragger(screenPosition)) return false;
            var endpoint = camera.WorldToScreenPoint(_toolDragger.transform.position);
            if (endpoint.z <= 0f) return false;
            var start = camera.WorldToScreenPoint(_toolDragger.transform.position - _toolDragger.transform.up * .1f);
            var projected = new Vector2(endpoint.x - start.x, endpoint.y - start.y);
            _toolDragScreenDirection = projected.sqrMagnitude > 1f ? projected.normalized : Vector2.up;
            _toolPixelsPerWorldUnit = projected.magnitude / .1f;
            _toolDragStartPointer = screenPosition;
            _toolDragStartAmount = _amount;
            _loopDragStartSlide = _loopCutSlidePercent;
            if (ActiveTool == Tool.Extrude && !_dragSnapshotSaved)
            {
                SaveExtrudeUndo();
                _dragSnapshotSaved = true;
            }
            else if (ActiveTool == Tool.Inset && !_dragSnapshotSaved)
            {
                SaveInsetUndo();
                _dragSnapshotSaved = true;
            }
            else if (ActiveTool == Tool.LoopCut && !_dragSnapshotSaved)
            {
                SaveLoopUndo();
                _dragSnapshotSaved = true;
            }
            _draggingToolHandle = true;
            _editing = true;
            SetToolDraggerColor(new Color(.2f, .65f, 1f));
            return true;
        }

        public bool IsPointerOverToolDragger(Vector2 screenPosition)
        {
            var camera = Camera.main;
            if (_toolDragger == null || camera == null) return false;
            var endpoint = camera.WorldToScreenPoint(_toolDragger.transform.position);
            if (endpoint.z <= 0f) return false;
            var radius = Mathf.Clamp(Screen.dpi > 0f ? Screen.dpi * .28f : 72f, 64f, 128f);
            return Vector2.Distance(screenPosition, new Vector2(endpoint.x, endpoint.y)) <= radius;
        }

        void SetToolDraggerColor(Color color)
        {
            if (_toolDragger == null) return;
            foreach (var renderer in _toolDragger.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.material.color = color;
                renderer.material.SetColor("_BaseColor", color);
            }
        }

        public void SetBevelSelectionMode(int mode) => SetBevelSelectionMode((BevelSelectionMode)mode);

        public void SetBevelSelectionMode(BevelSelectionMode mode)
        {
            if (SelectionMode == mode && !HasBevelEdgeSelection) return;
            SelectionMode = mode;
            DeselectBevel();
        }

        public void ToggleBevelPrecision() => BevelPrecisionEnabled = !BevelPrecisionEnabled;

        public void CycleBevelSnap()
        {
            BevelSnapMillimetres = BevelSnapMillimetres <= 0f ? .5f : BevelSnapMillimetres < 1f ? 1f : 0f;
        }

        public void ToggleExtrudePrecision() => ExtrudePrecisionEnabled = !ExtrudePrecisionEnabled;

        public void CycleExtrudeSnap()
        {
            ExtrudeSnapMillimetres = ExtrudeSnapMillimetres <= 0f ? .5f : ExtrudeSnapMillimetres < 1f ? 1f : 0f;
        }

        public void ToggleInsetPrecision() => InsetPrecisionEnabled = !InsetPrecisionEnabled;

        public void CycleInsetSnap()
        {
            InsetSnapPercent = InsetSnapPercent <= 0f ? .5f : InsetSnapPercent < 1f ? 1f : 0f;
        }

        public void SetSelectedInsetPercent(float percent)
        {
            if (ActiveTool != Tool.Inset || !HasToolFaceSelection || float.IsNaN(percent) || float.IsInfinity(percent)) return;
            SaveInsetUndo();
            SetInsetPercentInternal(percent, false);
            RenderTool();
        }

        public void AdjustSelectedInsetPercent(float deltaPercent)
        {
            if (ActiveTool != Tool.Inset || !HasToolFaceSelection) return;
            SaveInsetUndo();
            SetInsetPercentInternal(EffectiveInsetPercent + deltaPercent, true);
            RenderTool();
        }

        public void ResetSelectedInset()
        {
            if (ActiveTool != Tool.Inset || !HasToolFaceSelection) return;
            SaveInsetUndo();
            _insetPercentByFace.Remove(_selectedToolTopologyFaceId);
            _amount = 0f;
            RenderTool();
        }

        public void UndoInset() => RestoreInsetHistory(_insetUndo, _insetRedo);
        public void RedoInset() => RestoreInsetHistory(_insetRedo, _insetUndo);

        void SetInsetPercentInternal(float percent, bool applySnap)
        {
            if (applySnap && InsetSnapPercent > 0f) percent = Mathf.Round(percent / InsetSnapPercent) * InsetSnapPercent;
            percent = Mathf.Clamp(percent, 0f, 45f);
            _amount = percent / 45f;
            if (HasToolFaceSelection)
            {
                if (percent <= .0001f) _insetPercentByFace.Remove(_selectedToolTopologyFaceId);
                else _insetPercentByFace[_selectedToolTopologyFaceId] = percent;
            }
        }

        void LoadSelectedInsetPercent()
        {
            _insetPercentByFace.TryGetValue(_selectedToolTopologyFaceId, out var percent);
            _amount = Mathf.Clamp01(percent / 45f);
        }

        void SaveInsetUndo()
        {
            _insetUndo.Push(new Dictionary<int, float>(_insetPercentByFace));
            _insetRedo.Clear();
        }

        void RestoreInsetHistory(Stack<Dictionary<int, float>> source, Stack<Dictionary<int, float>> destination)
        {
            if (source.Count == 0 || _filter == null) return;
            destination.Push(new Dictionary<int, float>(_insetPercentByFace));
            _insetPercentByFace.Clear();
            foreach (var pair in source.Pop()) _insetPercentByFace[pair.Key] = pair.Value;
            if (HasToolFaceSelection) LoadSelectedInsetPercent();
            _filter.sharedMesh = BuildStoredInsetsOnExtrudeMesh();
            RefreshCollider();
            if (HasToolFaceSelection) { RenderTool(); CreateToolDragger(); }
        }

        public void SetExtrudeDistanceMillimetres(float millimetres)
        {
            if (ActiveTool != Tool.Extrude || !CanManipulateSelectedExtrudeFace || float.IsNaN(millimetres) || float.IsInfinity(millimetres)) return;
            SaveExtrudeUndo();
            SetExtrudeDistanceInternal(millimetres, false);
            RenderTool();
        }

        public void AdjustExtrudeDistanceMillimetres(float deltaMillimetres)
        {
            if (ActiveTool != Tool.Extrude || !CanManipulateSelectedExtrudeFace) return;
            SaveExtrudeUndo();
            SetExtrudeDistanceInternal(EffectiveExtrudeDistanceMillimetres + deltaMillimetres, true);
            RenderTool();
        }

        public void ResetSelectedExtrude()
        {
            if (ActiveTool != Tool.Extrude || !CanManipulateSelectedExtrudeFace) return;
            SaveExtrudeUndo();
            SetExtrudeDistanceInternal(0f, false);
            RenderTool();
        }

        public void DeselectToolFace()
        {
            _selectedToolFaceAxis = -1;
            _selectedToolTopologyFaceId = -1;
            _selectedEditableFaceId = -1;
            _selectedToolFaceNormal = Vector3.zero;
            ClearGuides();
            ClearToolDragger();
        }

        public void ShowExtrudeTutorialTargetFace(int axis, int sign)
        {
            ClearToolTutorialTargetFace();
            if (!BindToPlacedCube() || axis < 0 || axis > 2) return;
            var normal = Vector3.zero;
            normal[axis] = sign >= 0 ? 1f : -1f;
            var half = _baseMeshSize * .5f;
            var scale = _baseMeshSize * .88f;
            scale[axis] = .014f;
            _toolTutorialTargetGuide = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _toolTutorialTargetGuide.name = "Extrude Tutorial Target Face";
            _toolTutorialTargetGuide.transform.SetParent(_filter.transform, false);
            var capOffset = GetStoredExtrudeLocalDistance(axis, sign >= 0 ? 1 : -1);
            _toolTutorialTargetGuide.transform.localPosition = Vector3.Scale(normal, half) + normal * (capOffset + .012f);
            _toolTutorialTargetGuide.transform.localScale = scale;
            Destroy(_toolTutorialTargetGuide.GetComponent<Collider>());
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(.25f, 1f, .48f));
            _toolTutorialTargetGuide.GetComponent<MeshRenderer>().material = material;
        }

        public void EnsureExtrudedFaceForInsetTutorial(int axis, int sign, float outwardMillimetres)
        {
            if (!BindToPlacedCube() || axis < 0 || axis > 2) return;
            var faceId = GetFaceId(axis, sign);
            if (_extrudeDistanceByFaceMm[faceId] <= .01f)
            {
                SaveExtrudeUndo();
                var scale = Mathf.Max(.00001f, Mathf.Abs(_filter.transform.lossyScale[axis]));
                _extrudeDistanceByFaceMm[faceId] = Mathf.Clamp(outwardMillimetres, 0f, .75f * scale * 1000f);
            }
            _filter.sharedMesh = ActiveTool == Tool.Inset ? BuildStoredInsetsOnExtrudeMesh() : BuildExtrudeMesh();
            RefreshCollider();
        }

        public void ClearToolTutorialTargetFace()
        {
            if (_toolTutorialTargetGuide != null) Destroy(_toolTutorialTargetGuide);
            _toolTutorialTargetGuide = null;
        }

        public bool TryGetToolLeverScreenPose(out Vector2 endpoint, out Vector2 direction)
        {
            endpoint = default;
            direction = Vector2.up;
            var camera = Camera.main;
            if (_toolDragger == null || camera == null) return false;
            var end = camera.WorldToScreenPoint(_toolDragger.transform.position);
            var start = camera.WorldToScreenPoint(_toolDragger.transform.position - _toolDragger.transform.up * .1f);
            if (end.z <= 0f) return false;
            endpoint = new Vector2(end.x, end.y);
            var projected = endpoint - new Vector2(start.x, start.y);
            if (projected.sqrMagnitude > 1f) direction = projected.normalized;
            return true;
        }

        void SetExtrudeDistanceInternal(float millimetres, bool applySnap)
        {
            if (applySnap && ExtrudeSnapMillimetres > 0f)
                millimetres = Mathf.Round(millimetres / ExtrudeSnapMillimetres) * ExtrudeSnapMillimetres;
            millimetres = Mathf.Clamp(millimetres, MinimumExtrudeDistanceMillimetres, MaximumExtrudeDistanceMillimetres);
            var scale = GetSelectedAxisWorldScale();
            var localDistance = millimetres / (scale * 1000f);
            _amount = Mathf.InverseLerp(-.25f, .75f, localDistance);
            if (HasToolFaceSelection) _extrudeDistanceByFaceMm[GetSelectedFaceId()] = AmountToExtrudeMillimetres(_amount);
        }

        void LoadSelectedExtrudeDistance()
        {
            if (!HasToolFaceSelection) return;
            SetExtrudeDistanceInternal(_extrudeDistanceByFaceMm[GetSelectedFaceId()], false);
        }

        int GetSelectedFaceId() => GetFaceId(_selectedToolFaceAxis, _selectedToolFaceSign);
        static int GetFaceId(int axis, int sign) => axis * 2 + (sign > 0 ? 1 : 0);

        static string GetExtrudeTopologyFaceLabel(int topologyFaceId)
        {
            if (topologyFaceId < 0) return "No face";
            if (topologyFaceId < 6) return $"{GetAxisFaceLabel(topologyFaceId)} cap/source face";
            var generated = topologyFaceId - 6;
            return $"{GetAxisFaceLabel(generated / 4)} generated side {generated % 4 + 1}";
        }

        static string GetAxisFaceLabel(int faceId)
        {
            var axis = faceId / 2;
            var sign = faceId % 2 == 1 ? "+" : "-";
            return $"{sign}{(axis == 0 ? "X" : axis == 1 ? "Y" : "Z")}";
        }

        public void UndoExtrude() => RestoreExtrudeHistory(_extrudeUndo, _extrudeRedo);
        public void RedoExtrude() => RestoreExtrudeHistory(_extrudeRedo, _extrudeUndo);

        void SaveExtrudeUndo()
        {
            _extrudeUndo.Push((float[])_extrudeDistanceByFaceMm.Clone());
            _extrudeRedo.Clear();
        }

        void RestoreExtrudeHistory(Stack<float[]> source, Stack<float[]> destination)
        {
            if (source.Count == 0 || _filter == null) return;
            destination.Push((float[])_extrudeDistanceByFaceMm.Clone());
            System.Array.Copy(source.Pop(), _extrudeDistanceByFaceMm, _extrudeDistanceByFaceMm.Length);
            ClampAllExtrudeDistances();
            if (HasToolFaceSelection) LoadSelectedExtrudeDistance();
            _filter.sharedMesh = BuildExtrudeMesh();
            RefreshCollider();
            if (HasToolFaceSelection) { RenderTool(); CreateToolDragger(); }
        }

        void ClampAllExtrudeDistances()
        {
            for (var axis = 0; axis < 3; axis++)
            {
                var scale = Mathf.Max(.00001f, Mathf.Abs(_filter.transform.lossyScale[axis]));
                var minimum = -.25f * scale * 1000f;
                var maximum = .75f * scale * 1000f;
                for (var signIndex = 0; signIndex < 2; signIndex++)
                {
                    var id = axis * 2 + signIndex;
                    _extrudeDistanceByFaceMm[id] = Mathf.Clamp(_extrudeDistanceByFaceMm[id], minimum, maximum);
                }
            }
        }

        float AmountToExtrudeMillimetres(float amount) => Mathf.Lerp(-.25f, .75f, amount) * GetSelectedAxisWorldScale() * 1000f;
        float GetExtrudeLocalDistance() => Mathf.Lerp(-.25f, .75f, _amount);
        float GetSelectedAxisWorldScale()
        {
            if (_filter == null || _selectedToolFaceAxis < 0) return 1f;
            return Mathf.Max(.00001f, Mathf.Abs(_filter.transform.lossyScale[_selectedToolFaceAxis]));
        }

        public void SetBevelTutorialTargetEdge(int edgeIndex)
        {
            if (edgeIndex < 0 || edgeIndex >= s_CubeEdges.Length || !BindToPlacedCube()) return;
            _tutorialTargetEdge = edgeIndex;
            if (_bevelEdgeGuides.Count == 0) CreateBevelEdgeGuides();
            UpdateBevelEdgeGuideColours();
        }

        public void ClearBevelTutorialTarget()
        {
            _tutorialTargetEdge = -1;
            UpdateBevelEdgeGuideColours();
        }

        public void ResetBevelEdgeForTutorial(int edgeIndex)
        {
            if (edgeIndex < 0 || edgeIndex >= _bevelWidthByEdgeMm.Length || _filter == null) return;
            SaveBevelUndo();
            _bevelWidthByEdgeMm[edgeIndex] = 0f;
            if (_selectedBevelEdge == edgeIndex) m_BevelWidthMillimetres = 0f;
            _filter.sharedMesh = BuildBevelMesh();
            RefreshCollider();
            UpdateBevelDragger();
        }

        public bool TryGetBevelLeverScreenPose(out Vector2 endpoint, out Vector2 direction)
        {
            endpoint = default;
            direction = Vector2.up;
            var camera = Camera.main;
            if (_bevelDragger == null || camera == null) return false;
            var end = camera.WorldToScreenPoint(_bevelDragger.transform.position);
            var axisOrigin = _bevelDragger.transform.childCount > 0
                ? _bevelDragger.transform.GetChild(0).TransformPoint(Vector3.down * .5f)
                : _bevelDragger.transform.position - _bevelDragger.transform.up * .1f;
            var start = camera.WorldToScreenPoint(axisOrigin);
            if (end.z <= 0f) return false;
            endpoint = new Vector2(end.x, end.y);
            var projected = endpoint - new Vector2(start.x, start.y);
            if (projected.sqrMagnitude > 1f) direction = projected.normalized;
            return true;
        }

        float SnapBevelWidth(float width)
        {
            if (BevelSnapMillimetres <= 0f) return width;
            return Mathf.Round(width / BevelSnapMillimetres) * BevelSnapMillimetres;
        }

        float ClampBevelWidth(float width) => Mathf.Clamp(width, 0f, GetMaximumBevelWidthMillimetres());

        void ClampAllBevelWidths()
        {
            for (var i = 0; i < _bevelWidthByEdgeMm.Length; i++)
                _bevelWidthByEdgeMm[i] = ClampBevelWidth(_bevelWidthByEdgeMm[i]);
        }

        float GetMaximumBevelWidthMillimetres()
        {
            if (_filter == null) return 0f;
            var scale = Absolute(_filter.transform.lossyScale);
            var worldSize = Vector3.Scale(_baseMeshSize, scale);
            return Mathf.Max(0f, Mathf.Min(worldSize.x, Mathf.Min(worldSize.y, worldSize.z)) * 500f - .001f);
        }

        bool TryBeginBevelHandleDrag(Vector2 screenPosition)
        {
            if (!IsPointerOverBevelDragger(screenPosition)) return false;
            var camera = Camera.main;
            if (camera == null) return false;
            _bevelDragDirection = _bevelDragger.transform.up;
            _bevelDragAxisOrigin = _bevelDragger.transform.GetChild(0).TransformPoint(Vector3.down * .5f);
            _bevelDragStartAxisDistance = ClosestAxisDistance(camera.ScreenPointToRay(screenPosition), _bevelDragAxisOrigin, _bevelDragDirection);
            _bevelDragStartWidth = m_BevelWidthMillimetres;
            _bevelDragStartWidths = (float[])_bevelWidthByEdgeMm.Clone();
            _bevelDragStartPointer = screenPosition;
            var axisStartScreen = camera.WorldToScreenPoint(_bevelDragAxisOrigin);
            var axisEndScreen = camera.WorldToScreenPoint(_bevelDragAxisOrigin + _bevelDragDirection * .1f);
            var projectedAxis = new Vector2(axisEndScreen.x - axisStartScreen.x, axisEndScreen.y - axisStartScreen.y);
            _bevelPixelsPerWorldUnit = projectedAxis.magnitude / .1f;
            _bevelDragScreenDirection = projectedAxis.sqrMagnitude > 1f ? projectedAxis.normalized : Vector2.up;
            _draggingBevelHandle = true;
            SetDraggerColor(new Color(.2f, .65f, 1f));
            _editing = true;
            SaveBevelUndo(); _dragSnapshotSaved = true;
            return true;
        }

        public bool IsPointerOverBevelDragger(Vector2 screenPosition)
        {
            var camera = Camera.main;
            if (_bevelDragger == null || camera == null) return false;

            // The object is intentionally small in the learning workspace. Use a
            // generous screen-space hit area around the visible endpoint so the
            // lever remains reliably draggable on high-DPI phones and in Play Mode.
            var endpointScreen = camera.WorldToScreenPoint(_bevelDragger.transform.position);
            if (endpointScreen.z > 0f)
            {
                var touchRadius = Mathf.Clamp(Screen.dpi > 0f ? Screen.dpi * .28f : 72f, 64f, 128f);
                if (Vector2.Distance(screenPosition, new Vector2(endpointScreen.x, endpointScreen.y)) <= touchRadius)
                    return true;
            }

            foreach (var hit in Physics.RaycastAll(camera.ScreenPointToRay(screenPosition)))
                if (hit.transform == _bevelDragger.transform || hit.transform.IsChildOf(_bevelDragger.transform)) return true;
            return false;
        }

        void SetDraggerColor(Color color)
        {
            if (_bevelDragger == null) return;
            foreach (var renderer in _bevelDragger.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.material.color = color;
                renderer.material.SetColor("_BaseColor", color);
            }
        }

        static float ClosestAxisDistance(Ray ray, Vector3 axisOrigin, Vector3 axisDirection)
        {
            var offset = axisOrigin - ray.origin;
            var rayDirection = ray.direction;
            var dot = Vector3.Dot(axisDirection, rayDirection);
            var denominator = 1f - dot * dot;
            if (Mathf.Abs(denominator) < .0001f) return Vector3.Dot(ray.origin - axisOrigin, axisDirection);
            return (dot * Vector3.Dot(rayDirection, offset) - Vector3.Dot(axisDirection, offset)) / denominator;
        }

        bool TryGetBevelEdgeAt(Vector2 screenPosition, out int edgeIndex)
        {
            edgeIndex = -1;
            var camera = Camera.main;
            if (camera == null || !Physics.Raycast(camera.ScreenPointToRay(screenPosition), out var hit)) return false;
            for (var i = 0; i < _bevelEdgeGuides.Count; i++)
                if (hit.transform == _bevelEdgeGuides[i].transform || hit.transform.IsChildOf(_bevelEdgeGuides[i].transform))
                {
                    edgeIndex = i;
                    return true;
                }
            if (hit.transform != _filter.transform) return false;
            var local = _filter.transform.InverseTransformPoint(hit.point);
            var half = _baseMeshSize * .5f;
            var best = float.PositiveInfinity;
            for (var i = 0; i < s_CubeEdges.Length; i++)
            {
                var edge = s_CubeEdges[i];
                var point = local;
                if (edge.Axis == 0) { point.y = edge.FirstSign * half.y; point.z = edge.SecondSign * half.z; }
                else if (edge.Axis == 1) { point.x = edge.FirstSign * half.x; point.z = edge.SecondSign * half.z; }
                else { point.x = edge.FirstSign * half.x; point.y = edge.SecondSign * half.y; }
                var distance = (local - point).sqrMagnitude;
                if (distance < best) { best = distance; edgeIndex = i; }
            }
            // A raw cube tap must be close to an edge; face handles are added only
            // after a deliberate face-selection step, avoiding accidental edits.
            var threshold = Mathf.Min(_baseMeshSize.x, Mathf.Min(_baseMeshSize.y, _baseMeshSize.z)) * .2f;
            return best <= threshold * threshold;
        }

        bool TrySelectBevelTargetAt(Vector2 screenPosition)
        {
            if (SelectionMode == BevelSelectionMode.Edge)
            {
                if (!TryGetBevelEdgeAt(screenPosition, out var edgeIndex)) return false;
                SelectSingleBevelEdge(edgeIndex);
                return true;
            }

            var camera = Camera.main;
            if (camera == null) return false;
            var foundSurface = false;
            var hit = new RaycastHit();
            foreach (var candidate in Physics.RaycastAll(camera.ScreenPointToRay(screenPosition)))
            {
                // Ignore edge guides and the lever. The editable mesh collider is
                // attached to the MeshFilter object itself.
                if (candidate.transform != _filter.transform) continue;
                hit = candidate;
                foundSurface = true;
                break;
            }
            if (!foundSurface) return false;

            var normal = _filter.transform.InverseTransformDirection(hit.normal).normalized;
            var axis = 0;
            if (Mathf.Abs(normal.y) > Mathf.Abs(normal.x)) axis = 1;
            if (Mathf.Abs(normal.z) > Mathf.Abs(normal[axis])) axis = 2;
            SelectBevelFace(axis, normal[axis] >= 0f ? 1 : -1);
            return true;
        }

        void SelectSingleBevelEdge(int edgeIndex)
        {
            ClearBevelSelection();
            _selectedBevelEdge = edgeIndex;
            _selectedBevelEdges[edgeIndex] = true;
            FinishBevelSelection();
        }

        void SelectBevelFace(int normalAxis, int sign)
        {
            ClearBevelSelection();
            _selectedBevelFace = true;
            _selectedBevelFaceNormal = normalAxis == 0 ? new Vector3(sign, 0f, 0f)
                : normalAxis == 1 ? new Vector3(0f, sign, 0f)
                : new Vector3(0f, 0f, sign);

            for (var i = 0; i < s_CubeEdges.Length; i++)
            {
                var edge = s_CubeEdges[i];
                if (edge.Axis == normalAxis) continue;
                var liesOnFace = normalAxis switch
                {
                    0 => edge.Axis == 1 ? edge.FirstSign == sign : edge.FirstSign == sign,
                    1 => edge.Axis == 0 ? edge.FirstSign == sign : edge.SecondSign == sign,
                    _ => edge.Axis == 0 ? edge.SecondSign == sign : edge.SecondSign == sign
                };
                if (_selectedBevelEdges[i] = liesOnFace) _selectedBevelEdge = i;
            }
            FinishBevelSelection();
        }

        void FinishBevelSelection()
        {
            LoadSelectedBevelWidth();
            if (_bevelEdgeGuides.Count == 0) CreateBevelEdgeGuides();
            UpdateBevelEdgeGuideColours();
            CreateBevelDragger();
            if (_selectionPulse != null) StopCoroutine(_selectionPulse);
            _selectionPulse = StartCoroutine(PulseBevelSelection());
        }

        IEnumerator PulseBevelSelection()
        {
            var confirmation = new Color(.20f, .90f, .45f);
            for (var i = 0; i < _bevelEdgeGuides.Count; i++)
            {
                if (!_selectedBevelEdges[i] || _bevelEdgeGuides[i] == null) continue;
                var renderer = _bevelEdgeGuides[i].GetComponent<MeshRenderer>();
                renderer.material.SetColor("_BaseColor", confirmation);
                renderer.material.SetColor("_EmissionColor", confirmation * 2.5f);
            }
            yield return new WaitForSeconds(.18f);
            UpdateBevelEdgeGuideColours();
            _selectionPulse = null;
        }

        public void DeselectBevel()
        {
            if (_selectionPulse != null)
            {
                StopCoroutine(_selectionPulse);
                _selectionPulse = null;
            }
            ClearBevelSelection();
            if (_bevelDragger != null) Destroy(_bevelDragger);
            if (_bevelValueLabel != null) Destroy(_bevelValueLabel);
            UpdateBevelEdgeGuideColours();
        }

        void ClearBevelSelection()
        {
            _selectedBevelEdge = -1;
            _selectedBevelFace = false;
            _selectedBevelFaceNormal = Vector3.zero;
            System.Array.Clear(_selectedBevelEdges, 0, _selectedBevelEdges.Length);
        }

        void RenderTool()
        {
            ClearGuides();

            switch (ActiveTool)
            {
                case Tool.Bevel:
                    if (HasBevelEdgeSelection)
                        _filter.sharedMesh = BuildBevelMesh();
                    break;
                case Tool.Extrude:
                    _filter.sharedMesh = BuildExtrudeMesh();
                    break;
                case Tool.Inset:
                    _filter.sharedMesh = BuildStoredInsetsOnExtrudeMesh();
                    break;
                case Tool.Knife:
                    _filter.sharedMesh = BuildStoredKnifeOnTopology();
                    break;
                case Tool.LoopCut:
                    _filter.sharedMesh = BuildStoredLoopCutTopology();
                    break;
            }

            RefreshCollider();
            if ((ActiveTool == Tool.Extrude && SelectedToolFaceIsGenerated) || ActiveTool is Tool.Inset or Tool.Knife)
            {
                AddSelectedExtrudeTopologyGuide();
                if (ActiveTool == Tool.Inset) UpdateToolDragger();
                if (ActiveTool == Tool.Knife) AddKnifePointGuide();
            }
            else if (ActiveTool == Tool.LoopCut)
            {
                AddLoopCutPreviewGuides();
                UpdateToolDragger();
            }
            else if (ActiveTool != Tool.Bevel && HasToolFaceSelection) AddSelectedToolGuides();
            if (ActiveTool == Tool.Bevel && _draggingBevelHandle) UpdateBevelDragger();
        }

        void RefreshCollider()
        {
            _filter.sharedMesh.RecalculateNormals();
            _filter.sharedMesh.RecalculateBounds();
            // Unity destroyed components compare equal to null, but the C# null
            // coalescing operator does not account for that special Unity state.
            if (_collider == null) _collider = _filter.gameObject.AddComponent<MeshCollider>();
            _collider.sharedMesh = null;
            _collider.sharedMesh = _filter.sharedMesh;
        }

        static float DefaultAmount(Tool tool) => tool is Tool.Knife or Tool.LoopCut ? .5f : .25f;

        void LoadSelectedBevelWidth()
        {
            m_BevelWidthMillimetres = ClampBevelWidth(_bevelWidthByEdgeMm[_selectedBevelEdge]);
            _bevelWidthByEdgeMm[_selectedBevelEdge] = m_BevelWidthMillimetres;
            GetClampedLocalBevelWidth(m_BevelWidthMillimetres, true);
        }

        public void ResetSelectedBevel()
        {
            if (!HasBevelEdgeSelection) return;
            SaveBevelUndo();
            for (var i = 0; i < _selectedBevelEdges.Length; i++)
                if (_selectedBevelEdges[i]) _bevelWidthByEdgeMm[i] = 0f;
            m_BevelWidthMillimetres = 0f;
            _filter.sharedMesh = BuildBevelMesh();
            RefreshCollider();
            UpdateBevelDragger();
        }

        public void AdjustSelectedBevel(float millimetres)
        {
            if (!HasBevelEdgeSelection) return;
            SaveBevelUndo();
            m_BevelWidthMillimetres = ClampBevelWidth(m_BevelWidthMillimetres + millimetres);
            for (var i = 0; i < _selectedBevelEdges.Length; i++)
                if (_selectedBevelEdges[i]) _bevelWidthByEdgeMm[i] = ClampBevelWidth(SnapBevelWidth(_bevelWidthByEdgeMm[i] + millimetres));
            m_BevelWidthMillimetres = _bevelWidthByEdgeMm[_selectedBevelEdge];
            _filter.sharedMesh = BuildBevelMesh();
            RefreshCollider();
            UpdateBevelDragger();
        }

        public void SetSelectedBevelWidth(float millimetres)
        {
            if (!HasBevelEdgeSelection || float.IsNaN(millimetres) || float.IsInfinity(millimetres)) return;
            SaveBevelUndo();
            // Numeric entry is exact. Snap is deliberately limited to direct
            // manipulation and step buttons so typed lesson targets are preserved.
            var width = ClampBevelWidth(millimetres);
            for (var i = 0; i < _selectedBevelEdges.Length; i++)
                if (_selectedBevelEdges[i]) _bevelWidthByEdgeMm[i] = width;
            m_BevelWidthMillimetres = width;
            _filter.sharedMesh = BuildBevelMesh();
            RefreshCollider();
            UpdateBevelDragger();
        }

        public void UndoBevel() => RestoreBevelHistory(_bevelUndo, _bevelRedo);
        public void RedoBevel() => RestoreBevelHistory(_bevelRedo, _bevelUndo);

        void SaveBevelUndo()
        {
            _bevelUndo.Push((float[])_bevelWidthByEdgeMm.Clone());
            _bevelRedo.Clear();
        }

        void RestoreBevelHistory(Stack<float[]> source, Stack<float[]> destination)
        {
            if (source.Count == 0 || _filter == null) return;
            destination.Push((float[])_bevelWidthByEdgeMm.Clone());
            var state = source.Pop();
            System.Array.Copy(state, _bevelWidthByEdgeMm, _bevelWidthByEdgeMm.Length);
            ClampAllBevelWidths();
            if (HasBevelEdgeSelection) LoadSelectedBevelWidth();
            _filter.sharedMesh = BuildBevelMesh();
            RefreshCollider();
            UpdateBevelDragger();
        }

        Mesh BuildBevelMesh()
        {
            var activeCount = 0;
            var onlyEdge = -1;
            for (var i = 0; i < _bevelWidthByEdgeMm.Length; i++)
                if (_bevelWidthByEdgeMm[i] > 0f) { activeCount++; onlyEdge = i; }
            if (activeCount == 0) return CreateBox(_baseMeshSize.x, _baseMeshSize.y, _baseMeshSize.z);
            if (activeCount == 1)
                return CreateSingleEdgeBevelledBox(_baseMeshSize, GetClampedLocalBevelWidth(_bevelWidthByEdgeMm[onlyEdge], onlyEdge == _selectedBevelEdge), s_CubeEdges[onlyEdge]);

            var widths = new Vector3[_bevelWidthByEdgeMm.Length];
            for (var i = 0; i < widths.Length; i++)
                widths[i] = GetClampedLocalBevelWidth(_bevelWidthByEdgeMm[i], _selectedBevelEdges[i]);
            return CreateMultiEdgeBevelledBox(_baseMeshSize, widths);
        }

        Vector3 GetClampedLocalBevelWidth(float widthMillimetres, bool updateSelectedDisplay)
        {
            // Unity's normal convention is one world unit = one metre. The UI is
            // millimetres, then converted to world metres before entering mesh space.
            var requestedWorldWidth = widthMillimetres * .001f;
            var scale = Absolute(_filter.transform.lossyScale);
            var maximumWorldWidth = GetMaximumBevelWidthMillimetres() * .001f;
            var clampedWorldWidth = Mathf.Min(requestedWorldWidth, maximumWorldWidth);
            if (updateSelectedDisplay) EffectiveBevelWidthMillimetres = clampedWorldWidth * 1000f;
            return new Vector3(
                clampedWorldWidth / Mathf.Max(.000001f, scale.x),
                clampedWorldWidth / Mathf.Max(.000001f, scale.y),
                clampedWorldWidth / Mathf.Max(.000001f, scale.z));
        }

        static Vector3 Absolute(Vector3 value) => new(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));

        bool BindToPlacedCube()
        {
            var placed = m_Placement == null ? null : m_Placement.PlacedObject;
            if (placed == null) return false;
            _filter = placed.GetComponentInChildren<MeshFilter>();
            _renderer = placed.GetComponentInChildren<MeshRenderer>();
            if (_filter == null || _renderer == null) return false;
            _collider = _filter.GetComponent<MeshCollider>();
            if (_collider == null) _collider = _filter.gameObject.AddComponent<MeshCollider>();
            if (_boundObject != placed)
            {
                _boundObject = placed;
                _baseMeshSize = _filter.sharedMesh.bounds.size;
                _lastBevelLossyScale = Absolute(_filter.transform.lossyScale);
                System.Array.Clear(_bevelWidthByEdgeMm, 0, _bevelWidthByEdgeMm.Length);
                _bevelUndo.Clear(); _bevelRedo.Clear();
                System.Array.Clear(_extrudeDistanceByFaceMm, 0, _extrudeDistanceByFaceMm.Length);
                _extrudeUndo.Clear(); _extrudeRedo.Clear();
                _insetPercentByFace.Clear();
                _insetUndo.Clear(); _insetRedo.Clear();
                _knifeCutByFace.Clear();
                _knifeUndo.Clear(); _knifeRedo.Clear();
                _hasKnifePointA = false;
                _knifePendingSemanticFaceId = -1;
                _loopCutEnabled = false;
                _loopCutPhase = LoopCutPhase.None;
                _loopCutAxis = 1;
                _loopCutSegments = 1;
                _loopCutSlidePercent = 0f;
                _loopRingClosed = false;
                _loopRingSpans.Clear(); _loopRingStops.Clear();
                _loopRingStopReason = "Tap an edge to discover a loop";
                _loopUndo.Clear(); _loopRedo.Clear();
                ClearBevelSelection();
            }
            return true;
        }

        void ClearGuides()
        {
            foreach (var guide in _guides) if (guide != null) Destroy(guide);
            _guides.Clear();
        }

        readonly struct CubeEdge
        {
            public readonly int Axis;
            public readonly int FirstSign;
            public readonly int SecondSign;
            public CubeEdge(int axis, int firstSign, int secondSign) { Axis = axis; FirstSign = firstSign; SecondSign = secondSign; }
        }

        static readonly CubeEdge[] s_CubeEdges =
        {
            new(0,-1,-1), new(0,-1,1), new(0,1,-1), new(0,1,1),
            new(1,-1,-1), new(1,-1,1), new(1,1,-1), new(1,1,1),
            new(2,-1,-1), new(2,-1,1), new(2,1,-1), new(2,1,1)
        };

        void CreateBevelEdgeGuides()
        {
            ClearBevelEdgeGuides();
            var half = _baseMeshSize * .5f;
            foreach (var edge in s_CubeEdges)
            {
                var guide = GameObject.CreatePrimitive(PrimitiveType.Cube);
                guide.name = "Selectable Bevel Edge";
                guide.transform.SetParent(_filter.transform, false);
                var length = edge.Axis == 0 ? _baseMeshSize.x : edge.Axis == 1 ? _baseMeshSize.y : _baseMeshSize.z;
                const float visibleThickness = .007f;
                guide.transform.localScale = edge.Axis == 0 ? new Vector3(length, visibleThickness, visibleThickness) : edge.Axis == 1 ? new Vector3(visibleThickness, length, visibleThickness) : new Vector3(visibleThickness, visibleThickness, length);
                var position = Vector3.zero;
                if (edge.Axis == 0) { position.y = edge.FirstSign * half.y; position.z = edge.SecondSign * half.z; }
                if (edge.Axis == 1) { position.x = edge.FirstSign * half.x; position.z = edge.SecondSign * half.z; }
                if (edge.Axis == 2) { position.x = edge.FirstSign * half.x; position.y = edge.SecondSign * half.y; }
                guide.transform.localPosition = position;
                // Blender-like thin visible edge, with a separate larger invisible
                // hit target so selecting it remains practical on touch screens.
                Destroy(guide.GetComponent<Collider>());
                var hitTarget = new GameObject("Edge Touch Target", typeof(BoxCollider));
                hitTarget.transform.SetParent(guide.transform, false);
                hitTarget.transform.localScale = edge.Axis == 0 ? new Vector3(1f, 9f, 9f) : edge.Axis == 1 ? new Vector3(9f, 1f, 9f) : new Vector3(9f, 9f, 1f);
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", new Color(.25f, .65f, 1f));
                guide.GetComponent<MeshRenderer>().material = material;
                _bevelEdgeGuides.Add(guide);
            }
        }

        void UpdateBevelEdgeGuideColours()
        {
            for (var i = 0; i < _bevelEdgeGuides.Count; i++)
            {
                var selected = _selectedBevelEdges[i];
                var renderer = _bevelEdgeGuides[i].GetComponent<MeshRenderer>();
                var tutorialTarget = !selected && i == _tutorialTargetEdge;
                var colour = selected ? new Color(1f, .72f, .05f)
                    : tutorialTarget ? new Color(.20f, .90f, .45f)
                    : new Color(.25f, .65f, 1f);
                renderer.material.SetColor("_BaseColor", colour);
                renderer.material.EnableKeyword("_EMISSION");
                renderer.material.SetColor("_EmissionColor", colour * (selected || tutorialTarget ? 2.5f : .35f));

                // The chosen edge is deliberately thicker, making the active
                // modelling target clear on a small mobile screen.
                var edge = s_CubeEdges[i];
                var length = edge.Axis == 0 ? _baseMeshSize.x : edge.Axis == 1 ? _baseMeshSize.y : _baseMeshSize.z;
                var thickness = selected ? .016f : .007f;
                _bevelEdgeGuides[i].transform.localScale = edge.Axis == 0 ? new Vector3(length, thickness, thickness) : edge.Axis == 1 ? new Vector3(thickness, length, thickness) : new Vector3(thickness, thickness, length);
            }
        }

        void CreateBevelDragger()
        {
            if (!HasBevelEdgeSelection) return;
            if (_bevelDragger != null) Destroy(_bevelDragger);
            GetBevelDraggerFrame(out var midpoint, out var direction);
            var length = .22f + GetClampedLocalBevelWidth(m_BevelWidthMillimetres, true).magnitude;
            _bevelDragger = new GameObject("Bevel Dragger"); _bevelDragger.transform.SetParent(_filter.transform, false);
            _bevelDragger.transform.localPosition = midpoint + direction * length;
            _bevelDragger.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube); line.transform.SetParent(_bevelDragger.transform, false); line.transform.localPosition = Vector3.down * length * .5f; line.transform.localScale = new Vector3(.01f, length, .01f); Destroy(line.GetComponent<Collider>());
            var end = GameObject.CreatePrimitive(PrimitiveType.Sphere); end.name = "Bevel Drag Handle"; end.transform.SetParent(_bevelDragger.transform, false);
            // The learning cube is 0.12 Unity units wide, so the visual circle
            // needs a substantially larger local collider to be touchable on mobile.
            end.transform.localScale = Vector3.one * .30f;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.SetColor("_BaseColor", new Color(1f,.72f,.05f)); line.GetComponent<MeshRenderer>().material = mat; end.GetComponent<MeshRenderer>().material = mat;
            CreateBevelValueLabel();
            UpdateBevelDragger();
        }

        void UpdateBevelDragger()
        {
            if (_bevelDragger == null || !HasBevelEdgeSelection) return;
            GetBevelDraggerFrame(out var midpoint, out var direction);
            var length = .22f + GetClampedLocalBevelWidth(m_BevelWidthMillimetres, true).magnitude;
            _bevelDragger.transform.localPosition = midpoint + direction * length;
            _bevelDragger.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            if (_bevelDragger.transform.childCount > 0)
            {
                var line = _bevelDragger.transform.GetChild(0); line.localPosition = Vector3.down * length * .5f; line.localScale = new Vector3(.01f, length, .01f);
            }
            UpdateBevelValueLabel();
        }

        void CreateBevelValueLabel()
        {
            if (_bevelValueLabel != null) Destroy(_bevelValueLabel);
            _bevelValueLabel = new GameObject("Bevel Width Label", typeof(TextMesh));
            var text = _bevelValueLabel.GetComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleLeft;
            text.alignment = TextAlignment.Left;
            text.fontSize = 64;
            text.characterSize = .006f;
            text.richText = true;
            text.color = Color.white;
        }

        void UpdateBevelValueLabel()
        {
            var camera = Camera.main;
            if (_bevelValueLabel == null || _bevelDragger == null || camera == null) return;
            _bevelValueLabel.transform.position = _bevelDragger.transform.position + camera.transform.right * .025f + camera.transform.up * .018f;
            _bevelValueLabel.transform.rotation = camera.transform.rotation;
            var text = _bevelValueLabel.GetComponent<TextMesh>();
            text.color = IsBevelAtMaximum ? new Color(1f, .68f, .15f) : Color.white;
            text.text = IsBevelAtMaximum
                ? $"{EffectiveBevelWidthMillimetres:0.0} mm\n<size=38>Maximum for this cube</size>"
                : $"{EffectiveBevelWidthMillimetres:0.0} mm";
        }

        void GetBevelDraggerFrame(out Vector3 anchor, out Vector3 direction)
        {
            var half = _baseMeshSize * .5f;
            if (_selectedBevelFace)
            {
                direction = _selectedBevelFaceNormal;
                anchor = Vector3.Scale(direction, half);
                return;
            }

            var edge = s_CubeEdges[_selectedBevelEdge];
            anchor = Vector3.zero;
            direction = edge.Axis == 0 ? new Vector3(0, edge.FirstSign, edge.SecondSign).normalized
                : edge.Axis == 1 ? new Vector3(edge.FirstSign, 0, edge.SecondSign).normalized
                : new Vector3(edge.FirstSign, edge.SecondSign, 0).normalized;
            if (edge.Axis == 0) { anchor.y = edge.FirstSign * half.y; anchor.z = edge.SecondSign * half.z; }
            else if (edge.Axis == 1) { anchor.x = edge.FirstSign * half.x; anchor.z = edge.SecondSign * half.z; }
            else { anchor.x = edge.FirstSign * half.x; anchor.y = edge.SecondSign * half.y; }
        }

        void ClearBevelEdgeGuides()
        {
            if (_bevelDragger != null) Destroy(_bevelDragger);
            if (_bevelValueLabel != null) Destroy(_bevelValueLabel);
            foreach (var guide in _bevelEdgeGuides) if (guide != null) Destroy(guide);
            _bevelEdgeGuides.Clear();
        }

        void AddSelectedToolGuides()
        {
            var normal = _selectedToolFaceNormal;
            var half = _baseMeshSize * .5f;
            var center = Vector3.Scale(normal, half);
            if (ActiveTool == Tool.Extrude)
                AddExtrudeTopologyGuides(GetExtrudeLocalDistance());
            else
            {
                var shrink = ActiveTool == Tool.Inset ? 1f - Mathf.Lerp(.02f, .46f, _amount) * 2f : .55f;
                var scale = _baseMeshSize * shrink;
                scale[_selectedToolFaceAxis] = .012f;
                AddBoxGuide("Selected Tool Face", center + normal * .008f, scale, new Color(1f, .62f, .08f));
            }

            if (ActiveTool == Tool.Knife) AddFaceKnifeGuide(Mathf.Lerp(-70f, 70f, _amount));
            if (ActiveTool == Tool.LoopCut) AddLoopGuide(Mathf.Lerp(-.45f, .45f, _amount) * _baseMeshSize.y);
            UpdateToolDragger();
        }

        void AddSelectedExtrudeTopologyGuide()
        {
            if (_filter == null || _selectedToolTopologyFaceId < 0) return;
            var source = _filter.sharedMesh;
            var sourceVertices = source.vertices;
            var sourceTriangles = source.triangles;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var triangle = 0; triangle < _extrudeTriangleFaceIds.Count; triangle++)
            {
                if (_extrudeTriangleFaceIds[triangle] != _selectedToolTopologyFaceId) continue;
                var a = sourceVertices[sourceTriangles[triangle * 3]];
                var b = sourceVertices[sourceTriangles[triangle * 3 + 1]];
                var c = sourceVertices[sourceTriangles[triangle * 3 + 2]];
                var normal = Vector3.Cross(b - a, c - a).normalized;
                var start = vertices.Count;
                vertices.Add(a + normal * .006f);
                vertices.Add(b + normal * .006f);
                vertices.Add(c + normal * .006f);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            }
            if (triangles.Count == 0) return;
            var guide = new GameObject($"Selected {SelectedToolTopologyLabel}", typeof(MeshFilter), typeof(MeshRenderer));
            guide.transform.SetParent(_filter.transform, false);
            var mesh = new Mesh { name = "Generated Extrude Face Selection" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            guide.GetComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(1f, .58f, .08f));
            guide.GetComponent<MeshRenderer>().material = material;
            _guides.Add(guide);
        }

        void AddExtrudeTopologyGuides(float distance)
        {
            const float lineThickness = .014f;
            const float surfaceThickness = .009f;
            var axis = _selectedToolFaceAxis;
            var uAxis = axis == 0 ? 1 : 0;
            var vAxis = axis == 2 ? 1 : 2;
            var normal = _selectedToolFaceNormal;
            var half = _baseMeshSize * .5f;
            var baseCenter = Vector3.Scale(normal, half);
            var capCenter = baseCenter + normal * distance;
            var blue = new Color(.18f, .62f, 1f);
            var orange = new Color(1f, .58f, .08f);
            var generatedSide = new Color(.27f, .62f, .46f);

            AddFaceBoundaryLoop("Extrude Base Loop", baseCenter, axis, uAxis, vAxis, lineThickness, blue);

            var capScale = _baseMeshSize * .94f;
            capScale[axis] = surfaceThickness;
            AddBoxGuide("Extruded Cap", capCenter + normal * .008f, capScale, orange);
            AddFaceBoundaryLoop("Extruded Cap Loop", capCenter + normal * .014f, axis, uAxis, vAxis, lineThickness, orange);

            if (Mathf.Abs(distance) < .0001f) return;
            for (var sign = -1; sign <= 1; sign += 2)
            {
                var uSidePosition = baseCenter + normal * (distance * .5f);
                uSidePosition[uAxis] = sign * half[uAxis] + sign * .006f;
                var uSideScale = Vector3.one * surfaceThickness;
                uSideScale[axis] = Mathf.Abs(distance);
                uSideScale[vAxis] = _baseMeshSize[vAxis] * .96f;
                AddBoxGuide("Generated Extrude Side", uSidePosition, uSideScale, generatedSide);

                var vSidePosition = baseCenter + normal * (distance * .5f);
                vSidePosition[vAxis] = sign * half[vAxis] + sign * .006f;
                var vSideScale = Vector3.one * surfaceThickness;
                vSideScale[axis] = Mathf.Abs(distance);
                vSideScale[uAxis] = _baseMeshSize[uAxis] * .96f;
                AddBoxGuide("Generated Extrude Side", vSidePosition, vSideScale, generatedSide);
            }
        }

        void AddFaceBoundaryLoop(string name, Vector3 center, int normalAxis, int uAxis, int vAxis, float thickness, Color color)
        {
            var half = _baseMeshSize * .5f;
            for (var sign = -1; sign <= 1; sign += 2)
            {
                var uBoundaryPosition = center;
                uBoundaryPosition[uAxis] += sign * half[uAxis];
                var uBoundaryScale = Vector3.one * thickness;
                uBoundaryScale[vAxis] = _baseMeshSize[vAxis];
                uBoundaryScale[normalAxis] = thickness;
                AddBoxGuide(name, uBoundaryPosition, uBoundaryScale, color);

                var vBoundaryPosition = center;
                vBoundaryPosition[vAxis] += sign * half[vAxis];
                var vBoundaryScale = Vector3.one * thickness;
                vBoundaryScale[uAxis] = _baseMeshSize[uAxis];
                vBoundaryScale[normalAxis] = thickness;
                AddBoxGuide(name, vBoundaryPosition, vBoundaryScale, color);
            }
        }

        void CreateToolDragger()
        {
            ClearToolDragger();
            if (!HasToolFaceSelection || ActiveTool is not (Tool.Extrude or Tool.Inset or Tool.LoopCut) ||
                (ActiveTool == Tool.Extrude && !CanManipulateSelectedExtrudeFace) ||
                (ActiveTool == Tool.LoopCut && (_loopCutPhase != LoopCutPhase.Sliding || !_loopRingClosed || _loopRingSpans.Count == 0))) return;
            _toolDragger = new GameObject($"{ActiveTool} Dragger");
            _toolDragger.transform.SetParent(_filter.transform, false);
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = $"{ActiveTool} Lever Line";
            line.transform.SetParent(_toolDragger.transform, false);
            Destroy(line.GetComponent<Collider>());
            var endpoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            endpoint.name = $"{ActiveTool} Drag Handle";
            endpoint.transform.SetParent(_toolDragger.transform, false);
            endpoint.transform.localScale = Vector3.one * .30f;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(1f, .72f, .05f));
            line.GetComponent<MeshRenderer>().material = material;
            endpoint.GetComponent<MeshRenderer>().material = material;
            if (ActiveTool is Tool.Extrude or Tool.Inset or Tool.LoopCut) CreateToolValueLabel();
            UpdateToolDragger();
        }

        void UpdateToolDragger()
        {
            if (_toolDragger == null || !HasToolFaceSelection) return;
            var normal = _selectedToolFaceNormal;
            var half = _baseMeshSize * .5f;
            var anchor = Vector3.Scale(normal, half);
            Vector3 direction;
            if (ActiveTool == Tool.Extrude)
            {
                anchor += normal * GetExtrudeLocalDistance();
                direction = normal;
            }
            else if (ActiveTool == Tool.LoopCut)
            {
                var span = _loopRingSpans[0];
                var t = Mathf.Clamp((1f + _loopCutSlidePercent / 100f) / (_loopCutSegments + 1f), .02f, .98f);
                var first = Vector3.Lerp(span.EntryA, span.EntryB, t);
                var second = Vector3.Lerp(span.OppositeA, span.OppositeB, t);
                anchor = (first + second) * .5f;
                direction = (span.EntryB - span.EntryA + span.OppositeB - span.OppositeA).normalized;
                if (direction.sqrMagnitude < .0001f) direction = Vector3.up;
            }
            else
            {
                var reference = Mathf.Abs(normal.y) < .9f ? Vector3.up : Vector3.right;
                var tangent = Vector3.Cross(normal, reference).normalized;
                var bitangent = Vector3.Cross(normal, tangent).normalized;
                direction = (tangent + bitangent).normalized;
                anchor = _selectedToolFaceCenter;
            }
            var length = .18f + (ActiveTool == Tool.Inset ? _amount * .08f : 0f);
            _toolDragger.transform.localPosition = anchor + direction * length;
            _toolDragger.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            if (_toolDragger.transform.childCount > 0)
            {
                var line = _toolDragger.transform.GetChild(0);
                line.localPosition = Vector3.down * length * .5f;
                line.localScale = new Vector3(.01f, length, .01f);
            }
            UpdateToolValueLabel();
        }

        void CreateToolValueLabel()
        {
            if (_toolValueLabel != null) Destroy(_toolValueLabel);
            _toolValueLabel = new GameObject($"{ActiveTool} Value Label", typeof(TextMesh));
            var text = _toolValueLabel.GetComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleLeft;
            text.alignment = TextAlignment.Left;
            text.fontSize = 64;
            text.characterSize = .006f;
            text.richText = true;
            text.color = Color.white;
        }

        void UpdateToolValueLabel()
        {
            var camera = Camera.main;
            if (_toolValueLabel == null || _toolDragger == null || camera == null || ActiveTool is not (Tool.Extrude or Tool.Inset or Tool.LoopCut)) return;
            _toolValueLabel.transform.position = _toolDragger.transform.position + camera.transform.right * .025f + camera.transform.up * .018f;
            _toolValueLabel.transform.rotation = camera.transform.rotation;
            var text = _toolValueLabel.GetComponent<TextMesh>();
            if (ActiveTool == Tool.LoopCut)
            {
                var atLimit = Mathf.Abs(_loopCutSlidePercent) >= 44.99f;
                text.color = atLimit ? new Color(1f, .68f, .15f) : Color.white;
                text.text = atLimit ? $"{_loopCutSlidePercent:+0.0;-0.0;0.0}%\n<size=38>Slide limit</size>" : $"{_loopCutSlidePercent:+0.0;-0.0;0.0}% slide";
                return;
            }
            if (ActiveTool == Tool.Inset)
            {
                text.color = IsInsetAtMaximum ? new Color(1f, .68f, .15f) : Color.white;
                text.text = IsInsetAtMaximum
                    ? $"{EffectiveInsetPercent:0.0}%\n<size=38>Maximum inset</size>"
                    : $"{EffectiveInsetPercent:0.0}% inset";
                return;
            }
            text.color = IsExtrudeAtLimit ? new Color(1f, .68f, .15f) : Color.white;
            var direction = EffectiveExtrudeDistanceMillimetres > .01f ? "Outward" : EffectiveExtrudeDistanceMillimetres < -.01f ? "Inward" : "On face";
            text.text = IsExtrudeAtLimit
                ? $"{EffectiveExtrudeDistanceMillimetres:+0.0;-0.0;0.0} mm  {direction}\n<size=38>Extrude limit</size>"
                : $"{EffectiveExtrudeDistanceMillimetres:+0.0;-0.0;0.0} mm  {direction}";
        }

        void ClearToolDragger()
        {
            if (_toolDragger != null) Destroy(_toolDragger);
            if (_toolValueLabel != null) Destroy(_toolValueLabel);
            _toolDragger = null;
            _toolValueLabel = null;
            _draggingToolHandle = false;
        }

        void AddFaceKnifeGuide(float angleDegrees)
        {
            var normal = _selectedToolFaceNormal;
            var reference = Mathf.Abs(normal.y) < .9f ? Vector3.up : Vector3.right;
            var tangent = Vector3.Cross(normal, reference).normalized;
            var bitangent = Vector3.Cross(normal, tangent).normalized;
            var radians = angleDegrees * Mathf.Deg2Rad;
            var lineDirection = tangent * Mathf.Cos(radians) + bitangent * Mathf.Sin(radians);
            var center = Vector3.Scale(normal, _baseMeshSize * .5f) + normal * .016f;
            var guide = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guide.name = "Knife Topology Cut";
            guide.transform.SetParent(_filter.transform, false);
            guide.transform.localPosition = center;
            guide.transform.localRotation = Quaternion.FromToRotation(Vector3.right, lineDirection);
            guide.transform.localScale = new Vector3(_baseMeshSize.magnitude * .75f, .018f, .018f);
            Destroy(guide.GetComponent<Collider>());
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(.95f, .18f, .12f));
            guide.GetComponent<MeshRenderer>().material = material;
            _guides.Add(guide);
        }

        void AddKnifePointGuide()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(1f, .22f, .12f));
            if (_hasKnifePointA)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "Knife Point A";
                marker.transform.SetParent(_filter.transform, false);
                marker.transform.localPosition = _knifePointA + _selectedToolFaceNormal * .01f;
                marker.transform.localScale = Vector3.one * .045f;
                Destroy(marker.GetComponent<Collider>());
                marker.GetComponent<MeshRenderer>().material = material;
                _guides.Add(marker);
            }
            if (!_knifeCutByFace.TryGetValue(_selectedToolTopologyFaceId, out var cut)) return;
            var direction = cut.PointB - cut.PointA;
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "Committed Knife Cut";
            line.transform.SetParent(_filter.transform, false);
            line.transform.localPosition = (cut.PointA + cut.PointB) * .5f + _selectedToolFaceNormal * .012f;
            line.transform.localRotation = Quaternion.FromToRotation(Vector3.right, direction.normalized);
            line.transform.localScale = new Vector3(direction.magnitude, .012f, .012f);
            Destroy(line.GetComponent<Collider>());
            line.GetComponent<MeshRenderer>().material = material;
            _guides.Add(line);
        }

        Mesh BuildExtrudeMesh()
        {
            var half = _baseMeshSize * .5f;
            var coordinates = new List<float>[3];
            for (var axis = 0; axis < 3; axis++)
            {
                var scale = Mathf.Max(.00001f, Mathf.Abs(_filter.transform.lossyScale[axis]));
                coordinates[axis] = new List<float> { -half[axis], half[axis] };
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    var localDistance = _extrudeDistanceByFaceMm[GetFaceId(axis, sign)] / (scale * 1000f);
                    coordinates[axis].Add(sign * half[axis] + sign * localDistance);
                }
                coordinates[axis].Sort();
                for (var i = coordinates[axis].Count - 1; i > 0; i--)
                    if (Mathf.Abs(coordinates[axis][i] - coordinates[axis][i - 1]) < .000001f)
                        coordinates[axis].RemoveAt(i);
            }

            var nx = coordinates[0].Count - 1;
            var ny = coordinates[1].Count - 1;
            var nz = coordinates[2].Count - 1;
            var occupied = new bool[nx, ny, nz];
            for (var x = 0; x < nx; x++)
            for (var y = 0; y < ny; y++)
            for (var z = 0; z < nz; z++)
            {
                var point = new Vector3(
                    (coordinates[0][x] + coordinates[0][x + 1]) * .5f,
                    (coordinates[1][y] + coordinates[1][y + 1]) * .5f,
                    (coordinates[2][z] + coordinates[2][z + 1]) * .5f);
                occupied[x, y, z] = IsInsideExtrudedSolid(point, half);
            }

            var faces = new List<PolygonFace>();
            var topologyFaceIds = new List<int>();
            for (var x = 0; x < nx; x++)
            for (var y = 0; y < ny; y++)
            for (var z = 0; z < nz; z++)
            {
                if (!occupied[x, y, z]) continue;
                var minimum = new Vector3(coordinates[0][x], coordinates[1][y], coordinates[2][z]);
                var maximum = new Vector3(coordinates[0][x + 1], coordinates[1][y + 1], coordinates[2][z + 1]);
                for (var axis = 0; axis < 3; axis++)
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    var neighborX = x + (axis == 0 ? sign : 0);
                    var neighborY = y + (axis == 1 ? sign : 0);
                    var neighborZ = z + (axis == 2 ? sign : 0);
                    var neighborOccupied = neighborX >= 0 && neighborX < nx && neighborY >= 0 && neighborY < ny && neighborZ >= 0 && neighborZ < nz && occupied[neighborX, neighborY, neighborZ];
                    if (!neighborOccupied)
                    {
                        var face = CreateCellBoundaryFace(minimum, maximum, axis, sign);
                        faces.Add(face);
                        topologyFaceIds.Add(ClassifyExtrudeBoundaryFace(face, axis, sign, half));
                    }
                }
            }
            return CreateExtrudePolygonMesh(faces, topologyFaceIds);
        }

        int ClassifyExtrudeBoundaryFace(PolygonFace face, int normalAxis, int normalSign, Vector3 half)
        {
            var center = Vector3.zero;
            foreach (var vertex in face.Vertices) center += vertex;
            center /= face.Vertices.Count;
            var plane = center[normalAxis];

            // A moved cap retains the stable ID of its originating cube face.
            var capDistance = GetStoredExtrudeLocalDistance(normalAxis, normalSign);
            var capPlane = normalSign * half[normalAxis] + normalSign * capDistance;
            if (Mathf.Abs(plane - capPlane) < .00001f && InsideBaseTangents(center, half, normalAxis, .00001f))
                return GetFaceId(normalAxis, normalSign);

            // A generated side is keyed by its source face plus one of that
            // face's four stable tangent boundaries. Grid fragments created by
            // adjacent operations deliberately share the same semantic ID.
            for (var sourceAxis = 0; sourceAxis < 3; sourceAxis++)
            {
                if (sourceAxis == normalAxis) continue;
                for (var sourceSign = -1; sourceSign <= 1; sourceSign += 2)
                {
                    var distance = GetStoredExtrudeLocalDistance(sourceAxis, sourceSign);
                    if (Mathf.Abs(distance) < .000001f) continue;
                    var boundary = sourceSign * half[sourceAxis];
                    var cap = boundary + sourceSign * distance;
                    if (center[sourceAxis] < Mathf.Min(boundary, cap) - .00001f || center[sourceAxis] > Mathf.Max(boundary, cap) + .00001f) continue;
                    if (Mathf.Abs(Mathf.Abs(plane) - half[normalAxis]) > .00001f) continue;
                    var tangents = sourceAxis == 0 ? new[] { 1, 2 } : sourceAxis == 1 ? new[] { 0, 2 } : new[] { 0, 1 };
                    var tangentIndex = tangents[0] == normalAxis ? 0 : 1;
                    var slot = tangentIndex * 2 + (normalSign > 0 ? 1 : 0);
                    return 6 + GetFaceId(sourceAxis, sourceSign) * 4 + slot;
                }
            }

            return GetFaceId(normalAxis, normalSign);
        }

        Mesh CreateExtrudePolygonMesh(List<PolygonFace> faces, List<int> topologyFaceIds)
        {
            _editableTopology.Rebuild(faces, topologyFaceIds);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            _extrudeTriangleFaceIds.Clear();
            _extrudeTriangleGraphFaceIds.Clear();
            for (var i = 0; i < faces.Count; i++)
            {
                var triangleCountBefore = triangles.Count / 3;
                AddFace(vertices, triangles, faces[i].Vertices, faces[i].Normal);
                var triangleCountAfter = triangles.Count / 3;
                for (var triangle = triangleCountBefore; triangle < triangleCountAfter; triangle++)
                {
                    _extrudeTriangleFaceIds.Add(topologyFaceIds[i]);
                    _extrudeTriangleGraphFaceIds.Add(_editableTopology.Faces[i].Id);
                }
            }
            return CreateMesh(vertices.ToArray(), triangles.ToArray());
        }

        bool IsInsideExtrudedSolid(Vector3 point, Vector3 half)
        {
            const float epsilon = .000001f;
            var inside = Mathf.Abs(point.x) <= half.x + epsilon && Mathf.Abs(point.y) <= half.y + epsilon && Mathf.Abs(point.z) <= half.z + epsilon;

            // Positive face distances add six axis-aligned extrusion prisms to
            // one solid. Their shared/internal faces disappear during boundary extraction.
            for (var axis = 0; axis < 3; axis++)
            for (var sign = -1; sign <= 1; sign += 2)
            {
                var distance = GetStoredExtrudeLocalDistance(axis, sign);
                if (distance <= epsilon || !InsideBaseTangents(point, half, axis, epsilon)) continue;
                var boundary = sign * half[axis];
                var cap = boundary + sign * distance;
                var coordinate = point[axis];
                if (coordinate >= Mathf.Min(boundary, cap) - epsilon && coordinate <= Mathf.Max(boundary, cap) + epsilon) inside = true;
            }

            // Negative distances push a face into the solid. Apply these cuts
            // after unions so the effective cap always agrees with its lever.
            for (var axis = 0; axis < 3; axis++)
            for (var sign = -1; sign <= 1; sign += 2)
            {
                var distance = GetStoredExtrudeLocalDistance(axis, sign);
                if (distance >= -epsilon || !InsideBaseTangents(point, half, axis, epsilon)) continue;
                var boundary = sign * half[axis];
                var cap = boundary + sign * distance;
                var coordinate = point[axis];
                if (coordinate >= Mathf.Min(boundary, cap) - epsilon && coordinate <= Mathf.Max(boundary, cap) + epsilon) inside = false;
            }
            return inside;
        }

        float GetStoredExtrudeLocalDistance(int axis, int sign)
        {
            var scale = Mathf.Max(.00001f, Mathf.Abs(_filter.transform.lossyScale[axis]));
            return _extrudeDistanceByFaceMm[GetFaceId(axis, sign)] / (scale * 1000f);
        }

        static bool InsideBaseTangents(Vector3 point, Vector3 half, int normalAxis, float epsilon)
        {
            for (var axis = 0; axis < 3; axis++)
                if (axis != normalAxis && Mathf.Abs(point[axis]) > half[axis] + epsilon) return false;
            return true;
        }

        static PolygonFace CreateCellBoundaryFace(Vector3 minimum, Vector3 maximum, int axis, int sign)
        {
            var normal = Vector3.zero;
            normal[axis] = sign;
            var uAxis = axis == 0 ? 1 : 0;
            var vAxis = axis == 2 ? 1 : 2;
            var plane = sign > 0 ? maximum[axis] : minimum[axis];
            var vertices = new List<Vector3>(4);
            for (var i = 0; i < 4; i++)
            {
                var vertex = Vector3.zero;
                vertex[axis] = plane;
                vertex[uAxis] = (i == 0 || i == 3) ? minimum[uAxis] : maximum[uAxis];
                vertex[vAxis] = i < 2 ? minimum[vAxis] : maximum[vAxis];
                vertices.Add(vertex);
            }
            if (Vector3.Dot(Vector3.Cross(vertices[1] - vertices[0], vertices[2] - vertices[0]), normal) < 0f)
                vertices.Reverse();
            return new PolygonFace(vertices, normal);
        }

        Mesh BuildStoredInsetsOnExtrudeMesh()
        {
            // Rebuild the current Extrude result first so Inset always consumes
            // the latest shared topology rather than the original learning cube.
            BuildExtrudeMesh();
            var faces = new List<PolygonFace>();
            var semanticIds = new List<int>();
            foreach (var topologyFace in _editableTopology.Faces)
            {
                var polygon = new List<Vector3>(topologyFace.VertexIds.Length);
                foreach (var vertexId in topologyFace.VertexIds) polygon.Add(_editableTopology.Vertices[vertexId].Position);
                if (!_insetPercentByFace.TryGetValue(topologyFace.SemanticId, out var insetPercent) || insetPercent <= .0001f || polygon.Count < 3)
                {
                    faces.Add(new PolygonFace(polygon, topologyFace.Normal));
                    semanticIds.Add(topologyFace.SemanticId);
                    continue;
                }

                var insetFraction = Mathf.Clamp(insetPercent / 100f, .00001f, .45f);
                var center = Vector3.zero;
                foreach (var point in polygon) center += point;
                center /= polygon.Count;
                var inner = new List<Vector3>(polygon.Count);
                foreach (var point in polygon) inner.Add(Vector3.Lerp(point, center, insetFraction));
                for (var i = 0; i < polygon.Count; i++)
                {
                    var next = (i + 1) % polygon.Count;
                    faces.Add(new PolygonFace(new List<Vector3> { polygon[i], polygon[next], inner[next], inner[i] }, topologyFace.Normal));
                    semanticIds.Add(topologyFace.SemanticId);
                }
                faces.Add(new PolygonFace(inner, topologyFace.Normal));
                semanticIds.Add(topologyFace.SemanticId);
            }
            return CreateExtrudePolygonMesh(faces, semanticIds);
        }

        Mesh BuildStoredKnifeOnTopology()
        {
            BuildStoredInsetsOnExtrudeMesh();
            var faces = new List<PolygonFace>();
            var semanticIds = new List<int>();
            foreach (var topologyFace in _editableTopology.Faces)
            {
                var polygon = new List<Vector3>(topologyFace.VertexIds.Length);
                foreach (var vertexId in topologyFace.VertexIds) polygon.Add(_editableTopology.Vertices[vertexId].Position);
                if (!_knifeCutByFace.TryGetValue(topologyFace.SemanticId, out var cut))
                {
                    faces.Add(new PolygonFace(polygon, topologyFace.Normal));
                    semanticIds.Add(topologyFace.SemanticId);
                    continue;
                }

                var lineDirection = cut.PointB - cut.PointA;
                lineDirection -= topologyFace.Normal * Vector3.Dot(lineDirection, topologyFace.Normal);
                if (lineDirection.sqrMagnitude < .0000001f)
                {
                    faces.Add(new PolygonFace(polygon, topologyFace.Normal));
                    semanticIds.Add(topologyFace.SemanticId);
                    continue;
                }
                var dividerNormal = Vector3.Cross(topologyFace.Normal, lineDirection.normalized).normalized;
                var first = ClipCoplanarPolygon(polygon, cut.PointA, dividerNormal, true);
                var second = ClipCoplanarPolygon(polygon, cut.PointA, dividerNormal, false);
                if (first.Count >= 3)
                {
                    faces.Add(new PolygonFace(first, topologyFace.Normal));
                    semanticIds.Add(topologyFace.SemanticId);
                }
                if (second.Count >= 3)
                {
                    faces.Add(new PolygonFace(second, topologyFace.Normal));
                    semanticIds.Add(topologyFace.SemanticId);
                }
            }
            return CreateExtrudePolygonMesh(faces, semanticIds);
        }

        public void CycleLoopCutAxis()
        {
            if (_loopCutPhase != LoopCutPhase.Preview) return;
            SaveLoopUndoIfCommitted();
            _loopCutAxis = (_loopCutAxis + 1) % 3;
            _loopCutEnabled = true;
            if (_loopDiscoveryFaceId >= 0)
            {
                BuildStoredKnifeOnTopology();
                DiscoverLoopRing(_loopDiscoveryFaceId, _loopDiscoveryPoint, _loopCutAxis);
            }
            RenderTool();
        }

        static string GetAxisName(int axis) => axis == 0 ? "X" : axis == 1 ? "Y" : "Z";

        void DiscoverLoopRing(int graphFaceId, Vector3 localHitPoint, int requiredAxis = -1)
        {
            _loopDiscoveryFaceId = graphFaceId;
            _loopDiscoveryPoint = localHitPoint;
            _loopRingSpans.Clear();
            _loopRingStops.Clear();
            _loopRingClosed = false;
            var face = _editableTopology.Faces.Find(candidate => candidate.Id == graphFaceId);
            if (face == null && _selectedToolTopologyFaceId >= 0)
            {
                var closestDistance = float.PositiveInfinity;
                foreach (var candidate in _editableTopology.Faces)
                {
                    if (candidate.SemanticId != _selectedToolTopologyFaceId) continue;
                    var center = Vector3.zero;
                    foreach (var vertexId in candidate.VertexIds) center += _editableTopology.Vertices[vertexId].Position;
                    center /= candidate.VertexIds.Length;
                    var distance = (center - localHitPoint).sqrMagnitude;
                    if (distance < closestDistance) { closestDistance = distance; face = candidate; }
                }
            }
            if (face == null)
            {
                _loopRingStopReason = "No editable face under the pointer";
                return;
            }
            if (face.VertexIds.Length != 4)
            {
                _loopRingStopReason = $"Loop stopped — selected face has {face.VertexIds.Length} sides, not four";
                _loopRingStops.Add(localHitPoint);
                return;
            }

            EditableTopologyGraph.Edge startEdge = null;
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < face.VertexIds.Length; i++)
            {
                var edge = FindTopologyEdge(face.VertexIds[i], face.VertexIds[(i + 1) % face.VertexIds.Length]);
                if (edge == null) continue;
                var a = _editableTopology.Vertices[edge.VertexA].Position;
                var b = _editableTopology.Vertices[edge.VertexB].Position;
                var direction = b - a;
                var dominantAxis = 0;
                if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x)) dominantAxis = 1;
                if (Mathf.Abs(direction.z) > Mathf.Abs(direction[dominantAxis])) dominantAxis = 2;
                if (requiredAxis >= 0 && dominantAxis != requiredAxis) continue;
                var distance = (ClosestPointOnSegment(localHitPoint, a, b) - localHitPoint).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; startEdge = edge; }
            }
            if (startEdge == null)
            {
                _loopRingStopReason = requiredAxis >= 0 ? $"No edge aligned with local {GetAxisName(requiredAxis)} on this face" : "Loop stopped — no edge found";
                return;
            }

            var edgeDirection = _editableTopology.Vertices[startEdge.VertexB].Position - _editableTopology.Vertices[startEdge.VertexA].Position;
            _loopCutAxis = 0;
            if (Mathf.Abs(edgeDirection.y) > Mathf.Abs(edgeDirection.x)) _loopCutAxis = 1;
            if (Mathf.Abs(edgeDirection.z) > Mathf.Abs(edgeDirection[_loopCutAxis])) _loopCutAxis = 2;
            TraverseQuadRing(startEdge, face);
        }

        void TraverseQuadRing(EditableTopologyGraph.Edge startEdge, EditableTopologyGraph.Face startFace)
        {
            var currentEdge = startEdge;
            var currentFace = startFace;
            var visitedFaces = new HashSet<int>();
            for (var step = 0; step < 256; step++)
            {
                if (!visitedFaces.Add(currentFace.Id))
                {
                    _loopRingStopReason = "Loop stopped — topology cycle did not return to the starting edge";
                    return;
                }
                if (currentFace.VertexIds.Length != 4)
                {
                    _loopRingStopReason = $"Loop stopped at a {currentFace.VertexIds.Length}-sided face";
                    _loopRingStops.Add(TopologyEdgeMidpoint(currentEdge));
                    return;
                }
                var edgeIndex = FindFaceEdgeIndex(currentFace, currentEdge);
                if (edgeIndex < 0)
                {
                    _loopRingStopReason = "Loop stopped — broken edge adjacency";
                    return;
                }
                var opposite = FindTopologyEdge(currentFace.VertexIds[(edgeIndex + 2) % 4], currentFace.VertexIds[(edgeIndex + 3) % 4]);
                if (opposite == null)
                {
                    _loopRingStopReason = "Loop stopped — opposite edge is missing";
                    return;
                }
                var entryA = _editableTopology.Vertices[currentEdge.VertexA].Position;
                var entryB = _editableTopology.Vertices[currentEdge.VertexB].Position;
                var oppositeA = _editableTopology.Vertices[opposite.VertexA].Position;
                var oppositeB = _editableTopology.Vertices[opposite.VertexB].Position;
                if ((entryA - oppositeA).sqrMagnitude + (entryB - oppositeB).sqrMagnitude >
                    (entryA - oppositeB).sqrMagnitude + (entryB - oppositeA).sqrMagnitude)
                    (oppositeA, oppositeB) = (oppositeB, oppositeA);
                _loopRingSpans.Add(new LoopRingSpan(currentFace.Id, entryA, entryB, oppositeA, oppositeB));
                if (opposite.Id == startEdge.Id)
                {
                    _loopRingClosed = true;
                    _loopRingStopReason = string.Empty;
                    return;
                }
                if (opposite.FaceIds.Count != 2)
                {
                    _loopRingStopReason = opposite.FaceIds.Count < 2 ? "Loop stopped at an open boundary" : $"Loop stopped at a pole shared by {opposite.FaceIds.Count} faces";
                    _loopRingStops.Add(TopologyEdgeMidpoint(opposite));
                    return;
                }
                var nextFaceId = opposite.FaceIds[0] == currentFace.Id ? opposite.FaceIds[1] : opposite.FaceIds[0];
                var nextFace = _editableTopology.Faces.Find(candidate => candidate.Id == nextFaceId);
                if (nextFace == null)
                {
                    _loopRingStopReason = "Loop stopped — adjacent face is missing";
                    return;
                }
                currentEdge = opposite;
                currentFace = nextFace;
            }
            _loopRingStopReason = "Loop stopped — traversal safety limit reached";
        }

        EditableTopologyGraph.Edge FindTopologyEdge(int vertexA, int vertexB)
        {
            var minimum = Mathf.Min(vertexA, vertexB);
            var maximum = Mathf.Max(vertexA, vertexB);
            return _editableTopology.Edges.Find(edge => edge.VertexA == minimum && edge.VertexB == maximum);
        }

        static int FindFaceEdgeIndex(EditableTopologyGraph.Face face, EditableTopologyGraph.Edge edge)
        {
            for (var i = 0; i < face.VertexIds.Length; i++)
            {
                var a = face.VertexIds[i];
                var b = face.VertexIds[(i + 1) % face.VertexIds.Length];
                if ((a == edge.VertexA && b == edge.VertexB) || (a == edge.VertexB && b == edge.VertexA)) return i;
            }
            return -1;
        }

        Vector3 TopologyEdgeMidpoint(EditableTopologyGraph.Edge edge) =>
            (_editableTopology.Vertices[edge.VertexA].Position + _editableTopology.Vertices[edge.VertexB].Position) * .5f;

        public void AdjustLoopCutSegments(int delta)
        {
            if (_loopCutPhase != LoopCutPhase.Preview) return;
            SaveLoopUndoIfCommitted();
            _loopCutSegments = Mathf.Clamp(_loopCutSegments + delta, 1, 3);
            _loopCutEnabled = true;
            RenderTool();
        }

        public void AdjustLoopCutSlide(float deltaPercent)
        {
            if (_loopCutPhase != LoopCutPhase.Sliding) return;
            SaveLoopUndoIfCommitted();
            SetLoopCutSlideInternal(_loopCutSlidePercent + deltaPercent, true);
            _loopCutEnabled = true;
            RenderTool();
        }

        public void SetLoopCutSlide(float percent)
        {
            if (ActiveTool != Tool.LoopCut || _loopCutPhase != LoopCutPhase.Sliding || float.IsNaN(percent) || float.IsInfinity(percent)) return;
            SaveLoopUndoIfCommitted();
            SetLoopCutSlideInternal(percent, false);
            _loopCutEnabled = true;
            RenderTool();
        }

        public void ToggleLoopCutPrecision() => LoopCutPrecisionEnabled = !LoopCutPrecisionEnabled;

        public void CycleLoopCutSnap()
        {
            LoopCutSnapPercent = LoopCutSnapPercent <= 0f ? 1f : LoopCutSnapPercent < 5f ? 5f : 0f;
        }

        void SetLoopCutSlideInternal(float percent, bool applySnap)
        {
            if (applySnap && LoopCutSnapPercent > 0f) percent = Mathf.Round(percent / LoopCutSnapPercent) * LoopCutSnapPercent;
            _loopCutSlidePercent = Mathf.Clamp(percent, -45f, 45f);
        }

        public void ResetLoopCut()
        {
            if (_loopCutPhase is LoopCutPhase.Sliding or LoopCutPhase.Committed) SaveLoopUndo();
            _loopCutEnabled = false;
            _loopCutPhase = LoopCutPhase.None;
            _loopCutSegments = 1;
            _loopCutSlidePercent = 0f;
            ClearToolDragger();
            RenderTool();
        }

        public void ConfirmLoopCut()
        {
            if (_loopCutPhase == LoopCutPhase.Preview)
            {
                if (!_loopCutValid) return;
                _loopUndo.Push(new LoopCutState(false, LoopCutPhase.None, _loopCutAxis, 1, 0f));
                _loopRedo.Clear();
                _loopCutPhase = LoopCutPhase.Sliding;
                _loopCutSlidePercent = 0f;
                RenderTool();
                CreateToolDragger();
            }
            else if (_loopCutPhase == LoopCutPhase.Sliding)
            {
                _loopCutPhase = LoopCutPhase.Committed;
                ClearToolDragger();
                RenderTool();
            }
        }

        public void CancelLoopCut()
        {
            if (_loopCutPhase == LoopCutPhase.Preview)
            {
                _loopCutEnabled = false;
                _loopCutPhase = LoopCutPhase.None;
            }
            else if (_loopCutPhase == LoopCutPhase.Sliding)
            {
                _loopCutPhase = LoopCutPhase.Preview;
                _loopCutSlidePercent = 0f;
            }
            else return;
            ClearToolDragger();
            RenderTool();
        }

        public void UndoLoopCut() => RestoreLoopHistory(_loopUndo, _loopRedo);
        public void RedoLoopCut() => RestoreLoopHistory(_loopRedo, _loopUndo);

        void SaveLoopUndo()
        {
            _loopUndo.Push(new LoopCutState(_loopCutEnabled, _loopCutPhase, _loopCutAxis, _loopCutSegments, _loopCutSlidePercent));
            _loopRedo.Clear();
        }

        void SaveLoopUndoIfCommitted()
        {
            if (_loopCutPhase is LoopCutPhase.Sliding or LoopCutPhase.Committed) SaveLoopUndo();
        }

        void RestoreLoopHistory(Stack<LoopCutState> source, Stack<LoopCutState> destination)
        {
            if (source.Count == 0 || _filter == null) return;
            destination.Push(new LoopCutState(_loopCutEnabled, _loopCutPhase, _loopCutAxis, _loopCutSegments, _loopCutSlidePercent));
            var state = source.Pop();
            _loopCutEnabled = state.Enabled;
            _loopCutPhase = state.Phase;
            _loopCutAxis = state.Axis;
            _loopCutSegments = state.Segments;
            _loopCutSlidePercent = state.SlidePercent;
            RenderTool();
        }

        Mesh BuildStoredLoopCutTopology()
        {
            var baseMesh = BuildStoredKnifeOnTopology();
            if (!_loopCutEnabled)
            {
                _loopCutValid = false;
                return baseMesh;
            }
            if (_loopCutPhase == LoopCutPhase.Preview)
            {
                _loopCutValid = _loopRingClosed && _loopRingSpans.Count >= 3;
                return baseMesh;
            }
            var faces = new List<PolygonFace>();
            var semanticIds = new List<int>();
            _loopCutValid = _loopRingClosed && _loopRingSpans.Count >= 3;
            foreach (var face in _editableTopology.Faces)
            {
                var polygon = new List<Vector3>(face.VertexIds.Length);
                foreach (var vertexId in face.VertexIds) polygon.Add(_editableTopology.Vertices[vertexId].Position);
                var spanIndex = _loopRingSpans.FindIndex(candidate => candidate.FaceId == face.Id);
                if (spanIndex < 0)
                {
                    faces.Add(new PolygonFace(polygon, face.Normal));
                    semanticIds.Add(face.SemanticId);
                    continue;
                }

                var pieces = new List<List<Vector3>> { polygon };
                var span = _loopRingSpans[spanIndex];
                for (var segment = 0; segment < _loopCutSegments; segment++)
                {
                    var t = Mathf.Clamp((segment + 1f + _loopCutSlidePercent / 100f) / (_loopCutSegments + 1f), .02f, .98f);
                    var firstPoint = Vector3.Lerp(span.EntryA, span.EntryB, t);
                    var secondPoint = Vector3.Lerp(span.OppositeA, span.OppositeB, t);
                    var cutDirection = secondPoint - firstPoint;
                    var dividerNormal = Vector3.Cross(face.Normal, cutDirection).normalized;
                    var nextPieces = new List<List<Vector3>>();
                    var splitOccurred = false;
                    foreach (var piece in pieces)
                    {
                        var first = ClipCoplanarPolygon(piece, firstPoint, dividerNormal, true);
                        var second = ClipCoplanarPolygon(piece, firstPoint, dividerNormal, false);
                        if (first.Count >= 3 && second.Count >= 3)
                        {
                            nextPieces.Add(first);
                            nextPieces.Add(second);
                            splitOccurred = true;
                        }
                        else nextPieces.Add(piece);
                    }
                    if (!splitOccurred) _loopCutValid = false;
                    pieces = nextPieces;
                }
                foreach (var piece in pieces)
                {
                    faces.Add(new PolygonFace(piece, face.Normal));
                    semanticIds.Add(face.SemanticId);
                }
            }
            return CreateExtrudePolygonMesh(faces, semanticIds);
        }

        static List<Vector3> ClipPolygonByAxis(List<Vector3> polygon, int axis, float plane, bool below)
        {
            var result = new List<Vector3>();
            for (var i = 0; i < polygon.Count; i++)
            {
                var current = polygon[i];
                var next = polygon[(i + 1) % polygon.Count];
                var currentInside = below ? current[axis] <= plane + .000001f : current[axis] >= plane - .000001f;
                var nextInside = below ? next[axis] <= plane + .000001f : next[axis] >= plane - .000001f;
                if (currentInside) result.Add(current);
                if (currentInside == nextInside) continue;
                var denominator = next[axis] - current[axis];
                if (Mathf.Abs(denominator) < .000001f) continue;
                result.Add(Vector3.Lerp(current, next, (plane - current[axis]) / denominator));
            }
            return result;
        }

        static Mesh CreateFaceExtrudedBox(Vector3 size, int axis, int sign, float distance)
        {
            var half = size * .5f;
            var faces = CreateBoxFaces(half);
            if (Mathf.Abs(distance) < .000001f) return CreatePolygonMesh(faces);

            var normal = Vector3.zero;
            normal[axis] = sign;
            var selectedFace = faces.Find(face => Vector3.Dot(face.Normal, normal) > .99f);
            if (selectedFace == null) return CreatePolygonMesh(faces);

            // Blender-style face extrusion keeps the original boundary loop,
            // removes the original cap, creates a translated duplicate cap, and
            // connects both loops with four new side faces. This makes extrusion
            // topologically different from simply stretching a cuboid.
            faces.Remove(selectedFace);
            var offset = Vector3.zero;
            offset[axis] = sign * distance;
            var cap = new List<Vector3>(selectedFace.Vertices.Count);
            foreach (var vertex in selectedFace.Vertices) cap.Add(vertex + offset);

            for (var i = 0; i < selectedFace.Vertices.Count; i++)
            {
                var next = (i + 1) % selectedFace.Vertices.Count;
                var edgeDirection = selectedFace.Vertices[next] - selectedFace.Vertices[i];
                var sideNormal = Vector3.Cross(normal, edgeDirection).normalized;
                faces.Add(new PolygonFace(new List<Vector3>
                {
                    selectedFace.Vertices[i], selectedFace.Vertices[next], cap[next], cap[i]
                }, sideNormal));
            }
            faces.Add(new PolygonFace(cap, normal));
            return CreatePolygonMesh(faces);
        }

        static Mesh CreateInsetFaceBox(Vector3 size, int axis, int sign, float insetFraction)
        {
            var faces = CreateBoxFaces(size * .5f);
            var normal = Vector3.zero; normal[axis] = sign;
            var target = faces.Find(face => Vector3.Dot(face.Normal, normal) > .99f);
            faces.Remove(target);
            var center = Vector3.zero;
            foreach (var point in target.Vertices) center += point;
            center /= target.Vertices.Count;
            var inner = new List<Vector3>(target.Vertices.Count);
            foreach (var point in target.Vertices) inner.Add(Vector3.Lerp(point, center, Mathf.Clamp(insetFraction, .001f, .49f)));
            for (var i = 0; i < target.Vertices.Count; i++)
            {
                var next = (i + 1) % target.Vertices.Count;
                faces.Add(new PolygonFace(new List<Vector3> { target.Vertices[i], target.Vertices[next], inner[next], inner[i] }, normal));
            }
            faces.Add(new PolygonFace(inner, normal));
            return CreatePolygonMesh(faces);
        }

        static Mesh CreateKnifeCutBox(Vector3 size, int axis, int sign, float angleDegrees)
        {
            var faces = CreateBoxFaces(size * .5f);
            var normal = Vector3.zero; normal[axis] = sign;
            var target = faces.Find(face => Vector3.Dot(face.Normal, normal) > .99f);
            faces.Remove(target);
            var center = Vector3.zero;
            foreach (var point in target.Vertices) center += point;
            center /= target.Vertices.Count;
            var reference = Mathf.Abs(normal.y) < .9f ? Vector3.up : Vector3.right;
            var tangent = Vector3.Cross(normal, reference).normalized;
            var bitangent = Vector3.Cross(normal, tangent).normalized;
            var radians = angleDegrees * Mathf.Deg2Rad;
            var lineDirection = tangent * Mathf.Cos(radians) + bitangent * Mathf.Sin(radians);
            var dividerNormal = Vector3.Cross(normal, lineDirection).normalized;
            var first = ClipCoplanarPolygon(target.Vertices, center, dividerNormal, true);
            var second = ClipCoplanarPolygon(target.Vertices, center, dividerNormal, false);
            if (first.Count >= 3) faces.Add(new PolygonFace(first, normal));
            if (second.Count >= 3) faces.Add(new PolygonFace(second, normal));
            return CreatePolygonMesh(faces);
        }

        static Mesh CreateLoopCutBox(Vector3 size, float normalizedHeight)
        {
            var faces = CreateBoxFaces(size * .5f);
            var cutY = Mathf.Clamp(normalizedHeight, -.49f, .49f) * size.y;
            var result = new List<PolygonFace>();
            foreach (var face in faces)
            {
                var below = ClipPolygonByY(face.Vertices, cutY, true);
                var above = ClipPolygonByY(face.Vertices, cutY, false);
                if (below.Count >= 3 && above.Count >= 3)
                {
                    result.Add(new PolygonFace(below, face.Normal));
                    result.Add(new PolygonFace(above, face.Normal));
                }
                else result.Add(face);
            }
            return CreatePolygonMesh(result);
        }

        static List<Vector3> ClipCoplanarPolygon(List<Vector3> polygon, Vector3 origin, Vector3 normal, bool positive)
        {
            var result = new List<Vector3>();
            for (var i = 0; i < polygon.Count; i++)
            {
                var current = polygon[i];
                var next = polygon[(i + 1) % polygon.Count];
                var currentDistance = Vector3.Dot(current - origin, normal) * (positive ? 1f : -1f);
                var nextDistance = Vector3.Dot(next - origin, normal) * (positive ? 1f : -1f);
                if (currentDistance >= -.000001f) result.Add(current);
                if ((currentDistance >= 0f) != (nextDistance >= 0f))
                    result.Add(Vector3.Lerp(current, next, currentDistance / (currentDistance - nextDistance)));
            }
            return result;
        }

        static List<Vector3> ClipPolygonByY(List<Vector3> polygon, float y, bool below)
        {
            var result = new List<Vector3>();
            for (var i = 0; i < polygon.Count; i++)
            {
                var current = polygon[i];
                var next = polygon[(i + 1) % polygon.Count];
                var currentDistance = (current.y - y) * (below ? -1f : 1f);
                var nextDistance = (next.y - y) * (below ? -1f : 1f);
                if (currentDistance >= -.000001f) result.Add(current);
                if ((currentDistance >= 0f) != (nextDistance >= 0f))
                    result.Add(Vector3.Lerp(current, next, currentDistance / (currentDistance - nextDistance)));
            }
            return result;
        }

        static Mesh CreatePolygonMesh(List<PolygonFace> faces)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var face in faces) AddFace(vertices, triangles, face.Vertices, face.Normal);
            return CreateMesh(vertices.ToArray(), triangles.ToArray());
        }

        void AddBoxGuide(string name, Vector3 position, Vector3 scale, Color color)
        {
            var guide = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guide.name = name;
            guide.transform.SetParent(_filter.transform, false);
            guide.transform.localPosition = position;
            guide.transform.localScale = scale;
            Destroy(guide.GetComponent<Collider>());
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            guide.GetComponent<MeshRenderer>().material = material;
            _guides.Add(guide);
        }

        void AddKnifeGuide(float angle)
        {
            var guide = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guide.name = "Knife Cut";
            guide.transform.SetParent(_filter.transform, false);
            guide.transform.localPosition = new Vector3(0, .506f, 0);
            guide.transform.localRotation = Quaternion.Euler(0, angle, 0);
            guide.transform.localScale = new Vector3(.025f, .025f, 1.25f);
            Destroy(guide.GetComponent<Collider>());
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(.95f, .18f, .12f));
            guide.GetComponent<MeshRenderer>().material = material;
            _guides.Add(guide);
        }

        void AddLoopGuide(float height)
        {
            for (var axis = 0; axis < 4; axis++)
            {
                var guide = GameObject.CreatePrimitive(PrimitiveType.Cube);
                guide.name = "Loop Cut Edge";
                guide.transform.SetParent(_filter.transform, false);
                guide.transform.localPosition = new Vector3(0, height, 0);
                guide.transform.localScale = axis < 2 ? new Vector3(1.04f, .025f, .025f) : new Vector3(.025f, .025f, 1.04f);
                guide.transform.localRotation = Quaternion.Euler(0, axis < 2 ? 0 : 90, 0);
                if (axis % 2 == 0) guide.transform.localPosition += axis < 2 ? new Vector3(0, 0, .5f) : new Vector3(.5f, 0, 0);
                else guide.transform.localPosition -= axis < 2 ? new Vector3(0, 0, .5f) : new Vector3(.5f, 0, 0);
                Destroy(guide.GetComponent<Collider>());
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", new Color(.2f, .95f, .4f));
                guide.GetComponent<MeshRenderer>().material = material;
                _guides.Add(guide);
            }
        }

        void AddLoopCutPreviewGuides()
        {
            if (!_loopCutEnabled || _editableTopology.Vertices.Count == 0) return;
            var ringColor = _loopRingClosed ? new Color(.15f, .72f, 1f) : new Color(1f, .48f, .12f);
            foreach (var span in _loopRingSpans)
            {
                for (var segment = 0; segment < _loopCutSegments; segment++)
                {
                    var t = Mathf.Clamp((segment + 1f + _loopCutSlidePercent / 100f) / (_loopCutSegments + 1f), .02f, .98f);
                    var start = Vector3.Lerp(span.EntryA, span.EntryB, t);
                    var end = Vector3.Lerp(span.OppositeA, span.OppositeB, t);
                    var direction = end - start;
                    if (direction.sqrMagnitude < .0000001f) continue;
                    var guide = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    guide.name = "Discovered Quad Ring Segment";
                    guide.transform.SetParent(_filter.transform, false);
                    guide.transform.localPosition = (start + end) * .5f;
                    guide.transform.localRotation = Quaternion.FromToRotation(Vector3.right, direction.normalized);
                    guide.transform.localScale = new Vector3(direction.magnitude, .018f, .018f);
                    Destroy(guide.GetComponent<Collider>());
                    var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    material.SetColor("_BaseColor", ringColor);
                    guide.GetComponent<MeshRenderer>().material = material;
                    _guides.Add(guide);
                }
            }
            foreach (var point in _loopRingStops)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "Loop Termination";
                marker.transform.SetParent(_filter.transform, false);
                marker.transform.localPosition = point;
                marker.transform.localScale = Vector3.one * .05f;
                Destroy(marker.GetComponent<Collider>());
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", new Color(1f, .15f, .1f));
                marker.GetComponent<MeshRenderer>().material = material;
                _guides.Add(marker);
            }
        }

        static Mesh CreateBox(float size) => CreateBox(size, size, size);

        static Mesh CreateExtrudedBox(float size, float topHeight)
        {
            // The cube's top face is pulled upward: a simple, readable face-extrude result.
            return CreateBox(size, size + topHeight, size);
        }

        static Mesh CreateBox(float width, float height, float depth)
        {
            var x = width * .5f; var y = height * .5f; var z = depth * .5f;
            var vertices = new[] { new Vector3(-x,-y,-z), new Vector3(x,-y,-z), new Vector3(x,y,-z), new Vector3(-x,y,-z), new Vector3(-x,-y,z), new Vector3(x,-y,z), new Vector3(x,y,z), new Vector3(-x,y,z) };
            return CreateMesh(vertices, new[] { 0,2,1, 0,3,2, 1,2,6, 1,6,5, 5,6,7, 5,7,4, 4,7,3, 4,3,0, 3,7,6, 3,6,2, 4,0,1, 4,1,5 });
        }

        static Mesh CreateSingleEdgeBevelledBox(Vector3 size, Vector3 bevel, CubeEdge edge)
        {
            // A selected cube edge is a five-sided cross-section extruded along
            // that edge. Only its two adjacent faces are offset by the width.
            var half = size * .5f;
            float alongHalf;
            float halfU;
            float halfV;
            float bevelU;
            float bevelV;
            if (edge.Axis == 0) { alongHalf = half.x; halfU = half.y; halfV = half.z; bevelU = bevel.y; bevelV = bevel.z; }
            else if (edge.Axis == 1) { alongHalf = half.y; halfU = half.x; halfV = half.z; bevelU = bevel.x; bevelV = bevel.z; }
            else { alongHalf = half.z; halfU = half.x; halfV = half.y; bevelU = bevel.x; bevelV = bevel.y; }

            bevelU = Mathf.Min(bevelU, halfU - .000001f);
            bevelV = Mathf.Min(bevelV, halfV - .000001f);
            var signU = edge.FirstSign;
            var signV = edge.SecondSign;
            var polygon = new List<Vector2>
            {
                new(-halfU, -halfV), new(halfU, -halfV), new(halfU, halfV), new(-halfU, halfV)
            };
            var corner = new Vector2(signU * halfU, signV * halfV);
            var cornerIndex = polygon.FindIndex(point => point == corner);
            polygon.RemoveAt(cornerIndex);
            polygon.Add(new Vector2(signU * (halfU - bevelU), signV * halfV));
            polygon.Add(new Vector2(signU * halfU, signV * (halfV - bevelV)));
            polygon.Sort((a, b) => Mathf.Atan2(a.y, a.x).CompareTo(Mathf.Atan2(b.y, b.x)));

            Vector3 Point(float along, Vector2 cross)
            {
                return edge.Axis == 0 ? new Vector3(along, cross.x, cross.y) : edge.Axis == 1 ? new Vector3(cross.x, along, cross.y) : new Vector3(cross.x, cross.y, along);
            }

            var vertices = new List<Vector3>(10);
            foreach (var point in polygon) vertices.Add(Point(-alongHalf, point));
            foreach (var point in polygon) vertices.Add(Point(alongHalf, point));
            var triangles = new List<int>();
            void Face(List<int> indices)
            {
                var center = Vector3.zero; foreach (var index in indices) center += vertices[index]; center /= indices.Count;
                for (var i = 1; i < indices.Count - 1; i++)
                {
                    var a = indices[0]; var b = indices[i]; var c = indices[i + 1];
                    if (Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), center) < 0f) (b, c) = (c, b);
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                }
            }
            Face(new List<int> { 0, 1, 2, 3, 4 });
            Face(new List<int> { 5, 6, 7, 8, 9 });
            for (var i = 0; i < 5; i++) Face(new List<int> { i, (i + 1) % 5, (i + 1) % 5 + 5, i + 5 });
            return CreateMesh(vertices.ToArray(), triangles.ToArray());
        }

        sealed class PolygonFace
        {
            public readonly List<Vector3> Vertices;
            public readonly Vector3 Normal;
            public PolygonFace(List<Vector3> vertices, Vector3 normal) { Vertices = vertices; Normal = normal; }
        }

        readonly struct KnifeCutState
        {
            public readonly Vector3 PointA;
            public readonly Vector3 PointB;
            public KnifeCutState(Vector3 pointA, Vector3 pointB) { PointA = pointA; PointB = pointB; }
        }

        readonly struct LoopCutState
        {
            public readonly bool Enabled;
            public readonly LoopCutPhase Phase;
            public readonly int Axis;
            public readonly int Segments;
            public readonly float SlidePercent;
            public LoopCutState(bool enabled, LoopCutPhase phase, int axis, int segments, float slidePercent)
            {
                Enabled = enabled;
                Phase = phase;
                Axis = axis;
                Segments = segments;
                SlidePercent = slidePercent;
            }
        }

        readonly struct LoopRingSpan
        {
            public readonly int FaceId;
            public readonly Vector3 EntryA;
            public readonly Vector3 EntryB;
            public readonly Vector3 OppositeA;
            public readonly Vector3 OppositeB;
            public LoopRingSpan(int faceId, Vector3 entryA, Vector3 entryB, Vector3 oppositeA, Vector3 oppositeB)
            {
                FaceId = faceId;
                EntryA = entryA;
                EntryB = entryB;
                OppositeA = oppositeA;
                OppositeB = oppositeB;
            }
        }

        sealed class EditableTopologyGraph
        {
            public sealed class Vertex
            {
                public int Id;
                public Vector3 Position;
            }

            public sealed class Edge
            {
                public int Id;
                public int VertexA;
                public int VertexB;
                public readonly List<int> FaceIds = new();
            }

            public sealed class Face
            {
                public int Id;
                public int SemanticId;
                public Vector3 Normal;
                public int[] VertexIds;
            }

            public readonly List<Vertex> Vertices = new();
            public readonly List<Edge> Edges = new();
            public readonly List<Face> Faces = new();

            public void Rebuild(List<PolygonFace> polygons, List<int> semanticIds)
            {
                Vertices.Clear();
                Edges.Clear();
                Faces.Clear();

                var unique = new List<Vector3>();
                foreach (var polygon in polygons)
                    foreach (var point in polygon.Vertices)
                        if (FindPosition(unique, point) < 0) unique.Add(point);
                unique.Sort(ComparePosition);
                for (var i = 0; i < unique.Count; i++) Vertices.Add(new Vertex { Id = i, Position = unique[i] });

                var edgeByVertices = new Dictionary<ulong, Edge>();
                var semanticOccurrence = new Dictionary<int, int>();
                for (var polygonIndex = 0; polygonIndex < polygons.Count; polygonIndex++)
                {
                    var polygon = polygons[polygonIndex];
                    var semanticId = semanticIds[polygonIndex];
                    semanticOccurrence.TryGetValue(semanticId, out var occurrence);
                    semanticOccurrence[semanticId] = occurrence + 1;
                    var faceId = semanticId * 1024 + occurrence;
                    var vertexIds = new int[polygon.Vertices.Count];
                    for (var i = 0; i < vertexIds.Length; i++) vertexIds[i] = FindPosition(unique, polygon.Vertices[i]);
                    var face = new Face { Id = faceId, SemanticId = semanticId, Normal = polygon.Normal, VertexIds = vertexIds };
                    Faces.Add(face);

                    for (var i = 0; i < vertexIds.Length; i++)
                    {
                        var a = vertexIds[i];
                        var b = vertexIds[(i + 1) % vertexIds.Length];
                        var minimum = Mathf.Min(a, b);
                        var maximum = Mathf.Max(a, b);
                        var key = ((ulong)(uint)minimum << 32) | (uint)maximum;
                        if (!edgeByVertices.TryGetValue(key, out var edge))
                        {
                            edge = new Edge { Id = edgeByVertices.Count, VertexA = minimum, VertexB = maximum };
                            edgeByVertices.Add(key, edge);
                            Edges.Add(edge);
                        }
                        edge.FaceIds.Add(faceId);
                    }
                }
            }

            static int FindPosition(List<Vector3> positions, Vector3 target)
            {
                for (var i = 0; i < positions.Count; i++)
                    if ((positions[i] - target).sqrMagnitude < .0000000001f) return i;
                return -1;
            }

            static int ComparePosition(Vector3 a, Vector3 b)
            {
                var x = a.x.CompareTo(b.x);
                if (x != 0) return x;
                var y = a.y.CompareTo(b.y);
                return y != 0 ? y : a.z.CompareTo(b.z);
            }
        }

        readonly struct BevelPlane
        {
            public readonly Vector3 Normal;
            public readonly float Distance;
            public BevelPlane(Vector3 normal, float distance) { Normal = normal; Distance = distance; }
        }

        static Mesh CreateMultiEdgeBevelledBox(Vector3 size, Vector3[] widths, CubeEdge[] edges = null)
        {
            // Intersect the base cuboid with one half-space per active original
            // edge. This preserves all stored edge widths in a single manifold mesh.
            edges ??= s_CubeEdges;
            var h = size * .5f;
            var faces = CreateBoxFaces(h);
            for (var i = 0; i < edges.Length; i++)
            {
                if (widths[i].sqrMagnitude <= 0f) continue;
                var plane = CreateBevelPlane(h, widths[i], edges[i]);
                faces = ClipFaces(faces, plane);
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var face in faces) AddFace(vertices, triangles, face.Vertices, face.Normal);
            return CreateMesh(vertices.ToArray(), triangles.ToArray());
        }

        static List<PolygonFace> CreateBoxFaces(Vector3 h) => new()
        {
            new(new List<Vector3> { new(-h.x,-h.y,-h.z), new(-h.x,-h.y,h.z), new(-h.x,h.y,h.z), new(-h.x,h.y,-h.z) }, Vector3.left),
            new(new List<Vector3> { new(h.x,-h.y,h.z), new(h.x,-h.y,-h.z), new(h.x,h.y,-h.z), new(h.x,h.y,h.z) }, Vector3.right),
            new(new List<Vector3> { new(-h.x,-h.y,h.z), new(h.x,-h.y,h.z), new(h.x,-h.y,-h.z), new(-h.x,-h.y,-h.z) }, Vector3.down),
            new(new List<Vector3> { new(-h.x,h.y,-h.z), new(h.x,h.y,-h.z), new(h.x,h.y,h.z), new(-h.x,h.y,h.z) }, Vector3.up),
            new(new List<Vector3> { new(h.x,-h.y,-h.z), new(-h.x,-h.y,-h.z), new(-h.x,h.y,-h.z), new(h.x,h.y,-h.z) }, Vector3.back),
            new(new List<Vector3> { new(-h.x,-h.y,h.z), new(h.x,-h.y,h.z), new(h.x,h.y,h.z), new(-h.x,h.y,h.z) }, Vector3.forward)
        };

        static BevelPlane CreateBevelPlane(Vector3 h, Vector3 width, CubeEdge edge)
        {
            float firstHalf, secondHalf, firstWidth, secondWidth;
            if (edge.Axis == 0) { firstHalf = h.y; secondHalf = h.z; firstWidth = width.y; secondWidth = width.z; }
            else if (edge.Axis == 1) { firstHalf = h.x; secondHalf = h.z; firstWidth = width.x; secondWidth = width.z; }
            else { firstHalf = h.x; secondHalf = h.y; firstWidth = width.x; secondWidth = width.y; }
            firstWidth = Mathf.Max(.000001f, firstWidth);
            secondWidth = Mathf.Max(.000001f, secondWidth);
            var normal = edge.Axis == 0 ? new Vector3(0f, edge.FirstSign / firstWidth, edge.SecondSign / secondWidth)
                : edge.Axis == 1 ? new Vector3(edge.FirstSign / firstWidth, 0f, edge.SecondSign / secondWidth)
                : new Vector3(edge.FirstSign / firstWidth, edge.SecondSign / secondWidth, 0f);
            return new BevelPlane(normal, firstHalf / firstWidth + secondHalf / secondWidth - 1f);
        }

        static List<PolygonFace> ClipFaces(List<PolygonFace> faces, BevelPlane plane)
        {
            const float epsilon = .000001f;
            var result = new List<PolygonFace>();
            var capPoints = new List<Vector3>();
            foreach (var face in faces)
            {
                var clipped = new List<Vector3>();
                for (var i = 0; i < face.Vertices.Count; i++)
                {
                    var current = face.Vertices[i];
                    var next = face.Vertices[(i + 1) % face.Vertices.Count];
                    var currentDistance = Vector3.Dot(plane.Normal, current) - plane.Distance;
                    var nextDistance = Vector3.Dot(plane.Normal, next) - plane.Distance;
                    var currentInside = currentDistance <= epsilon;
                    var nextInside = nextDistance <= epsilon;
                    if (currentInside) clipped.Add(current);
                    if (currentInside != nextInside)
                    {
                        var point = Vector3.Lerp(current, next, currentDistance / (currentDistance - nextDistance));
                        clipped.Add(point);
                        AddUnique(capPoints, point);
                    }
                }
                if (clipped.Count >= 3) result.Add(new PolygonFace(clipped, face.Normal));
            }
            if (capPoints.Count >= 3)
            {
                var center = Vector3.zero; foreach (var point in capPoints) center += point; center /= capPoints.Count;
                var tangent = Vector3.Cross(plane.Normal.normalized, Mathf.Abs(plane.Normal.y) < .9f ? Vector3.up : Vector3.right).normalized;
                var bitangent = Vector3.Cross(plane.Normal.normalized, tangent);
                capPoints.Sort((a, b) => Mathf.Atan2(Vector3.Dot(a - center, bitangent), Vector3.Dot(a - center, tangent)).CompareTo(Mathf.Atan2(Vector3.Dot(b - center, bitangent), Vector3.Dot(b - center, tangent))));
                result.Add(new PolygonFace(capPoints, plane.Normal.normalized));
            }
            return result;
        }

        static void AddUnique(List<Vector3> points, Vector3 point)
        {
            foreach (var existing in points) if ((existing - point).sqrMagnitude < .0000000001f) return;
            points.Add(point);
        }

        static void AddFace(List<Vector3> vertices, List<int> triangles, List<Vector3> polygon, Vector3 normal)
        {
            var offset = vertices.Count;
            vertices.AddRange(polygon);
            for (var i = 1; i < polygon.Count - 1; i++)
            {
                var a = offset; var b = offset + i; var c = offset + i + 1;
                if (Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), normal) < 0f) (b, c) = (c, b);
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
            }
        }

        static Mesh CreateBevelledBox(Vector3 size, Vector3 bevel)
        {
            // A truncated cuboid: six original faces become octagons and each
            // original corner becomes a triangle. It is closed, manifold, and does
            // not change the object's outer dimensions.
            var h = size * .5f;
            bevel = Vector3.Min(bevel, h - Vector3.one * .000001f);
            var inner = h - bevel;
            var vertices = new List<Vector3>(24);
            var xFaces = new List<int>[2] { new(), new() };
            var yFaces = new List<int>[2] { new(), new() };
            var zFaces = new List<int>[2] { new(), new() };
            var corners = new int[2, 2, 2, 3];
            for (var sx = 0; sx < 2; sx++) for (var sy = 0; sy < 2; sy++) for (var sz = 0; sz < 2; sz++)
            {
                var x = sx == 0 ? -1f : 1f; var y = sy == 0 ? -1f : 1f; var z = sz == 0 ? -1f : 1f;
                // One vertex on each edge meeting this original corner. Each
                // original face therefore receives eight boundary vertices.
                corners[sx, sy, sz, 0] = vertices.Count; vertices.Add(new Vector3(x * h.x, y * h.y, z * inner.z)); xFaces[sx].Add(vertices.Count - 1); yFaces[sy].Add(vertices.Count - 1);
                corners[sx, sy, sz, 1] = vertices.Count; vertices.Add(new Vector3(x * h.x, y * inner.y, z * h.z)); xFaces[sx].Add(vertices.Count - 1); zFaces[sz].Add(vertices.Count - 1);
                corners[sx, sy, sz, 2] = vertices.Count; vertices.Add(new Vector3(x * inner.x, y * h.y, z * h.z)); yFaces[sy].Add(vertices.Count - 1); zFaces[sz].Add(vertices.Count - 1);
            }
            var triangles = new List<int>();
            void Face(List<int> indices)
            {
                var center = Vector3.zero; foreach (var index in indices) center += vertices[index]; center /= indices.Count;
                for (var i = 1; i < indices.Count - 1; i++)
                {
                    var a = indices[0]; var b = indices[i]; var c = indices[i + 1];
                    if (Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), center) < 0f) (b, c) = (c, b);
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                }
            }
            void SortFace(List<int> indices, int axis)
            {
                indices.Sort((a, b) =>
                {
                    var pa = vertices[a]; var pb = vertices[b];
                    var aa = axis == 0 ? Mathf.Atan2(pa.z, pa.y) : axis == 1 ? Mathf.Atan2(pa.x, pa.z) : Mathf.Atan2(pa.y, pa.x);
                    var ab = axis == 0 ? Mathf.Atan2(pb.z, pb.y) : axis == 1 ? Mathf.Atan2(pb.x, pb.z) : Mathf.Atan2(pb.y, pb.x);
                    return aa.CompareTo(ab);
                });
                Face(indices);
            }
            SortFace(xFaces[0], 0); SortFace(xFaces[1], 0); SortFace(yFaces[0], 1); SortFace(yFaces[1], 1); SortFace(zFaces[0], 2); SortFace(zFaces[1], 2);
            for (var sx = 0; sx < 2; sx++) for (var sy = 0; sy < 2; sy++) for (var sz = 0; sz < 2; sz++) Face(new List<int> { corners[sx, sy, sz, 0], corners[sx, sy, sz, 1], corners[sx, sy, sz, 2] });
            return CreateMesh(vertices.ToArray(), triangles.ToArray());
        }

        static Mesh CreateMesh(Vector3[] vertices, int[] triangles)
        {
            var mesh = new Mesh { name = "Edited Learning Cube" };
            mesh.vertices = vertices; mesh.triangles = triangles;
            return mesh;
        }
    }
}
