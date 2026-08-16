using UnityEngine;

namespace ARLearning.AR
{
    /// <summary>Subtle green top-face cue for orientation in the learning workspace.</summary>
    public sealed class TopFaceCue : MonoBehaviour
    {
        void Awake()
        {
            var cue = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cue.name = "Top Face Orientation Cue";
            cue.transform.SetParent(transform, false);
            cue.transform.localPosition = new Vector3(0f, .501f, 0f);
            cue.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            cue.transform.localScale = new Vector3(.72f, .72f, 1f);
            Destroy(cue.GetComponent<Collider>());
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(.20f, .72f, .43f, 1f));
            cue.GetComponent<MeshRenderer>().material = material;
        }
    }
}
