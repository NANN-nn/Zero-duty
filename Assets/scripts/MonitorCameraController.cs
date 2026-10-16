using System;
using UnityEngine;

/// <summary>References the three fixed CCTV cameras and their live RenderTextures.</summary>
[DisallowMultipleComponent]
public class MonitorCameraController : MonoBehaviour
{
    [Serializable]
    public class CameraControl
    {
        public Camera camera;
    }

    [Tooltip("固定顺序：0 左屏、1 中屏、2 右屏。Camera 的 Target Texture 在 Inspector 中配置。")]
    [SerializeField] private CameraControl[] cameras =
    {
        new CameraControl(), new CameraControl(), new CameraControl()
    };

    private void Awake()
    {
        if (GetComponent<MonitorFaultFeedback>() == null)
            gameObject.AddComponent<MonitorFaultFeedback>();
        // CCTV RenderTexture cameras are visual feeds, not extra audio ears.
        if (cameras == null) return;
        foreach (CameraControl feed in cameras)
        {
            if (feed == null || feed.camera == null) continue;
            AudioListener listener = feed.camera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
        }
    }

    public bool HasValidCameras
    {
        get
        {
            if (cameras == null || cameras.Length != 3) return false;
            for (int i = 0; i < 3; i++)
            {
                if (cameras[i] == null || cameras[i].camera == null) return false;
                for (int j = 0; j < i; j++)
                    if (cameras[i].camera == cameras[j].camera) return false;
            }
            return true;
        }
    }

    public Camera GetCamera(int index)
    {
        if (cameras == null || index < 0 || index >= cameras.Length || cameras[index] == null)
            return null;
        return cameras[index].camera;
    }

}
