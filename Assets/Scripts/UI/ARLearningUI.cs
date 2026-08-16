using System.Collections;
using System.Globalization;
using ARLearning.AR;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ARLearning.UI
{
    public sealed class ARLearningUI : MonoBehaviour
    {
        [SerializeField] ARLearningStateController m_State;
        [SerializeField] ARPlacementManager m_Placement;
        [SerializeField] ARAvailabilityHandler m_Availability;
        [SerializeField] TransformToolController m_TransformTools;
        [SerializeField] CubeMeshToolController m_MeshTools;
        [SerializeField] LearningObjectCatalog m_ObjectCatalog;
        Text _label;
        Text _modelLabel;
        GameObject _tools;
        GameObject _meshTools;
        GameObject _bevelPanel;
        GameObject _extrudePanel;
        GameObject _insetPanel;
        GameObject _knifePanel;
        GameObject _loopCutPanel;
        GameObject _orientationPanel;
        GameObject _canvasRoot;
        GameObject _modelPanel;
        Button _resetButton;
        Button _edgeSelectionButton;
        Button _faceSelectionButton;
        Button _precisionButton;
        Button _snapButton;
        readonly Button[] _transformToolButtons = new Button[3];
        Button _spinButton;
        InputField _bevelWidthInput;
        InputField _extrudeDistanceInput;
        Button _extrudePrecisionButton;
        Button _extrudeSnapButton;
        InputField _insetPercentInput;
        Button _insetPrecisionButton;
        Button _insetSnapButton;
        Button _knifeSnapButton;
        Button _loopAxisButton;
        Text _loopSegmentsText;
        Text _loopSlideText;
        Text _loopStatusText;
        Button _loopPrecisionButton;
        Button _loopSnapButton;
        InputField _loopSlideInput;
        Button _loopConfirmButton;
        Button _loopCancelButton;
        Text _tutorialLabel;
        Button _retryTutorialButton;
        RectTransform _ghostDragCue;
        RectTransform _canvasRect;
        Coroutine _ghostCueRoutine;
        BevelTutorialStep _bevelTutorialStep;
        ExtrudeTutorialStep _extrudeTutorialStep;
        InsetTutorialStep _insetTutorialStep;
        KnifeTutorialStep _knifeTutorialStep;
        LoopCutTutorialStep _loopCutTutorialStep;
        const int TutorialTargetEdge = 3; // Stable +Y/+Z top-front cube edge.
        const float TutorialTargetWidthMm = 5f;
        const int ExtrudeTargetFaceAxis = 1;
        const int ExtrudeTargetFaceSign = 1;
        const float ExtrudeTutorialTargetMm = 20f;
        const int InsetTutorialTargetFaceId = 3; // Stable +Y Extrude cap.
        const float InsetTutorialTargetPercent = 20f;
        enum BevelTutorialStep { None, SelectEdge, Demonstration, SetWidth, Complete }
        enum ExtrudeTutorialStep { None, SelectFace, Demonstration, SetDistance, Complete }
        enum InsetTutorialStep { None, SelectFace, Demonstration, SetAmount, Complete }
        enum KnifeTutorialStep { None, PlacePointA, Demonstration, PlacePointB, Complete }
        enum LoopCutTutorialStep { None, CreatePreview, Configure, ConfirmCut, Slide, Finish, Complete }
        CanvasScaler _canvasScaler;
        bool _wasLandscape;
        CubeMeshToolController.Tool? _focusedTutorial;
        void Awake()
        {
            EnsureEventSystem();
            var canvas = new GameObject("AR Learning UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasRoot = canvas;
            _canvasRect = canvas.GetComponent<RectTransform>();
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _canvasScaler = canvas.GetComponent<CanvasScaler>();
            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _canvasScaler.matchWidthOrHeight = .5f;
            UpdateCanvasReferenceResolution(true);
            _label = CreateText(canvas.transform, "Status", new Vector2(.5f, 1), new Vector2(0, -90), new Vector2(900, 100), 36);
            _resetButton = CreateButton(canvas.transform); _resetButton.onClick.AddListener(m_Placement.ResetPlacement);
            _tools = CreateToolPanel(canvas.transform);
            _meshTools = CreateMeshToolPanel(canvas.transform);
            _bevelPanel = CreateBevelPanel(canvas.transform);
            _extrudePanel = CreateExtrudePanel(canvas.transform);
            _insetPanel = CreateInsetPanel(canvas.transform);
            _knifePanel = CreateKnifePanel(canvas.transform);
            _loopCutPanel = CreateLoopCutPanel(canvas.transform);
            _orientationPanel = CreateOrientationPanel(canvas.transform);
            _modelPanel = CreateModelPanel(canvas.transform);
            CreateBevelTutorialGuidance(canvas.transform);
        }

        public void SetWorkspaceVisible(bool visible)
        {
            if (_canvasRoot != null) _canvasRoot.SetActive(visible);
            if (!visible)
            {
                if (_ghostCueRoutine != null) StopCoroutine(_ghostCueRoutine);
                _ghostCueRoutine = null;
                if (_ghostDragCue != null) _ghostDragCue.gameObject.SetActive(false);
                _bevelTutorialStep = BevelTutorialStep.None;
                _extrudeTutorialStep = ExtrudeTutorialStep.None;
                _insetTutorialStep = InsetTutorialStep.None;
                _knifeTutorialStep = KnifeTutorialStep.None;
                _loopCutTutorialStep = LoopCutTutorialStep.None;
                m_MeshTools?.ClearBevelTutorialTarget();
                m_MeshTools?.ClearToolTutorialTargetFace();
            }
        }

        public void OpenTutorial(CubeMeshToolController.Tool tool)
        {
            m_MeshTools.ClearToolTutorialTargetFace();
            _focusedTutorial = tool;
            SetWorkspaceVisible(true);
            m_MeshTools.ApplyTool((int)tool);
            if (tool == CubeMeshToolController.Tool.Bevel) StartBevelTutorial();
            else if (tool == CubeMeshToolController.Tool.Extrude) StartExtrudeTutorial();
            else if (tool == CubeMeshToolController.Tool.Inset) StartInsetTutorial();
            else if (tool == CubeMeshToolController.Tool.Knife) StartKnifeTutorial();
            else if (tool == CubeMeshToolController.Tool.LoopCut) StartLoopCutTutorial();
            else
            {
                _bevelTutorialStep = BevelTutorialStep.None;
                _extrudeTutorialStep = ExtrudeTutorialStep.None;
                _insetTutorialStep = InsetTutorialStep.None;
                _knifeTutorialStep = KnifeTutorialStep.None;
                _loopCutTutorialStep = LoopCutTutorialStep.None;
                m_MeshTools.ClearBevelTutorialTarget();
                _retryTutorialButton.gameObject.SetActive(false);
            }
        }
        void Update()
        {
            UpdateCanvasReferenceResolution(false);
            var placed = m_State.Current == ARLearningState.Placed;
            var focusedLesson = _focusedTutorial.HasValue;
            // Object transforms stay available in Bevel lessons so learners can
            // inspect every face and edge before editing.
            _tools.SetActive(placed);
            _meshTools.SetActive(placed && !focusedLesson);
            _modelPanel.SetActive(placed && !focusedLesson);
            _resetButton.gameObject.SetActive(placed && !focusedLesson);
            _bevelPanel.SetActive(placed && m_MeshTools != null && m_MeshTools.ActiveTool == CubeMeshToolController.Tool.Bevel);
            _extrudePanel.SetActive(placed && m_MeshTools != null && m_MeshTools.ActiveTool == CubeMeshToolController.Tool.Extrude);
            _insetPanel.SetActive(placed && m_MeshTools != null && m_MeshTools.ActiveTool == CubeMeshToolController.Tool.Inset);
            _knifePanel.SetActive(placed && m_MeshTools != null && m_MeshTools.ActiveTool == CubeMeshToolController.Tool.Knife);
            _loopCutPanel.SetActive(placed && m_MeshTools != null && m_MeshTools.ActiveTool == CubeMeshToolController.Tool.LoopCut);
            UpdateBevelSelectionButtons();
            UpdateBevelPrecisionControls();
            UpdateExtrudePrecisionControls();
            UpdateInsetPrecisionControls();
            if (_knifeSnapButton != null) _knifeSnapButton.GetComponentInChildren<Text>().text = m_MeshTools.KnifeSnapLabel;
            UpdateLoopCutControls();
            UpdateViewControlButtons();
            UpdateBevelTutorial();
            _orientationPanel.SetActive(placed);
            _label.text = m_State.Current switch
            {
                ARLearningState.PlacementReady => "Tap to place",
                ARLearningState.Placed => m_MeshTools != null && m_MeshTools.HasActiveTool
                    ? m_MeshTools.ActiveTool == CubeMeshToolController.Tool.Bevel
                        ? m_MeshTools.HasBevelEdgeSelection
                            ? m_MeshTools.IsBevelAtMaximum
                                ? $"{m_MeshTools.BevelSelectionStatus} — {m_MeshTools.EffectiveBevelWidthMillimetres:0.0} mm — Maximum for this cube"
                                : $"{m_MeshTools.BevelSelectionStatus} — {m_MeshTools.EffectiveBevelWidthMillimetres:0.0} mm — pull the lever"
                            : m_MeshTools.BevelSelectionStatus
                        : m_MeshTools.ActiveToolInstruction
                    : m_TransformTools == null ? "Object placed" : $"{m_TransformTools.ActiveTool}: select or transform object",
                _ => m_Availability.Status
            };
            if (_modelLabel != null && m_ObjectCatalog != null) _modelLabel.text = m_ObjectCatalog.CurrentName;
        }

        void UpdateCanvasReferenceResolution(bool force)
        {
            if (_canvasScaler == null) return;
            var landscape = Screen.width >= Screen.height;
            if (!force && landscape == _wasLandscape) return;
            _wasLandscape = landscape;
            _canvasScaler.referenceResolution = landscape ? new Vector2(1920f, 1080f) : new Vector2(1080f, 1920f);
        }

        GameObject CreateModelPanel(Transform parent)
        {
            var panel = new GameObject("Model Selector", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, 0); rt.pivot = new Vector2(.5f, 0); rt.anchoredPosition = new Vector2(0, 145); rt.sizeDelta = new Vector2(580, 80);
            var previous = CreateButton(panel.transform, "<", new Vector2(0, 0));
            previous.onClick.AddListener(m_ObjectCatalog.Previous);
            _modelLabel = CreateText(panel.transform, "Current Model", new Vector2(0, 0), new Vector2(290, 40), new Vector2(300, 70), 25);
            var next = CreateButton(panel.transform, ">", new Vector2(460, 0));
            next.onClick.AddListener(m_ObjectCatalog.Next);
            return panel;
        }

        GameObject CreateToolPanel(Transform parent)
        {
            var panel = new GameObject("View Controls", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0); rt.pivot = new Vector2(0, 0); rt.anchoredPosition = new Vector2(36, 36); rt.sizeDelta = new Vector2(570, 138);
            var heading = CreateText(panel.transform, "View Controls Heading", new Vector2(0, 0), new Vector2(285, 116), new Vector2(570, 36), 20);
            heading.text = "VIEW CONTROLS — inspect the model";
            heading.color = new Color(.42f, .92f, .62f, 1f);
            _transformToolButtons[0] = CreateToolButton(panel.transform, "Move", 1, 0);
            _transformToolButtons[1] = CreateToolButton(panel.transform, "Rotate", 2, 135);
            _transformToolButtons[2] = CreateToolButton(panel.transform, "Scale", 3, 270);
            _spinButton = CreateButton(panel.transform, "Spin", new Vector2(405, 0));
            _spinButton.onClick.AddListener(m_TransformTools.ToggleSpin);
            return panel;
        }

        GameObject CreateMeshToolPanel(Transform parent)
        {
            var panel = new GameObject("Mesh Tools", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0); rt.pivot = new Vector2(0, 0); rt.anchoredPosition = new Vector2(36, 142); rt.sizeDelta = new Vector2(720, 190);
            CreateMeshToolButton(panel.transform, "Bevel", 0, new Vector2(0, 96));
            CreateMeshToolButton(panel.transform, "Extrude", 1, new Vector2(135, 96));
            CreateMeshToolButton(panel.transform, "Inset", 2, new Vector2(270, 96));
            CreateMeshToolButton(panel.transform, "Knife", 3, new Vector2(405, 96));
            CreateMeshToolButton(panel.transform, "Loop Cut", 4, new Vector2(540, 96));
            return panel;
        }

        GameObject CreateBevelPanel(Transform parent)
        {
            var panel = new GameObject("Bevel Controls", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0); rt.pivot = Vector2.zero; rt.anchoredPosition = new Vector2(36, 340); rt.sizeDelta = new Vector2(720, 220);
            var heading = CreateText(panel.transform, "Bevel Controls Heading", new Vector2(0, 0), new Vector2(360, 198), new Vector2(720, 36), 20);
            heading.text = "BEVEL CONTROLS — edit selected geometry";
            heading.color = new Color(1f, .72f, .05f, 1f);
            _edgeSelectionButton = CreateButton(panel.transform, "Edge", new Vector2(0, 100));
            _faceSelectionButton = CreateButton(panel.transform, "Face", new Vector2(135, 100));
            ((RectTransform)_edgeSelectionButton.transform).sizeDelta = new Vector2(120, 64);
            ((RectTransform)_faceSelectionButton.transform).sizeDelta = new Vector2(120, 64);
            _edgeSelectionButton.onClick.AddListener(() => m_MeshTools.SetBevelSelectionMode(0));
            _faceSelectionButton.onClick.AddListener(() => m_MeshTools.SetBevelSelectionMode(1));
            _precisionButton = CreateButton(panel.transform, "Precision", new Vector2(270, 100));
            _snapButton = CreateButton(panel.transform, "Snap Off", new Vector2(405, 100));
            ((RectTransform)_precisionButton.transform).sizeDelta = new Vector2(120, 64);
            ((RectTransform)_snapButton.transform).sizeDelta = new Vector2(120, 64);
            _precisionButton.onClick.AddListener(m_MeshTools.ToggleBevelPrecision);
            _snapButton.onClick.AddListener(m_MeshTools.CycleBevelSnap);
            _bevelWidthInput = CreateNumericInput(panel.transform, new Vector2(540, 100), new Vector2(150, 64));
            _bevelWidthInput.onEndEdit.AddListener(ApplyBevelWidthInput);
            var minus = CreateButton(panel.transform, "-1mm", new Vector2(0, 0)); minus.onClick.AddListener(() => m_MeshTools.AdjustSelectedBevel(-1f));
            var plus = CreateButton(panel.transform, "+1mm", new Vector2(135, 0)); plus.onClick.AddListener(() => m_MeshTools.AdjustSelectedBevel(1f));
            var reset = CreateButton(panel.transform, "Reset Selection", new Vector2(270, 0)); reset.onClick.AddListener(m_MeshTools.ResetSelectedBevel);
            var undo = CreateButton(panel.transform, "Undo", new Vector2(405, 0)); undo.onClick.AddListener(m_MeshTools.UndoBevel);
            var redo = CreateButton(panel.transform, "Redo", new Vector2(540, 0)); redo.onClick.AddListener(m_MeshTools.RedoBevel);
            CreateText(panel.transform, "Bevel Guide", new Vector2(.5f, 1), new Vector2(90, -30), new Vector2(420, 44), 22).text = "Choose Edge or Face, then tap the cube";
            return panel;
        }

        GameObject CreateExtrudePanel(Transform parent)
        {
            var panel = new GameObject("Extrude Controls", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(36, 340);
            rt.sizeDelta = new Vector2(720, 245);
            var heading = CreateText(panel.transform, "Extrude Controls Heading", Vector2.zero, new Vector2(360, 223), new Vector2(720, 36), 20);
            heading.text = "EXTRUDE CONTROLS — extend the selected face";
            heading.color = new Color(1f, .72f, .05f, 1f);

            _extrudePrecisionButton = CreateButton(panel.transform, "Precision", new Vector2(0, 50));
            _extrudeSnapButton = CreateButton(panel.transform, "Snap Off", new Vector2(135, 50));
            _extrudePrecisionButton.onClick.AddListener(m_MeshTools.ToggleExtrudePrecision);
            _extrudeSnapButton.onClick.AddListener(m_MeshTools.CycleExtrudeSnap);
            var minus = CreateButton(panel.transform, "-1mm", new Vector2(270, 50));
            var plus = CreateButton(panel.transform, "+1mm", new Vector2(405, 50));
            minus.onClick.AddListener(() => m_MeshTools.AdjustExtrudeDistanceMillimetres(-1f));
            plus.onClick.AddListener(() => m_MeshTools.AdjustExtrudeDistanceMillimetres(1f));
            _extrudeDistanceInput = CreateNumericInput(panel.transform, new Vector2(540, 50), new Vector2(150, 90));
            _extrudeDistanceInput.gameObject.name = "Extrude Distance Input";
            _extrudeDistanceInput.placeholder.GetComponent<Text>().text = "Distance mm";
            _extrudeDistanceInput.onEndEdit.AddListener(ApplyExtrudeDistanceInput);
            var reset = CreateButton(panel.transform, "Reset Face", new Vector2(0, -40));
            var undo = CreateButton(panel.transform, "Undo", new Vector2(135, -40));
            var redo = CreateButton(panel.transform, "Redo", new Vector2(270, -40));
            reset.onClick.AddListener(m_MeshTools.ResetSelectedExtrude);
            undo.onClick.AddListener(m_MeshTools.UndoExtrude);
            redo.onClick.AddListener(m_MeshTools.RedoExtrude);
            CreateText(panel.transform, "Extrude Guide", new Vector2(.5f, 1f), new Vector2(90, -30), new Vector2(520, 44), 22).text = "Tap a face, then pull its orange lever";
            return panel;
        }

        GameObject CreateInsetPanel(Transform parent)
        {
            var panel = new GameObject("Inset Controls", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(36, 340);
            rt.sizeDelta = new Vector2(720, 245);
            var heading = CreateText(panel.transform, "Inset Controls Heading", Vector2.zero, new Vector2(360, 223), new Vector2(720, 36), 20);
            heading.text = "INSET CONTROLS — create an inner face and boundary ring";
            heading.color = new Color(1f, .72f, .05f, 1f);
            _insetPrecisionButton = CreateButton(panel.transform, "Precision", new Vector2(0, 50));
            _insetSnapButton = CreateButton(panel.transform, "Snap Off", new Vector2(135, 50));
            _insetPrecisionButton.onClick.AddListener(m_MeshTools.ToggleInsetPrecision);
            _insetSnapButton.onClick.AddListener(m_MeshTools.CycleInsetSnap);
            var minus = CreateButton(panel.transform, "-1%", new Vector2(270, 50));
            var plus = CreateButton(panel.transform, "+1%", new Vector2(405, 50));
            minus.onClick.AddListener(() => m_MeshTools.AdjustSelectedInsetPercent(-1f));
            plus.onClick.AddListener(() => m_MeshTools.AdjustSelectedInsetPercent(1f));
            _insetPercentInput = CreateNumericInput(panel.transform, new Vector2(540, 50), new Vector2(150, 90));
            _insetPercentInput.gameObject.name = "Inset Percent Input";
            _insetPercentInput.placeholder.GetComponent<Text>().text = "Inset %";
            _insetPercentInput.onEndEdit.AddListener(ApplyInsetPercentInput);
            var reset = CreateButton(panel.transform, "Reset Face", new Vector2(0, -40));
            var undo = CreateButton(panel.transform, "Undo", new Vector2(135, -40));
            var redo = CreateButton(panel.transform, "Redo", new Vector2(270, -40));
            reset.onClick.AddListener(m_MeshTools.ResetSelectedInset);
            undo.onClick.AddListener(m_MeshTools.UndoInset);
            redo.onClick.AddListener(m_MeshTools.RedoInset);
            CreateText(panel.transform, "Inset Guide", new Vector2(.5f, 1f), new Vector2(90, -30), new Vector2(540, 44), 22).text = "Tap any topology face, then pull its diagonal lever";
            return panel;
        }

        GameObject CreateKnifePanel(Transform parent)
        {
            var panel = new GameObject("Knife Controls", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(36, 340);
            rt.sizeDelta = new Vector2(720, 150);
            var heading = CreateText(panel.transform, "Knife Controls Heading", Vector2.zero, new Vector2(360, 128), new Vector2(720, 36), 20);
            heading.text = "KNIFE CONTROLS — place Point A, then Point B";
            heading.color = new Color(1f, .32f, .18f, 1f);
            _knifeSnapButton = CreateButton(panel.transform, "Snap Off", new Vector2(0, 20));
            var reset = CreateButton(panel.transform, "Reset Cut", new Vector2(135, 20));
            var undo = CreateButton(panel.transform, "Undo", new Vector2(270, 20));
            var redo = CreateButton(panel.transform, "Redo", new Vector2(405, 20));
            _knifeSnapButton.onClick.AddListener(m_MeshTools.CycleKnifeSnap);
            reset.onClick.AddListener(m_MeshTools.ResetSelectedKnifeCut);
            undo.onClick.AddListener(m_MeshTools.UndoKnife);
            redo.onClick.AddListener(m_MeshTools.RedoKnife);
            CreateText(panel.transform, "Knife Guide", new Vector2(.5f, 1f), new Vector2(90, -30), new Vector2(560, 44), 22).text = "Both points must be on the same highlighted topology face";
            return panel;
        }

        GameObject CreateLoopCutPanel(Transform parent)
        {
            var panel = new GameObject("Loop Cut Controls", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = Vector2.zero; rt.anchoredPosition = new Vector2(36, 340); rt.sizeDelta = new Vector2(720, 330);
            var heading = CreateText(panel.transform, "Loop Cut Heading", Vector2.zero, new Vector2(360, 308), new Vector2(720, 36), 20);
            heading.text = "LOOP CUT CONTROLS — preview, segment, and slide";
            heading.color = new Color(.25f, 1f, .48f, 1f);
            _loopAxisButton = CreateButton(panel.transform, "Axis Y", new Vector2(0, 50));
            var lessSegments = CreateButton(panel.transform, "- Segment", new Vector2(135, 50));
            var moreSegments = CreateButton(panel.transform, "+ Segment", new Vector2(270, 50));
            var slideBack = CreateButton(panel.transform, "Slide -5%", new Vector2(405, 50));
            var slideForward = CreateButton(panel.transform, "Slide +5%", new Vector2(540, 50));
            _loopAxisButton.onClick.AddListener(m_MeshTools.CycleLoopCutAxis);
            lessSegments.onClick.AddListener(() => m_MeshTools.AdjustLoopCutSegments(-1));
            moreSegments.onClick.AddListener(() => m_MeshTools.AdjustLoopCutSegments(1));
            slideBack.onClick.AddListener(() => m_MeshTools.AdjustLoopCutSlide(-5f));
            slideForward.onClick.AddListener(() => m_MeshTools.AdjustLoopCutSlide(5f));
            _loopPrecisionButton = CreateButton(panel.transform, "Precision", new Vector2(0, -40));
            _loopSnapButton = CreateButton(panel.transform, "Snap Off", new Vector2(135, -40));
            _loopSlideInput = CreateNumericInput(panel.transform, new Vector2(270, -40), new Vector2(150, 90));
            _loopSlideInput.gameObject.name = "Loop Slide Input";
            _loopSlideInput.placeholder.GetComponent<Text>().text = "Slide %";
            _loopSlideInput.onEndEdit.AddListener(ApplyLoopSlideInput);
            var reset = CreateButton(panel.transform, "Reset Loop", new Vector2(435, -40));
            var undo = CreateButton(panel.transform, "Undo", new Vector2(570, -40));
            var redo = CreateButton(panel.transform, "Redo", new Vector2(0, -130));
            _loopConfirmButton = CreateButton(panel.transform, "Confirm Cut", new Vector2(135, -130));
            _loopCancelButton = CreateButton(panel.transform, "Cancel", new Vector2(270, -130));
            _loopPrecisionButton.onClick.AddListener(m_MeshTools.ToggleLoopCutPrecision);
            _loopSnapButton.onClick.AddListener(m_MeshTools.CycleLoopCutSnap);
            reset.onClick.AddListener(m_MeshTools.ResetLoopCut);
            undo.onClick.AddListener(m_MeshTools.UndoLoopCut);
            redo.onClick.AddListener(m_MeshTools.RedoLoopCut);
            _loopConfirmButton.onClick.AddListener(m_MeshTools.ConfirmLoopCut);
            _loopCancelButton.onClick.AddListener(m_MeshTools.CancelLoopCut);
            _loopSegmentsText = CreateText(panel.transform, "Loop Segments", Vector2.zero, new Vector2(480, -118), new Vector2(180, 40), 19);
            _loopSlideText = CreateText(panel.transform, "Loop Slide", Vector2.zero, new Vector2(620, -118), new Vector2(150, 40), 19);
            _loopStatusText = CreateText(panel.transform, "Loop Status", Vector2.zero, new Vector2(420, -158), new Vector2(540, 36), 17);
            return panel;
        }

        void UpdateLoopCutControls()
        {
            if (_loopAxisButton == null || _loopSegmentsText == null || _loopSlideText == null || _loopStatusText == null || _loopPrecisionButton == null || _loopSnapButton == null || _loopSlideInput == null || _loopConfirmButton == null || _loopCancelButton == null || m_MeshTools == null) return;
            _loopAxisButton.GetComponentInChildren<Text>().text = m_MeshTools.LoopCutAxisLabel;
            _loopSegmentsText.text = $"Segments: {m_MeshTools.LoopCutSegments}";
            _loopSlideText.text = $"Slide: {m_MeshTools.LoopCutSlidePercent:+0;-0;0}%";
            _loopStatusText.text = $"{m_MeshTools.LoopCutPhaseLabel} — {m_MeshTools.LoopRingStatus}";
            _loopStatusText.color = m_MeshTools.IsLoopRingClosed ? new Color(.42f, .92f, .62f) : new Color(1f, .55f, .2f);
            var active = new Color(.20f, .72f, .43f, .95f);
            var inactive = new Color(.08f, .35f, .48f, .9f);
            _loopPrecisionButton.GetComponent<Image>().color = m_MeshTools.LoopCutPrecisionEnabled ? active : inactive;
            _loopSnapButton.GetComponent<Image>().color = m_MeshTools.LoopCutSnapPercent > 0f ? active : inactive;
            _loopSnapButton.GetComponentInChildren<Text>().text = m_MeshTools.LoopCutSnapLabel;
            _loopSlideInput.interactable = m_MeshTools.CurrentLoopCutPhase == CubeMeshToolController.LoopCutPhase.Sliding;
            if (!_loopSlideInput.isFocused) _loopSlideInput.text = m_MeshTools.LoopCutSlidePercent.ToString("0.0", CultureInfo.InvariantCulture);
            _loopConfirmButton.GetComponentInChildren<Text>().text = m_MeshTools.LoopCutConfirmLabel;
            _loopConfirmButton.interactable = m_MeshTools.CanConfirmLoopCut;
            _loopCancelButton.interactable = m_MeshTools.CurrentLoopCutPhase is CubeMeshToolController.LoopCutPhase.Preview or CubeMeshToolController.LoopCutPhase.Sliding;
            var color = !m_MeshTools.LoopCutEnabled ? new Color(.08f, .35f, .48f, .9f)
                : m_MeshTools.IsLoopCutValid ? new Color(.20f, .72f, .43f, .95f) : new Color(.75f, .18f, .12f, .95f);
            _loopAxisButton.GetComponent<Image>().color = color;
        }

        void ApplyLoopSlideInput(string value)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) ||
                float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out percent))
                m_MeshTools.SetLoopCutSlide(percent);
            UpdateLoopCutControls();
        }

        void UpdateBevelSelectionButtons()
        {
            if (_edgeSelectionButton == null || _faceSelectionButton == null || m_MeshTools == null) return;
            var edgeActive = m_MeshTools.SelectionMode == CubeMeshToolController.BevelSelectionMode.Edge;
            _edgeSelectionButton.GetComponent<Image>().color = edgeActive ? new Color(.20f, .72f, .43f, .95f) : new Color(.08f, .35f, .48f, .9f);
            _faceSelectionButton.GetComponent<Image>().color = !edgeActive ? new Color(.20f, .72f, .43f, .95f) : new Color(.08f, .35f, .48f, .9f);
        }

        void UpdateBevelPrecisionControls()
        {
            if (_precisionButton == null || _snapButton == null || _bevelWidthInput == null || m_MeshTools == null) return;
            var active = new Color(.20f, .72f, .43f, .95f);
            var inactive = new Color(.08f, .35f, .48f, .9f);
            _precisionButton.GetComponent<Image>().color = m_MeshTools.BevelPrecisionEnabled ? active : inactive;
            _snapButton.GetComponent<Image>().color = m_MeshTools.BevelSnapMillimetres > 0f ? active : inactive;
            _snapButton.GetComponentInChildren<Text>().text = m_MeshTools.BevelSnapLabel;
            _bevelWidthInput.interactable = m_MeshTools.HasBevelEdgeSelection;
            if (!_bevelWidthInput.isFocused)
                _bevelWidthInput.text = m_MeshTools.HasBevelEdgeSelection ? m_MeshTools.EffectiveBevelWidthMillimetres.ToString("0.0", CultureInfo.InvariantCulture) : string.Empty;
        }

        void UpdateExtrudePrecisionControls()
        {
            if (_extrudePrecisionButton == null || _extrudeSnapButton == null || _extrudeDistanceInput == null || m_MeshTools == null) return;
            var active = new Color(.20f, .72f, .43f, .95f);
            var inactive = new Color(.08f, .35f, .48f, .9f);
            _extrudePrecisionButton.GetComponent<Image>().color = m_MeshTools.ExtrudePrecisionEnabled ? active : inactive;
            _extrudeSnapButton.GetComponent<Image>().color = m_MeshTools.ExtrudeSnapMillimetres > 0f ? active : inactive;
            _extrudeSnapButton.GetComponentInChildren<Text>().text = m_MeshTools.ExtrudeSnapLabel;
            _extrudeDistanceInput.interactable = m_MeshTools.ActiveTool == CubeMeshToolController.Tool.Extrude && m_MeshTools.CanManipulateSelectedExtrudeFace;
            if (!_extrudeDistanceInput.isFocused)
                _extrudeDistanceInput.text = m_MeshTools.CanManipulateSelectedExtrudeFace
                    ? m_MeshTools.EffectiveExtrudeDistanceMillimetres.ToString("0.0", CultureInfo.InvariantCulture)
                    : string.Empty;
            _extrudeDistanceInput.textComponent.color = m_MeshTools.IsExtrudeAtLimit ? new Color(1f, .68f, .15f) : Color.white;
        }

        void UpdateInsetPrecisionControls()
        {
            if (_insetPrecisionButton == null || _insetSnapButton == null || _insetPercentInput == null || m_MeshTools == null) return;
            var active = new Color(.20f, .72f, .43f, .95f);
            var inactive = new Color(.08f, .35f, .48f, .9f);
            _insetPrecisionButton.GetComponent<Image>().color = m_MeshTools.InsetPrecisionEnabled ? active : inactive;
            _insetSnapButton.GetComponent<Image>().color = m_MeshTools.InsetSnapPercent > 0f ? active : inactive;
            _insetSnapButton.GetComponentInChildren<Text>().text = m_MeshTools.InsetSnapLabel;
            _insetPercentInput.interactable = m_MeshTools.ActiveTool == CubeMeshToolController.Tool.Inset && m_MeshTools.HasToolFaceSelection;
            if (!_insetPercentInput.isFocused)
                _insetPercentInput.text = m_MeshTools.HasToolFaceSelection
                    ? m_MeshTools.EffectiveInsetPercent.ToString("0.0", CultureInfo.InvariantCulture)
                    : string.Empty;
            _insetPercentInput.textComponent.color = m_MeshTools.IsInsetAtMaximum ? new Color(1f, .68f, .15f) : Color.white;
        }

        void UpdateViewControlButtons()
        {
            if (m_TransformTools == null || _spinButton == null) return;
            var active = new Color(.20f, .72f, .43f, .95f);
            var inactive = new Color(.08f, .35f, .48f, .9f);
            for (var i = 0; i < _transformToolButtons.Length; i++)
                if (_transformToolButtons[i] != null)
                    _transformToolButtons[i].GetComponent<Image>().color = m_TransformTools.ActiveTool == (TransformToolController.Tool)(i + 1) ? active : inactive;
            _spinButton.GetComponent<Image>().color = m_TransformTools.IsSpinning ? active : inactive;
        }

        void ApplyBevelWidthInput(string value)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var width) ||
                float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out width))
                m_MeshTools.SetSelectedBevelWidth(width);
            UpdateBevelPrecisionControls();
        }

        void ApplyExtrudeDistanceInput(string value)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var distance) ||
                float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out distance))
                m_MeshTools.SetExtrudeDistanceMillimetres(distance);
            UpdateExtrudePrecisionControls();
        }

        void ApplyInsetPercentInput(string value)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) ||
                float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out percent))
                m_MeshTools.SetSelectedInsetPercent(percent);
            UpdateInsetPrecisionControls();
        }

        void CreateBevelTutorialGuidance(Transform parent)
        {
            var banner = new GameObject("Bevel Tutorial Guidance", typeof(RectTransform), typeof(Image));
            banner.transform.SetParent(parent, false);
            var rect = (RectTransform)banner.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -145f);
            rect.sizeDelta = new Vector2(980f, 74f);
            var image = banner.GetComponent<Image>();
            image.color = new Color(.08f, .12f, .13f, .88f);
            image.raycastTarget = false;
            _tutorialLabel = CreateText(banner.transform, "Tutorial Instruction", new Vector2(.5f, .5f), Vector2.zero, new Vector2(940f, 66f), 24);

            _retryTutorialButton = CreateButton(parent, "Retry Lesson", Vector2.zero);
            var retryRect = (RectTransform)_retryTutorialButton.transform;
            retryRect.anchorMin = retryRect.anchorMax = new Vector2(.5f, 1f);
            retryRect.pivot = new Vector2(.5f, 1f);
            retryRect.anchoredPosition = new Vector2(0f, -228f);
            retryRect.sizeDelta = new Vector2(180f, 60f);
            _retryTutorialButton.onClick.AddListener(RetryFocusedTutorial);
            _retryTutorialButton.gameObject.SetActive(false);

            var ghost = new GameObject("Ghost Lever Drag Cue", typeof(RectTransform), typeof(Image));
            ghost.transform.SetParent(parent, false);
            _ghostDragCue = (RectTransform)ghost.transform;
            _ghostDragCue.anchorMin = _ghostDragCue.anchorMax = new Vector2(.5f, .5f);
            _ghostDragCue.sizeDelta = new Vector2(78f, 78f);
            var ghostImage = ghost.GetComponent<Image>();
            ghostImage.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            ghostImage.color = new Color(.35f, 1f, .58f, .72f);
            ghostImage.raycastTarget = false;
            CreateText(ghost.transform, "Drag Label", new Vector2(.5f, .5f), Vector2.zero, new Vector2(100f, 40f), 16).text = "DRAG";
            ghost.SetActive(false);
            banner.SetActive(false);
        }

        void StartBevelTutorial()
        {
            _bevelTutorialStep = BevelTutorialStep.SelectEdge;
            _tutorialLabel.transform.parent.gameObject.SetActive(true);
            _retryTutorialButton.gameObject.SetActive(false);
            m_MeshTools.SetBevelSelectionMode(CubeMeshToolController.BevelSelectionMode.Edge);
            m_MeshTools.SetBevelTutorialTargetEdge(TutorialTargetEdge);
            _tutorialLabel.text = "Step 1 — Choose Edge mode, then tap the glowing green top-front edge.";
        }

        void UpdateBevelTutorial()
        {
            if (_tutorialLabel == null) return;
            if (_focusedTutorial == CubeMeshToolController.Tool.Extrude)
            {
                UpdateExtrudeTutorial();
                return;
            }
            if (_focusedTutorial == CubeMeshToolController.Tool.Inset)
            {
                UpdateInsetTutorial();
                return;
            }
            if (_focusedTutorial == CubeMeshToolController.Tool.Knife)
            {
                UpdateKnifeTutorial();
                return;
            }
            if (_focusedTutorial == CubeMeshToolController.Tool.LoopCut)
            {
                UpdateLoopCutTutorial();
                return;
            }
            if (_focusedTutorial.HasValue && _focusedTutorial != CubeMeshToolController.Tool.Bevel)
            {
                _tutorialLabel.transform.parent.gameObject.SetActive(true);
                _retryTutorialButton.gameObject.SetActive(false);
                _tutorialLabel.text = m_MeshTools.HasToolFaceSelection
                    ? _focusedTutorial.Value switch
                    {
                        CubeMeshToolController.Tool.Extrude => "Extrude — pull the orange face-normal lever to extend or push the selected face.",
                        CubeMeshToolController.Tool.Inset => "Inset — pull the diagonal sizing handle to create a smaller face and boundary ring.",
                        CubeMeshToolController.Tool.Knife => "Knife — tap Point A, then tap Point B on the same highlighted face.",
                        CubeMeshToolController.Tool.LoopCut => "Loop Cut — drag vertically to slide the green loop around the cube.",
                        _ => string.Empty
                    }
                    : $"{_focusedTutorial.Value} — tap the cube face you want to edit.";
                return;
            }
            var visible = _focusedTutorial == CubeMeshToolController.Tool.Bevel && _bevelTutorialStep != BevelTutorialStep.None;
            _tutorialLabel.transform.parent.gameObject.SetActive(visible);
            if (!visible) return;

            if (_bevelTutorialStep == BevelTutorialStep.SelectEdge)
            {
                var pulse = .78f + Mathf.Sin(Time.unscaledTime * 6f) * .18f;
                _edgeSelectionButton.GetComponent<Image>().color = new Color(.20f, .72f, .43f, pulse);
                if (!m_MeshTools.HasBevelEdgeSelection) return;
                if (!m_MeshTools.HasSingleBevelEdgeSelection || m_MeshTools.SelectedBevelEdgeIndex != TutorialTargetEdge)
                {
                    _tutorialLabel.text = "Try again — select only the glowing green top-front edge.";
                    return;
                }
                m_MeshTools.ClearBevelTutorialTarget();
                _bevelTutorialStep = BevelTutorialStep.Demonstration;
                _tutorialLabel.text = "Step 2 — Watch how the lever is pulled outward.";
                if (_ghostCueRoutine != null) StopCoroutine(_ghostCueRoutine);
                _ghostCueRoutine = StartCoroutine(AnimateGhostLeverPull());
            }
            else if (_bevelTutorialStep == BevelTutorialStep.SetWidth)
            {
                if (!m_MeshTools.HasSingleBevelEdgeSelection || m_MeshTools.SelectedBevelEdgeIndex != TutorialTargetEdge)
                {
                    _bevelTutorialStep = BevelTutorialStep.SelectEdge;
                    m_MeshTools.SetBevelTutorialTargetEdge(TutorialTargetEdge);
                    _tutorialLabel.text = "Select the glowing green top-front edge again to continue.";
                }
                else if (Mathf.Abs(m_MeshTools.EffectiveBevelWidthMillimetres - TutorialTargetWidthMm) <= .15f)
                {
                    _bevelTutorialStep = BevelTutorialStep.Complete;
                    _tutorialLabel.text = "Complete — Blender Bevel Width is the cut distance from the original edge. You reached 5.0 mm.";
                    _retryTutorialButton.gameObject.SetActive(true);
                }
            }
        }

        IEnumerator AnimateGhostLeverPull()
        {
            _ghostDragCue.gameObject.SetActive(true);
            const float duration = 2.2f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                if (m_MeshTools.TryGetBevelLeverScreenPose(out var endpoint, out var direction) &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, endpoint + direction * Mathf.PingPong(elapsed * 130f, 110f), null, out var local))
                    _ghostDragCue.anchoredPosition = local;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _ghostDragCue.gameObject.SetActive(false);
            _ghostCueRoutine = null;
            _bevelTutorialStep = BevelTutorialStep.SetWidth;
            _tutorialLabel.text = "Step 3 — Pull the lever or enter an exact width of 5.0 mm.";
        }

        void StartExtrudeTutorial()
        {
            _bevelTutorialStep = BevelTutorialStep.None;
            _extrudeTutorialStep = ExtrudeTutorialStep.SelectFace;
            _tutorialLabel.transform.parent.gameObject.SetActive(true);
            _retryTutorialButton.gameObject.SetActive(false);
            m_MeshTools.ShowExtrudeTutorialTargetFace(ExtrudeTargetFaceAxis, ExtrudeTargetFaceSign);
            _tutorialLabel.text = "Step 1 — Tap the cube's top face. This lesson will extrude it outward.";
        }

        void UpdateExtrudeTutorial()
        {
            var visible = _focusedTutorial == CubeMeshToolController.Tool.Extrude && _extrudeTutorialStep != ExtrudeTutorialStep.None;
            _tutorialLabel.transform.parent.gameObject.SetActive(visible);
            if (!visible) return;

            if (_extrudeTutorialStep == ExtrudeTutorialStep.SelectFace)
            {
                if (!m_MeshTools.HasToolFaceSelection) return;
                if (m_MeshTools.SelectedToolFaceAxis != ExtrudeTargetFaceAxis || m_MeshTools.SelectedToolFaceSign != ExtrudeTargetFaceSign)
                {
                    _tutorialLabel.text = "Try again — rotate the view if needed, then tap the cube's top face.";
                    return;
                }
                m_MeshTools.ClearToolTutorialTargetFace();
                _extrudeTutorialStep = ExtrudeTutorialStep.Demonstration;
                _tutorialLabel.text = "Step 2 — Watch the face-normal lever move outward.";
                if (_ghostCueRoutine != null) StopCoroutine(_ghostCueRoutine);
                _ghostCueRoutine = StartCoroutine(AnimateGhostExtrudePull());
            }
            else if (_extrudeTutorialStep == ExtrudeTutorialStep.SetDistance)
            {
                if (!m_MeshTools.HasToolFaceSelection || m_MeshTools.SelectedToolFaceAxis != ExtrudeTargetFaceAxis || m_MeshTools.SelectedToolFaceSign != ExtrudeTargetFaceSign)
                {
                    _extrudeTutorialStep = ExtrudeTutorialStep.SelectFace;
                    _tutorialLabel.text = "Select the top face again to continue.";
                }
                else if (Mathf.Abs(m_MeshTools.EffectiveExtrudeDistanceMillimetres - ExtrudeTutorialTargetMm) <= .15f)
                {
                    _extrudeTutorialStep = ExtrudeTutorialStep.Complete;
                    _tutorialLabel.text = "Complete — Extrude duplicates the selected face as a cap and connects it with four new side faces. You extruded outward by +20.0 mm.";
                    _retryTutorialButton.gameObject.SetActive(true);
                }
            }
        }

        IEnumerator AnimateGhostExtrudePull()
        {
            _ghostDragCue.gameObject.SetActive(true);
            const float duration = 2.2f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                if (m_MeshTools.TryGetToolLeverScreenPose(out var endpoint, out var direction) &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, endpoint + direction * Mathf.PingPong(elapsed * 130f, 110f), null, out var local))
                    _ghostDragCue.anchoredPosition = local;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _ghostDragCue.gameObject.SetActive(false);
            _ghostCueRoutine = null;
            _extrudeTutorialStep = ExtrudeTutorialStep.SetDistance;
            _tutorialLabel.text = "Step 3 — Pull outward or enter exactly +20.0 mm. Negative values push inward.";
        }

        void StartInsetTutorial()
        {
            _bevelTutorialStep = BevelTutorialStep.None;
            _extrudeTutorialStep = ExtrudeTutorialStep.None;
            _insetTutorialStep = InsetTutorialStep.SelectFace;
            _tutorialLabel.transform.parent.gameObject.SetActive(true);
            _retryTutorialButton.gameObject.SetActive(false);
            m_MeshTools.EnsureExtrudedFaceForInsetTutorial(ExtrudeTargetFaceAxis, ExtrudeTargetFaceSign, ExtrudeTutorialTargetMm);
            m_MeshTools.ShowExtrudeTutorialTargetFace(ExtrudeTargetFaceAxis, ExtrudeTargetFaceSign);
            _tutorialLabel.text = "Step 1 — Tap the glowing green top Extrude cap.";
        }

        void UpdateInsetTutorial()
        {
            var visible = _focusedTutorial == CubeMeshToolController.Tool.Inset && _insetTutorialStep != InsetTutorialStep.None;
            _tutorialLabel.transform.parent.gameObject.SetActive(visible);
            if (!visible) return;
            if (_insetTutorialStep == InsetTutorialStep.SelectFace)
            {
                if (!m_MeshTools.HasToolFaceSelection) return;
                if (m_MeshTools.SelectedToolTopologyFaceId != InsetTutorialTargetFaceId)
                {
                    _tutorialLabel.text = "Try again — select the glowing green top cap, not a generated side.";
                    return;
                }
                m_MeshTools.ClearToolTutorialTargetFace();
                _insetTutorialStep = InsetTutorialStep.Demonstration;
                _tutorialLabel.text = "Step 2 — Watch the diagonal lever create an inner face.";
                if (_ghostCueRoutine != null) StopCoroutine(_ghostCueRoutine);
                _ghostCueRoutine = StartCoroutine(AnimateGhostInsetPull());
            }
            else if (_insetTutorialStep == InsetTutorialStep.SetAmount)
            {
                if (!m_MeshTools.HasToolFaceSelection || m_MeshTools.SelectedToolTopologyFaceId != InsetTutorialTargetFaceId)
                {
                    _insetTutorialStep = InsetTutorialStep.SelectFace;
                    m_MeshTools.ShowExtrudeTutorialTargetFace(ExtrudeTargetFaceAxis, ExtrudeTargetFaceSign);
                    _tutorialLabel.text = "Select the glowing top cap again to continue.";
                }
                else if (Mathf.Abs(m_MeshTools.EffectiveInsetPercent - InsetTutorialTargetPercent) <= .15f)
                {
                    _insetTutorialStep = InsetTutorialStep.Complete;
                    _tutorialLabel.text = "Complete — Inset creates a smaller inner face and a surrounding boundary ring on the selected cap. You reached 20.0%.";
                    _retryTutorialButton.gameObject.SetActive(true);
                }
            }
        }

        IEnumerator AnimateGhostInsetPull()
        {
            _ghostDragCue.gameObject.SetActive(true);
            const float duration = 2.2f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                if (m_MeshTools.TryGetToolLeverScreenPose(out var endpoint, out var direction) &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, endpoint + direction * Mathf.PingPong(elapsed * 120f, 95f), null, out var local))
                    _ghostDragCue.anchoredPosition = local;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _ghostDragCue.gameObject.SetActive(false);
            _ghostCueRoutine = null;
            _insetTutorialStep = InsetTutorialStep.SetAmount;
            _tutorialLabel.text = "Step 3 — Pull the diagonal lever or enter exactly 20.0%.";
        }

        void StartKnifeTutorial()
        {
            _bevelTutorialStep = BevelTutorialStep.None;
            _extrudeTutorialStep = ExtrudeTutorialStep.None;
            _insetTutorialStep = InsetTutorialStep.None;
            _knifeTutorialStep = KnifeTutorialStep.PlacePointA;
            _tutorialLabel.transform.parent.gameObject.SetActive(true);
            _retryTutorialButton.gameObject.SetActive(false);
            m_MeshTools.EnsureExtrudedFaceForInsetTutorial(ExtrudeTargetFaceAxis, ExtrudeTargetFaceSign, ExtrudeTutorialTargetMm);
            m_MeshTools.CancelPendingKnifePoint();
            m_MeshTools.ShowKnifeTutorialTargets(ExtrudeTargetFaceAxis, ExtrudeTargetFaceSign);
            _tutorialLabel.text = "Step 1 — Tap the left green boundary marker to place Knife Point A.";
        }

        void UpdateKnifeTutorial()
        {
            var visible = _focusedTutorial == CubeMeshToolController.Tool.Knife && _knifeTutorialStep != KnifeTutorialStep.None;
            _tutorialLabel.transform.parent.gameObject.SetActive(visible);
            if (!visible) return;
            if (_knifeTutorialStep == KnifeTutorialStep.PlacePointA)
            {
                if (!m_MeshTools.HasKnifePointA) return;
                if (!m_MeshTools.KnifePendingPointMatchesTutorialTarget(ExtrudeTargetFaceAxis, ExtrudeTargetFaceSign))
                {
                    m_MeshTools.CancelPendingKnifePoint();
                    _tutorialLabel.text = "Try again — tap the left green marker on the top cap.";
                    return;
                }
                _knifeTutorialStep = KnifeTutorialStep.Demonstration;
                _tutorialLabel.text = "Step 2 — Watch the cut direction from Point A to Point B.";
                if (_ghostCueRoutine != null) StopCoroutine(_ghostCueRoutine);
                _ghostCueRoutine = StartCoroutine(AnimateGhostKnifeCut());
            }
            else if (_knifeTutorialStep == KnifeTutorialStep.PlacePointB && m_MeshTools.HasKnifePointA &&
                     !m_MeshTools.KnifePendingPointMatchesTutorialTarget(ExtrudeTargetFaceAxis, ExtrudeTargetFaceSign))
            {
                m_MeshTools.CancelPendingKnifePoint();
                _knifeTutorialStep = KnifeTutorialStep.PlacePointA;
                _tutorialLabel.text = "The cut changed faces. Start again at the left green marker.";
            }
            else if (_knifeTutorialStep == KnifeTutorialStep.PlacePointB && !m_MeshTools.HasKnifePointA)
            {
                if (m_MeshTools.KnifeCutMatchesTutorialTargets(ExtrudeTargetFaceAxis, ExtrudeTargetFaceSign))
                {
                    _knifeTutorialStep = KnifeTutorialStep.Complete;
                    m_MeshTools.ClearToolTutorialTargetFace();
                    _tutorialLabel.text = "Complete — Knife inserted new boundary vertices and divided the selected cap into two faces along your A-to-B line.";
                    _retryTutorialButton.gameObject.SetActive(true);
                }
                else
                {
                    m_MeshTools.ResetSelectedKnifeCut();
                    m_MeshTools.ShowKnifeTutorialTargets(ExtrudeTargetFaceAxis, ExtrudeTargetFaceSign);
                    _knifeTutorialStep = KnifeTutorialStep.PlacePointA;
                    _tutorialLabel.text = "Try again — place Point A and Point B on the two green boundary markers.";
                }
            }
        }

        IEnumerator AnimateGhostKnifeCut()
        {
            _ghostDragCue.gameObject.SetActive(true);
            const float duration = 2f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                if (m_MeshTools.TryGetKnifeTutorialScreenTargets(ExtrudeTargetFaceAxis, ExtrudeTargetFaceSign, out var first, out var second) &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, Vector2.Lerp(first, second, Mathf.Clamp01(elapsed / duration)), null, out var local))
                    _ghostDragCue.anchoredPosition = local;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _ghostDragCue.gameObject.SetActive(false);
            _ghostCueRoutine = null;
            _knifeTutorialStep = KnifeTutorialStep.PlacePointB;
            _tutorialLabel.text = "Step 3 — Tap the right green boundary marker to place Point B and commit the cut.";
        }

        void StartLoopCutTutorial()
        {
            _bevelTutorialStep = BevelTutorialStep.None;
            _extrudeTutorialStep = ExtrudeTutorialStep.None;
            _insetTutorialStep = InsetTutorialStep.None;
            _knifeTutorialStep = KnifeTutorialStep.None;
            _loopCutTutorialStep = LoopCutTutorialStep.CreatePreview;
            _tutorialLabel.transform.parent.gameObject.SetActive(true);
            _retryTutorialButton.gameObject.SetActive(false);
            m_MeshTools.ResetLoopCut();
            _tutorialLabel.text = "Step 1 — Tap near an edge to discover a Loop Cut ring through connected quad faces.";
        }

        void UpdateLoopCutTutorial()
        {
            var visible = _focusedTutorial == CubeMeshToolController.Tool.LoopCut && _loopCutTutorialStep != LoopCutTutorialStep.None;
            _tutorialLabel.transform.parent.gameObject.SetActive(visible);
            if (!visible) return;
            if (_loopCutTutorialStep == LoopCutTutorialStep.CreatePreview)
            {
                if (!m_MeshTools.LoopCutEnabled) return;
                if (!m_MeshTools.IsLoopCutValid)
                {
                    _tutorialLabel.text = "This axis has no valid closed intersection. Tap Axis to try another direction.";
                    return;
                }
                _loopCutTutorialStep = LoopCutTutorialStep.Configure;
                _tutorialLabel.text = "Step 2 — Set Axis Y, use 2 segments, and keep Slide at 0%. Green means the loop is valid.";
            }
            else if (_loopCutTutorialStep == LoopCutTutorialStep.Configure)
            {
                if (!m_MeshTools.IsLoopCutValid)
                {
                    _tutorialLabel.text = "Choose an axis with a green valid-loop preview.";
                    return;
                }
                if (m_MeshTools.LoopCutAxis == 1 && m_MeshTools.LoopCutSegments == 2 && Mathf.Abs(m_MeshTools.LoopCutSlidePercent) < .01f)
                {
                    _loopCutTutorialStep = LoopCutTutorialStep.ConfirmCut;
                    _tutorialLabel.text = "Step 3 — This is still a temporary preview. Tap Confirm Cut to create the rings and enter Slide mode.";
                }
            }
            else if (_loopCutTutorialStep == LoopCutTutorialStep.ConfirmCut && m_MeshTools.CurrentLoopCutPhase == CubeMeshToolController.LoopCutPhase.Sliding)
            {
                _loopCutTutorialStep = LoopCutTutorialStep.Slide;
                _tutorialLabel.text = "Step 4 — Pull the blue slide lever or enter +10% to move both rings through their quads.";
            }
            else if (_loopCutTutorialStep == LoopCutTutorialStep.Slide && Mathf.Abs(m_MeshTools.LoopCutSlidePercent - 10f) <= .15f)
            {
                _loopCutTutorialStep = LoopCutTutorialStep.Finish;
                _tutorialLabel.text = "Step 5 — Tap Finish Slide to commit the final topology.";
            }
            else if (_loopCutTutorialStep == LoopCutTutorialStep.Finish && m_MeshTools.CurrentLoopCutPhase == CubeMeshToolController.LoopCutPhase.Committed)
            {
                _loopCutTutorialStep = LoopCutTutorialStep.Complete;
                _tutorialLabel.text = "Complete — you previewed a quad ring, confirmed its topology, slid it to +10%, and committed it. Cancel removes an uncommitted preview.";
                _retryTutorialButton.gameObject.SetActive(true);
            }
        }

        void RetryFocusedTutorial()
        {
            if (_focusedTutorial == CubeMeshToolController.Tool.Extrude)
            {
                m_MeshTools.ResetSelectedExtrude();
                m_MeshTools.DeselectToolFace();
                StartExtrudeTutorial();
            }
            else if (_focusedTutorial == CubeMeshToolController.Tool.Inset)
            {
                m_MeshTools.ResetSelectedInset();
                m_MeshTools.DeselectToolFace();
                StartInsetTutorial();
            }
            else if (_focusedTutorial == CubeMeshToolController.Tool.Knife)
            {
                m_MeshTools.ResetSelectedKnifeCut();
                m_MeshTools.DeselectToolFace();
                StartKnifeTutorial();
            }
            else if (_focusedTutorial == CubeMeshToolController.Tool.LoopCut)
            {
                m_MeshTools.ResetLoopCut();
                m_MeshTools.DeselectToolFace();
                StartLoopCutTutorial();
            }
            else RetryBevelTutorial();
        }

        void RetryBevelTutorial()
        {
            m_MeshTools.ResetBevelEdgeForTutorial(TutorialTargetEdge);
            m_MeshTools.DeselectBevel();
            StartBevelTutorial();
        }

        GameObject CreateOrientationPanel(Transform parent)
        {
            var panel = new GameObject("View Shortcuts", typeof(RectTransform)); panel.transform.SetParent(parent, false);
            var rt = (RectTransform)panel.transform; rt.anchorMin = rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1); rt.anchoredPosition = new Vector2(-28, -180); rt.sizeDelta = new Vector2(300, 230);
            var names = new[] { "Top", "Bottom", "Front", "Back", "Left", "Right" };
            for (var i = 0; i < names.Length; i++)
            {
                var button = CreateButton(panel.transform, names[i], new Vector2((i % 3) * 100, 160 - (i / 3) * 72));
                ((RectTransform)button.transform).sizeDelta = new Vector2(94, 64);
                var view = names[i]; button.onClick.AddListener(() => m_TransformTools.SetOrientation(view));
            }
            var resetView = CreateButton(panel.transform, "Reset View", new Vector2(75, 16));
            ((RectTransform)resetView.transform).sizeDelta = new Vector2(150, 64);
            resetView.onClick.AddListener(m_TransformTools.ResetView);
            return panel;
        }

        void CreateMeshToolButton(Transform parent, string label, int tool, Vector2 position)
        {
            var button = CreateButton(parent, label, position);
            button.onClick.AddListener(() => m_MeshTools.ApplyTool(tool));
        }

        Button CreateToolButton(Transform parent, string label, int tool, float x)
        {
            var button = CreateButton(parent, label, new Vector2(x, 0));
            button.onClick.AddListener(() => m_TransformTools.ToggleTool(tool));
            return button;
        }
        static Text CreateText(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = anchor; rt.anchoredPosition = position; rt.sizeDelta = size;
            var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false; return text;
        }
        static Button CreateButton(Transform parent)
        {
            var go = new GameObject("Reset Button", typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(1, 0); rt.anchoredPosition = new Vector2(-36, 36); rt.sizeDelta = new Vector2(210, 90);
            go.GetComponent<Image>().color = new Color(.08f, .35f, .48f, .9f);
            CreateText(go.transform, "Label", new Vector2(.5f, .5f), Vector2.zero, new Vector2(200, 80), 28).text = "Reset";
            return go.GetComponent<Button>();
        }
        static Button CreateButton(Transform parent, string label, Vector2 position)
        {
            var go = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = Vector2.zero; rt.anchoredPosition = position; rt.sizeDelta = new Vector2(120, 90);
            go.GetComponent<Image>().color = new Color(.08f, .35f, .48f, .9f);
            CreateText(go.transform, "Label", new Vector2(.5f, .5f), Vector2.zero, new Vector2(116, 80), 23).text = label;
            return go.GetComponent<Button>();
        }

        static InputField CreateNumericInput(Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject("Bevel Width Input", typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(.12f, .16f, .17f, .95f);

            var valueText = CreateText(go.transform, "Value", new Vector2(.5f, .5f), Vector2.zero, size - new Vector2(16f, 8f), 22);
            valueText.alignment = TextAnchor.MiddleCenter;
            var placeholder = CreateText(go.transform, "Placeholder", new Vector2(.5f, .5f), Vector2.zero, size - new Vector2(16f, 8f), 20);
            placeholder.text = "Width mm";
            placeholder.color = new Color(.65f, .7f, .68f, 1f);

            var input = go.GetComponent<InputField>();
            input.textComponent = valueText;
            input.placeholder = placeholder;
            input.contentType = InputField.ContentType.DecimalNumber;
            input.keyboardType = TouchScreenKeyboardType.DecimalPad;
            input.characterLimit = 8;
            return input;
        }
        static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Object.DontDestroyOnLoad(go);
        }
    }
}
