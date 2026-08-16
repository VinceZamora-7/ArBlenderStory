using UnityEngine;

namespace ARLearning.AR
{
    /// <summary>
    /// Starts the app in a regular 3D preview so phones without ARCore can still
    /// inspect and transform the learning object. AR is deliberately not started.
    /// </summary>
    public sealed class PreviewModeController : MonoBehaviour
    {
        [SerializeField] ARPlacementManager m_Placement;

        void Start() => m_Placement.PlacePreviewObject();
    }
}
