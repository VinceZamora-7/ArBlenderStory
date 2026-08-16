using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace ARLearning.AR
{
    public sealed class ARAvailabilityHandler : MonoBehaviour
    {
        public string Status { get; private set; } = "Starting AR…";

        IEnumerator Start()
        {
            yield return ARSession.CheckAvailability();
            if (ARSession.state == ARSessionState.NeedsInstall)
            {
                // This app is AR Optional. Do not force Google Play Services for AR
                // installation on devices that cannot obtain it; a future preview
                // mode can remain available without an AR session.
                Status = "AR is unavailable on this device.";
                GetComponent<ARSession>().enabled = false;
                yield break;
            }
            UpdateStatus(ARSession.state);
        }

        void OnEnable() => ARSession.stateChanged += OnStateChanged;
        void OnDisable() => ARSession.stateChanged -= OnStateChanged;
        void OnStateChanged(ARSessionStateChangedEventArgs args) => UpdateStatus(args.state);

        void UpdateStatus(ARSessionState state)
        {
            Status = state switch
            {
                ARSessionState.Unsupported => "AR is not supported on this device.",
                ARSessionState.NeedsInstall => "AR is unavailable on this device.",
                ARSessionState.Installing => "Preparing AR…",
                ARSessionState.Ready => "Move your device to scan a surface.",
                ARSessionState.SessionInitializing => "Initializing AR…",
                ARSessionState.SessionTracking => "Move your device to scan a surface.",
                _ => "AR tracking is unavailable. Check camera permission."
            };
        }
    }
}
