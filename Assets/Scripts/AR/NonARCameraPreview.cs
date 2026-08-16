using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ARLearning.AR
{
    /// <summary>Displays the device's regular rear camera in preview mode. It is not AR tracking.</summary>
    public sealed class NonARCameraPreview : MonoBehaviour
    {
        WebCamTexture _webcam;

        IEnumerator Start()
        {
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam)) yield break;

            var deviceName = string.Empty;
            foreach (var device in WebCamTexture.devices)
                if (!device.isFrontFacing) { deviceName = device.name; break; }
            if (string.IsNullOrEmpty(deviceName) && WebCamTexture.devices.Length > 0)
                deviceName = WebCamTexture.devices[0].name;
            if (string.IsNullOrEmpty(deviceName)) yield break;

            _webcam = new WebCamTexture(deviceName, 1280, 720, 30);
            _webcam.Play();
            CreateBackground(_webcam);
        }

        void OnDisable()
        {
            if (_webcam != null && _webcam.isPlaying) _webcam.Stop();
        }

        static void CreateBackground(WebCamTexture webcam)
        {
            var canvas = new GameObject("Preview Camera Background", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var camera = Camera.main;
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceCamera;
            canvas.GetComponent<Canvas>().worldCamera = camera;
            canvas.GetComponent<Canvas>().planeDistance = camera.nearClipPlane + 0.01f;
            canvas.GetComponent<Canvas>().sortingOrder = -100;

            var image = new GameObject("Camera Feed", typeof(RectTransform), typeof(RawImage));
            image.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)image.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var rawImage = image.GetComponent<RawImage>();
            rawImage.texture = webcam;
            rawImage.raycastTarget = false;
            // Draw the camera feed before the world object so the preview object
            // remains visible in front of it.
            var material = new Material(Shader.Find("UI/Default")) { renderQueue = 1000 };
            rawImage.material = material;
        }
    }
}
