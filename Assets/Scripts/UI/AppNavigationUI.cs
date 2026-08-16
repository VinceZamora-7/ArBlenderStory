using ARLearning.AR;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ARLearning.UI
{
    /// <summary>Non-AR application shell: splash, home, and the four tutorial routes.</summary>
    public sealed class AppNavigationUI : MonoBehaviour
    {
        static readonly Color Background = new(.12f, .13f, .14f, 1f);
        static readonly Color Surface = new(.20f, .22f, .23f, 1f);
        static readonly Color Green = new(.20f, .72f, .43f, 1f);
        [SerializeField] ARLearningUI m_Workspace;
        GameObject _splash, _content, _homeButton;
        Image _background;

        void Start()
        {
            EnsureEventSystem();
            CreateShell();
            if (m_Workspace == null) m_Workspace = GetComponent<ARLearningUI>();
            if (m_Workspace != null) m_Workspace.SetWorkspaceVisible(false);
            Invoke(nameof(ShowHome), 1.4f);
        }

        void CreateShell()
        {
            var canvas = new GameObject("App Navigation", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920);
            var background = ImageBox(canvas.transform, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Background);
            _background = background.GetComponent<Image>(); _background.raycastTarget = false;
            _splash = new GameObject("Splash", typeof(RectTransform)); _splash.transform.SetParent(background.transform, false); Stretch(_splash.GetComponent<RectTransform>());
            TextBox(_splash.transform, "MODELAR", new Vector2(.5f,.58f), new Vector2(0,0), new Vector2(900,110), 72, Green);
            TextBox(_splash.transform, "Interactive Blender Learning", new Vector2(.5f,.49f), Vector2.zero, new Vector2(900,70), 30, Color.white);
            _content = new GameObject("Route Content", typeof(RectTransform)); _content.transform.SetParent(background.transform, false); Stretch(_content.GetComponent<RectTransform>());
            _content.SetActive(false);
            _homeButton = ButtonBox(background.transform, "← Home", new Vector2(36, 36), new Vector2(180,72));
            _homeButton.GetComponent<Button>().onClick.AddListener(ShowHome); _homeButton.SetActive(false);
        }

        public void ShowHome()
        {
            _background.color = Background;
            _splash.SetActive(false); _content.SetActive(true); _homeButton.SetActive(false);
            ClearContent();
            TextBox(_content.transform, "MODELAR", new Vector2(.5f,1), new Vector2(0,-105), new Vector2(900,80), 48, Color.white);
            TextBox(_content.transform, "Learn Blender tools by doing", new Vector2(.5f,1), new Vector2(0,-165), new Vector2(900,50), 26, Green);
            TextBox(_content.transform, "INTERACTIVE TUTORIALS", new Vector2(.5f,1), new Vector2(0,-230), new Vector2(850,40), 20, new Color(.72f,.75f,.74f));
            CreateTutorial("Bevel", CubeMeshToolController.Tool.Bevel, 0);
            CreateTutorial("Extrude", CubeMeshToolController.Tool.Extrude, 1);
            CreateTutorial("Inset", CubeMeshToolController.Tool.Inset, 2);
            CreateTutorial("Knife", CubeMeshToolController.Tool.Knife, 3);
            CreateTutorial("Loop Cut", CubeMeshToolController.Tool.LoopCut, 4);
            CreateNav();
        }

        void CreateTutorial(string title, CubeMeshToolController.Tool tool, int index)
        {
            var card = ButtonBox(_content.transform, title, new Vector2(60, -285 - index * 145), new Vector2(960,120));
            var rect = (RectTransform)card.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            card.GetComponent<Image>().color = index == 0 ? new Color(.16f,.40f,.27f,1f) : Surface;
            var subtitle = title == "Bevel" ? "EDGE SELECT • PRECISION WIDTH" : "Interactive practice lesson";
            var detail = TextBox(card.transform, "Detail", new Vector2(.5f,.5f), new Vector2(0,-28), new Vector2(850,34), 18, new Color(.82f,.86f,.84f));
            detail.text = subtitle;
            card.GetComponent<Button>().onClick.AddListener(() => OpenTutorial(tool));
        }

        void CreateNav()
        {
            var names = new[] { "Home", "Scan", "Story", "Settings" };
            for (var i=0; i<names.Length; i++)
            {
                var button = ButtonBox(_content.transform, names[i], new Vector2(30 + i*260, 36), new Vector2(240,78));
                ((RectTransform)button.transform).pivot = Vector2.zero;
                if (i == 0) button.GetComponent<Button>().onClick.AddListener(ShowHome);
                else if (i == 1) button.GetComponent<Button>().onClick.AddListener(ShowScan);
                else if (i == 2) button.GetComponent<Button>().onClick.AddListener(ShowStory);
                else button.GetComponent<Button>().onClick.AddListener(ShowSettings);
            }
        }

        void ShowScan() => ShowSimpleRoute("Scan", "Scan a lesson QR code to open its interactive tutorial.");
        void ShowStory() => ShowSimpleRoute("Story", "Read the hardcopy lesson, then use Scan to continue in the app.");
        void ShowSettings() => ShowSimpleRoute("Settings", "Tutorial preferences and accessibility controls will appear here.");
        void ShowSimpleRoute(string title, string message)
        {
            _background.color = Background;
            _splash.SetActive(false); _content.SetActive(true); _homeButton.SetActive(false); ClearContent();
            TextBox(_content.transform, title, new Vector2(.5f,.68f), Vector2.zero, new Vector2(900,90), 52, Green);
            TextBox(_content.transform, message, new Vector2(.5f,.53f), Vector2.zero, new Vector2(850,140), 30, Color.white);
            CreateNav();
        }

        void OpenTutorial(CubeMeshToolController.Tool tool)
        {
            _background.color = Color.clear;
            _content.SetActive(false); _homeButton.SetActive(true);
            if (m_Workspace != null) m_Workspace.OpenTutorial(tool);
        }

        void ClearContent()
        {
            for (var i = _content.transform.childCount - 1; i >= 0; i--) Destroy(_content.transform.GetChild(i).gameObject);
        }

        static GameObject ImageBox(Transform parent, string name, Vector2 min, Vector2 max, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rt=(RectTransform)go.transform; rt.anchorMin=min; rt.anchorMax=max; rt.anchoredPosition=position; rt.sizeDelta=size; go.GetComponent<Image>().color=color; return go;
        }
        static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        static GameObject ButtonBox(Transform parent, string label, Vector2 position, Vector2 size)
        {
            var go=ImageBox(parent,label+" Button",Vector2.zero,Vector2.zero,position,size,Surface); go.AddComponent<Button>();
            TextBox(go.transform,"Label",new Vector2(.5f,.5f),Vector2.zero,size-Vector2.one*8,26,Color.white).text=label; return go;
        }
        static Text TextBox(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size,int fontSize,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent,false); var rt=(RectTransform)go.transform; rt.anchorMin=rt.anchorMax=anchor; rt.anchoredPosition=position; rt.sizeDelta=size;
            var text=go.GetComponent<Text>(); text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize=fontSize; text.alignment=TextAnchor.MiddleCenter; text.color=color; text.raycastTarget=false; return text;
        }
        static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
