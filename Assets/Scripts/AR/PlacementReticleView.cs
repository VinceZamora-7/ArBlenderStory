using UnityEngine;

namespace ARLearning.AR
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PlacementReticleView : MonoBehaviour
    {
        void Awake()
        {
            var mesh = new Mesh { name = "Placement Reticle" };
            const int segments = 32;
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            for (var i = 0; i < segments; i++)
            {
                var a = i * Mathf.PI * 2f / segments;
                var next = (i + 1) % segments;
                vertices[i * 2] = new Vector3(Mathf.Cos(a) * .075f, 0, Mathf.Sin(a) * .075f);
                vertices[i * 2 + 1] = new Vector3(Mathf.Cos(a) * .09f, 0, Mathf.Sin(a) * .09f);
                var t = i * 6; var n = next * 2;
                triangles[t] = i * 2; triangles[t + 1] = n; triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = i * 2 + 1; triangles[t + 4] = n; triangles[t + 5] = n + 1;
            }
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.2f, .9f, 1f, .9f) };
        }
    }
}
