using ARLearning.AR;
using UnityEngine;
using UnityEngine.UIElements;

namespace ARLearning.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class AppToolkitUI : MonoBehaviour
    {
        [SerializeField] ARLearningUI m_Workspace;
        [SerializeField] StyleSheet m_StyleSheet;
        UIDocument _document;
        VisualElement _splash, _routes, _homePage, _simplePage;
        VisualElement _scanPanel, _scanPreview;
        Label _scanStatus;
        LessonQRScanner _qrScanner;
        Button _workspaceHome;
        Button _bevel, _extrude, _inset, _knife, _loopCut;
        Button _home, _scan, _story, _settings;

        void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            var root = _document.rootVisualElement;
            if (_document.visualTreeAsset == null)
            {
                Debug.LogError("AppToolkitUI needs AppShell.uxml assigned to its UIDocument. Run AR Learning > Setup Phase 1.", this);
                enabled = false;
                return;
            }

            if (_document.panelSettings != null)
            {
                _document.panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                _document.panelSettings.referenceResolution = new Vector2Int(1080, 1920);
                _document.panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                _document.panelSettings.match = .5f;
            }

            if (m_StyleSheet != null && !root.styleSheets.Contains(m_StyleSheet)) root.styleSheets.Add(m_StyleSheet);
            // The document fills the display, but its empty background must not
            // consume touches intended for the 3D modeling workspace. Buttons
            // remain pickable because they explicitly use PickingMode.Position.
            root.pickingMode = PickingMode.Ignore;
            var appRoot = root.Q("app-root");
            if (appRoot != null) appRoot.pickingMode = PickingMode.Ignore;
            _splash = root.Q("splash"); _routes = root.Q("route-shell");
            _homePage = root.Q("home-page"); _simplePage = root.Q("simple-page");
            _scanPanel = root.Q("scan-panel"); _scanPreview = root.Q("scan-preview");
            _scanStatus = root.Q<Label>("scan-status");
            _workspaceHome = root.Q<Button>("workspace-home");
            _workspaceHome.pickingMode = PickingMode.Position;

            _bevel = root.Q<Button>("tutorial-bevel");
            _extrude = root.Q<Button>("tutorial-extrude");
            _inset = root.Q<Button>("tutorial-inset");
            _knife = root.Q<Button>("tutorial-knife");
            _loopCut = root.Q<Button>("tutorial-loopcut");
            _home = root.Q<Button>("nav-home");
            _scan = root.Q<Button>("nav-scan");
            _story = root.Q<Button>("nav-story");
            _settings = root.Q<Button>("nav-settings");

            if (_splash == null || _routes == null || _homePage == null || _simplePage == null ||
                _workspaceHome == null || _bevel == null || _extrude == null || _inset == null ||
                _knife == null || _loopCut == null || _home == null || _scan == null ||
                _story == null || _settings == null || _scanPanel == null ||
                _scanPreview == null || _scanStatus == null)
            {
                Debug.LogError("AppShell.uxml is missing one or more required named UI elements.", this);
                enabled = false;
                return;
            }

            _bevel.clicked += OpenBevel;
            _extrude.clicked += OpenExtrude;
            _inset.clicked += OpenInset;
            _knife.clicked += OpenKnife;
            _loopCut.clicked += OpenLoopCut;
            _home.clicked += ShowHome;
            _scan.clicked += ShowScan;
            _story.clicked += ShowStory;
            _settings.clicked += ShowSettings;
            _workspaceHome.clicked += ShowHome;
            if (m_Workspace == null) m_Workspace = GetComponent<ARLearningUI>();
            _qrScanner = GetComponent<LessonQRScanner>();
            if (_qrScanner == null) _qrScanner = gameObject.AddComponent<LessonQRScanner>();
            _qrScanner.CodeRecognized += HandleScannedCode;
            _qrScanner.StatusChanged += SetScanStatus;
            m_Workspace?.SetWorkspaceVisible(false);
            root.schedule.Execute(ShowHome).StartingIn(1400);
        }

        void OnDisable()
        {
            if (_bevel != null) _bevel.clicked -= OpenBevel;
            if (_extrude != null) _extrude.clicked -= OpenExtrude;
            if (_inset != null) _inset.clicked -= OpenInset;
            if (_knife != null) _knife.clicked -= OpenKnife;
            if (_loopCut != null) _loopCut.clicked -= OpenLoopCut;
            if (_home != null) _home.clicked -= ShowHome;
            if (_scan != null) _scan.clicked -= ShowScan;
            if (_story != null) _story.clicked -= ShowStory;
            if (_settings != null) _settings.clicked -= ShowSettings;
            if (_workspaceHome != null) _workspaceHome.clicked -= ShowHome;
            if (_qrScanner != null)
            {
                _qrScanner.CodeRecognized -= HandleScannedCode;
                _qrScanner.StatusChanged -= SetScanStatus;
                _qrScanner.StopScanning();
            }
        }

        void OpenBevel() => OpenTutorial(CubeMeshToolController.Tool.Bevel);
        void OpenExtrude() => OpenTutorial(CubeMeshToolController.Tool.Extrude);
        void OpenInset() => OpenTutorial(CubeMeshToolController.Tool.Inset);
        void OpenKnife() => OpenTutorial(CubeMeshToolController.Tool.Knife);
        void OpenLoopCut() => OpenTutorial(CubeMeshToolController.Tool.LoopCut);
        void ShowScan()
        {
            ShowRoute("Scan", "Place the QR code inside the green frame.", "nav-scan");
            _scanPanel.RemoveFromClassList("hidden");
            _qrScanner.BeginScanning();
        }
        void ShowStory() => ShowRoute("Story", "Read the printed story first, then continue here with its matching modeling activity.", "nav-story");
        void ShowSettings() => ShowRoute("Settings", "Tutorial, accessibility, sound, and interaction preferences will live here.", "nav-settings");

        void ShowHome()
        {
            StopScanning();
            m_Workspace?.SetWorkspaceVisible(false);
            _splash.AddToClassList("hidden"); _routes.RemoveFromClassList("hidden");
            _homePage.RemoveFromClassList("hidden"); _simplePage.AddToClassList("hidden");
            _workspaceHome.AddToClassList("hidden"); SetActiveNav("nav-home");
        }

        void ShowRoute(string title, string copy, string nav)
        {
            StopScanning();
            _homePage.AddToClassList("hidden"); _simplePage.RemoveFromClassList("hidden");
            _simplePage.Q<Label>("simple-title").text = title;
            _simplePage.Q<Label>("simple-copy").text = copy; SetActiveNav(nav);
        }

        void OpenTutorial(CubeMeshToolController.Tool tool)
        {
            StopScanning();
            _routes.AddToClassList("hidden"); _workspaceHome.RemoveFromClassList("hidden");
            m_Workspace?.OpenTutorial(tool);
        }

        void StopScanning()
        {
            _qrScanner?.StopScanning();
            _scanPanel?.AddToClassList("hidden");
            if (_scanPreview != null) _scanPreview.style.backgroundImage = StyleKeyword.None;
        }

        void SetScanStatus(string message)
        {
            if (_scanStatus != null) _scanStatus.text = message;
        }

        void LateUpdate()
        {
            if (_scanPreview != null && _qrScanner != null && _qrScanner.PreviewTexture != null)
                _scanPreview.style.backgroundImage = Background.FromRenderTexture(_qrScanner.PreviewTexture);
        }

        void HandleScannedCode(string payload)
        {
            const string prefix = "arlearning://tutorial/";
            if (!payload.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            {
                SetScanStatus("This is not a MODELAR lesson QR code.");
                _qrScanner.BeginScanning();
                return;
            }

            var lesson = payload.Substring(prefix.Length).Trim('/').ToLowerInvariant();
            switch (lesson)
            {
                case "bevel": OpenTutorial(CubeMeshToolController.Tool.Bevel); break;
                case "extrude": OpenTutorial(CubeMeshToolController.Tool.Extrude); break;
                case "inset": OpenTutorial(CubeMeshToolController.Tool.Inset); break;
                case "knife": OpenTutorial(CubeMeshToolController.Tool.Knife); break;
                case "loop-cut": OpenTutorial(CubeMeshToolController.Tool.LoopCut); break;
                default:
                    SetScanStatus("That MODELAR lesson is not available in this build.");
                    _qrScanner.BeginScanning();
                    break;
            }
        }

        void SetActiveNav(string activeName)
        {
            foreach (var name in new[] { "nav-home", "nav-scan", "nav-story", "nav-settings" })
                _document.rootVisualElement.Q<Button>(name).EnableInClassList("active", name == activeName);
        }
    }
}
