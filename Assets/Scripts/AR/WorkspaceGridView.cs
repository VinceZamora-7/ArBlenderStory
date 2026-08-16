using UnityEngine;

namespace ARLearning.AR
{
    /// <summary>A neutral Blender-like preview background with a camera-facing grid.</summary>
    public sealed class WorkspaceGridView : MonoBehaviour
    {
        [SerializeField] Color m_Background = new(.16f, .17f, .19f, 1f);
        [SerializeField] Color m_Grid = new(.34f, .36f, .39f, .8f);

        void Start()
        {
            var camera = Camera.main;
            if (camera == null) return;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = m_Background;
            CreateGrid(camera.transform);
        }

        void CreateGrid(Transform camera)
        {
            var root = new GameObject("Workspace Grid").transform;
            root.SetParent(camera, false);
            root.localPosition = new Vector3(0f, 0f, 1.5f);
            const int divisions = 12;
            const float halfSize = 1.2f;
            for (var i = -divisions; i <= divisions; i++)
            {
                var value = i * halfSize / divisions;
                CreateLine(root, new Vector3(value, -halfSize, 0f), new Vector3(value, halfSize, 0f), i == 0 ? new Color(.58f, .18f, .18f, .9f) : m_Grid);
                CreateLine(root, new Vector3(-halfSize, value, 0f), new Vector3(halfSize, value, 0f), i == 0 ? new Color(.2f, .52f, .25f, .9f) : m_Grid);
            }
        }

        static void CreateLine(Transform parent, Vector3 start, Vector3 end, Color color)
        {
            var line = new GameObject("Grid Line", typeof(LineRenderer)).GetComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, start); line.SetPosition(1, end);
            line.widthMultiplier = .004f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = color;
            line.sortingOrder = -10;
        }
    }
}
