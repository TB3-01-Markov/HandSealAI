using System.Collections;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace HandSeals.Game
{
    public enum CameraFeedState
    {
        Starting = 0,
        Running = 1,
        PermissionDenied = 2,
        NoCamera = 3
    }

    /// <summary>
    /// Owns the device camera: asks for permission, prefers the front camera and exposes frames.
    /// </summary>
    public sealed class CameraFeed : MonoBehaviour
    {
        private const float PermissionTimeoutSeconds = 60f;
        private const float FirstFrameTimeoutSeconds = 10f;

        private Color32[] _pixels;
        private Coroutine _startRoutine;
        private bool _started;

        public int RequestedWidth { get; set; } = 640;
        public int RequestedHeight { get; set; } = 480;
        public int RequestedFps { get; set; } = 30;

        public CameraFeedState State { get; private set; } = CameraFeedState.Starting;

        public WebCamTexture Texture { get; private set; }

        public bool IsFrontFacing { get; private set; }

        public string DeviceName { get; private set; } = string.Empty;

        public bool IsRunning => State == CameraFeedState.Running && Texture != null;

        public bool DidUpdateThisFrame => IsRunning && Texture.didUpdateThisFrame;

        public int Width => IsRunning ? Texture.width : 0;

        public int Height => IsRunning ? Texture.height : 0;

        /// <summary>Clockwise rotation in degrees that makes the frame upright.</summary>
        public int RotationAngle => IsRunning ? Texture.videoRotationAngle : 0;

        /// <summary>True when the stored frame is upside down compared to a normal Unity texture.</summary>
        public bool VerticallyMirrored => IsRunning && Texture.videoVerticallyMirrored;

        /// <summary>Pixels of the current frame, rows bottom-up. The array is reused between calls.</summary>
        public Color32[] ReadPixels()
        {
            int count = Texture.width * Texture.height;
            if (_pixels == null || _pixels.Length != count)
            {
                _pixels = new Color32[count];
            }

            Texture.GetPixels32(_pixels);
            return _pixels;
        }

        // The camera starts in Start, not in OnEnable, so that whoever adds this component
        // can still set the requested resolution first.
        private void Start()
        {
            _started = true;
            Restart(true);
        }

        private void OnEnable()
        {
            if (_started)
            {
                Restart(true);
            }
        }

        private void OnDisable()
        {
            StopCamera();
        }

        /// <summary>Shows the system permission dialog again; for an "Allow camera" button.</summary>
        public void RequestPermission()
        {
            if (isActiveAndEnabled && State == CameraFeedState.PermissionDenied)
            {
                Restart(true);
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // The player may have granted the permission in the system settings and come back.
            // Only check, never ask here: closing the permission dialog also returns focus,
            // and asking again would trap a player who said no in an endless dialog.
            if (hasFocus && isActiveAndEnabled && State == CameraFeedState.PermissionDenied && IsAuthorized())
            {
                Restart(false);
            }
        }

        private static bool IsAuthorized()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return Permission.HasUserAuthorizedPermission(Permission.Camera);
#else
            return Application.HasUserAuthorization(UserAuthorization.WebCam);
#endif
        }

        private void OnApplicationPause(bool paused)
        {
            if (Texture == null || State != CameraFeedState.Running)
            {
                return;
            }

            if (paused)
            {
                Texture.Pause();
            }
            else
            {
                Texture.Play();
            }
        }

        private void Restart(bool askPermission)
        {
            StopCamera();
            State = CameraFeedState.Starting;
            _startRoutine = StartCoroutine(StartCamera(askPermission));
        }

        private void StopCamera()
        {
            if (_startRoutine != null)
            {
                StopCoroutine(_startRoutine);
                _startRoutine = null;
            }

            if (Texture != null)
            {
                Texture.Stop();
                Destroy(Texture);
                Texture = null;
            }
        }

        private IEnumerator StartCamera(bool askPermission)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (askPermission && !IsAuthorized())
            {
                // The callbacks report a refusal at once; without them a "no" would only show after the timeout.
                bool answered = false;
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => answered = true;
                callbacks.PermissionDenied += _ => answered = true;
                Permission.RequestUserPermission(Permission.Camera, callbacks);

                float waited = 0f;
                while (!answered && !IsAuthorized() && waited < PermissionTimeoutSeconds)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
#else
            if (askPermission && !IsAuthorized())
            {
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            }
#endif

            if (!IsAuthorized())
            {
                State = CameraFeedState.PermissionDenied;
                _startRoutine = null;
                yield break;
            }

            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0)
            {
                State = CameraFeedState.NoCamera;
                _startRoutine = null;
                yield break;
            }

            WebCamDevice chosen = devices[0];
            foreach (WebCamDevice device in devices)
            {
                if (device.isFrontFacing)
                {
                    chosen = device;
                    break;
                }
            }

            DeviceName = chosen.name;
            IsFrontFacing = chosen.isFrontFacing;
            Texture = new WebCamTexture(chosen.name, RequestedWidth, RequestedHeight, RequestedFps);
            Texture.Play();

            // Until the first frame arrives the texture reports a 16 x 16 placeholder size.
            float elapsed = 0f;
            while (Texture.width <= 16 && elapsed < FirstFrameTimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            State = Texture.width > 16 ? CameraFeedState.Running : CameraFeedState.NoCamera;
            _startRoutine = null;
        }
    }
}
