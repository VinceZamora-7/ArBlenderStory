using System;

namespace ARLearning.AR
{
    public enum ARLearningState { Scanning, PlacementReady, Placed, Editing }

    public sealed class ARLearningStateController : UnityEngine.MonoBehaviour
    {
        public ARLearningState Current { get; private set; } = ARLearningState.Scanning;
        public event Action<ARLearningState> Changed;

        public void Set(ARLearningState state)
        {
            if (Current == state) return;
            Current = state;
            Changed?.Invoke(state);
        }
    }
}
