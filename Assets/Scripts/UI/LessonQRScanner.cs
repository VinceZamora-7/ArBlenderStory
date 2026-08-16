using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using ZXing;
using ZXing.Common;

namespace ARLearning.UI
{
    /// <summary>Owns the regular device camera only while the Scan route is visible.</summary>
    public sealed class LessonQRScanner : MonoBehaviour
    {
        public event Action<string> CodeRecognized;
        public event Action<string> StatusChanged;

        public RenderTexture PreviewTexture { get; private set; }
        public bool IsRunning => _webcam != null && _webcam.isPlaying;

        WebCamTexture _webcam;
        BarcodeReaderGeneric _reader;
        Coroutine _startRoutine;
        float _nextDecodeTime;
        bool _decodeLocked;
        Color32[] _pixels;
        byte[] _rgbPixels;
        readonly List<Behaviour> _suspendedArComponents = new();

        public void BeginScanning()
        {
            StopScanning();
            _decodeLocked = false;
            _startRoutine = StartCoroutine(StartCamera());
        }

        public void StopScanning()
        {
            if (_startRoutine != null)
            {
                StopCoroutine(_startRoutine);
                _startRoutine = null;
            }
            if (_webcam != null)
            {
                if (_webcam.isPlaying) _webcam.Stop();
                Destroy(_webcam);
                _webcam = null;
            }
            if (PreviewTexture != null)
            {
                PreviewTexture.Release();
                Destroy(PreviewTexture);
                PreviewTexture = null;
            }
            RestoreArCamera();
        }

        IEnumerator StartCamera()
        {
            SuspendArCamera();
            // AR Foundation releases its native camera asynchronously.
            yield return null;
            yield return null;
            StatusChanged?.Invoke("Requesting camera permission…");
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);

            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                StatusChanged?.Invoke("Camera permission is required to scan a lesson QR code.");
                yield break;
            }

            StatusChanged?.Invoke("Finding the rear camera…");
            var discoveryTimeout = Time.realtimeSinceStartup + 5f;
            var devices = WebCamTexture.devices;
            while (devices.Length == 0 && Time.realtimeSinceStartup < discoveryTimeout)
            {
                yield return new WaitForSecondsRealtime(.1f);
                devices = WebCamTexture.devices;
            }

            var deviceName = devices.Length > 0 ? devices[0].name : string.Empty;
            foreach (var device in devices)
                if (!device.isFrontFacing) { deviceName = device.name; break; }

            // Some Android camera providers expose no entries in devices but still
            // support Unity's default WebCamTexture constructor.
            _webcam = string.IsNullOrEmpty(deviceName)
                ? new WebCamTexture(1280, 720, 30)
                : new WebCamTexture(deviceName, 1280, 720, 30);
            _webcam.Play();
            var timeout = Time.realtimeSinceStartup + 8f;
            while (_webcam.width <= 16 && Time.realtimeSinceStartup < timeout) yield return null;
            _startRoutine = null;

            if (!_webcam.isPlaying || _webcam.width <= 16)
            {
                StatusChanged?.Invoke("The camera did not start. Check camera permission and try again.");
                StopScanning();
                yield break;
            }

            PreviewTexture = new RenderTexture(_webcam.width, _webcam.height, 0, RenderTextureFormat.ARGB32)
            {
                name = "Lesson QR Camera Preview"
            };
            PreviewTexture.Create();
            _reader = new BarcodeReaderGeneric
            {
                AutoRotate = true,
                Options = new DecodingOptions
                {
                    PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE },
                    TryHarder = false
                }
            };
            StatusChanged?.Invoke("Point the camera at a MODELAR lesson QR code.");
        }

        void SuspendArCamera()
        {
            _suspendedArComponents.Clear();
            SuspendEnabled(FindObjectsByType<ARCameraBackground>());
            SuspendEnabled(FindObjectsByType<ARCameraManager>());
            SuspendEnabled(FindObjectsByType<ARSession>());
        }

        void SuspendEnabled<T>(T[] components) where T : Behaviour
        {
            foreach (var component in components)
            {
                if (component == null || !component.enabled) continue;
                component.enabled = false;
                _suspendedArComponents.Add(component);
            }
        }

        void RestoreArCamera()
        {
            foreach (var component in _suspendedArComponents)
                if (component != null) component.enabled = true;
            _suspendedArComponents.Clear();
        }

        void Update()
        {
            if (_webcam == null || !_webcam.isPlaying || _webcam.width <= 16 || PreviewTexture == null) return;
            Graphics.Blit(_webcam, PreviewTexture);
            if (_decodeLocked || Time.unscaledTime < _nextDecodeTime || !_webcam.didUpdateThisFrame) return;
            _nextDecodeTime = Time.unscaledTime + .25f;

            try
            {
                _pixels = _webcam.GetPixels32(_pixels);
                var requiredBytes = _pixels.Length * 3;
                if (_rgbPixels == null || _rgbPixels.Length != requiredBytes) _rgbPixels = new byte[requiredBytes];
                for (var source = 0; source < _pixels.Length; source++)
                {
                    var destination = source * 3;
                    _rgbPixels[destination] = _pixels[source].r;
                    _rgbPixels[destination + 1] = _pixels[source].g;
                    _rgbPixels[destination + 2] = _pixels[source].b;
                }
                var luminance = new RGBLuminanceSource(
                    _rgbPixels, _webcam.width, _webcam.height, RGBLuminanceSource.BitmapFormat.RGB24);
                var result = _reader.Decode(luminance);
                if (result == null || string.IsNullOrWhiteSpace(result.Text)) return;
                _decodeLocked = true;
                StatusChanged?.Invoke("Lesson found. Opening tutorial…");
                CodeRecognized?.Invoke(result.Text.Trim());
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"QR frame could not be decoded: {exception.Message}", this);
            }
        }

        void OnDisable() => StopScanning();
        void OnDestroy() => StopScanning();
    }
}
