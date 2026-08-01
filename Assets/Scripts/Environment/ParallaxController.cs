using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives every <see cref="ParallaxLayer"/> in the scene from a single camera reference.
///
/// Owning the update in one place means the layers all read the same camera position on the same
/// frame and share one Depth Scale, so their authored Depth values stay comparable to each other.
/// Runs in LateUpdate so the camera has already moved this frame — updating in Update would leave
/// the layers a frame behind whatever moves the camera.
/// </summary>
[DisallowMultipleComponent]
public class ParallaxController : MonoBehaviour
{
    [Header("View Reference")]
    [Tooltip("The camera the layers parallax against. Leave empty to use Camera.main — the camera tagged MainCamera.")]
    [SerializeField] private Camera viewCamera;

    [Header("Depth")]
    [Tooltip("The depth at which a layer moves at half the camera's rate. Larger values make the whole scene read as flatter and further away; smaller values separate the layers faster. " +
             "Shared by every layer, so this is the one dial for the overall strength of the effect.")]
    [Min(0.01f)]
    [SerializeField] private float depthScale = 10f;

    [Header("Auto Scroll")]
    [Tooltip("Global multiplier on every layer's Auto Scroll Speed. 0 pauses all drift, 1 is the authored speed. Useful for stopping the scroll while a menu is open.")]
    [SerializeField] private float scrollMultiplier = 1f;
    [Tooltip("Use unscaled time so layers keep drifting while Time.timeScale is 0 (pause menus, hit-stop).")]
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Layers")]
    [Tooltip("Layers driven by this controller. Leave empty to collect every ParallaxLayer in the scene on Start.")]
    [SerializeField] private List<ParallaxLayer> layers = new List<ParallaxLayer>();

    private Vector3 cameraStartPosition;
    private bool ready;

    public float DepthScale => depthScale;
    public Camera ViewCamera => viewCamera;

    private void Start()
    {
        if (viewCamera == null) viewCamera = Camera.main;

        if (viewCamera == null)
        {
            Debug.LogError("[ParallaxController] No view camera assigned and no camera tagged MainCamera in the scene. Parallax disabled.", this);
            enabled = false;
            return;
        }

        if (layers.Count == 0)
        {
            layers.AddRange(FindObjectsByType<ParallaxLayer>(FindObjectsSortMode.None));
            Debug.Log($"[ParallaxController] Collected {layers.Count} parallax layer(s) from the scene.");
        }

        Recapture();
    }

    private void LateUpdate()
    {
        if (!ready) return;

        Vector2 cameraDelta = viewCamera.transform.position - cameraStartPosition;
        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i] == null) continue;
            layers[i].Tick(cameraDelta, depthScale, scrollMultiplier, deltaTime);
        }
    }

    /// <summary>
    /// Re-baselines the camera and every layer to their current positions, and clears accumulated
    /// scroll. Call this after moving the camera or the layers deliberately (a scene reset, a cut
    /// to a new vista) so the offsets are measured from the new arrangement rather than the old.
    /// </summary>
    [ContextMenu("Recapture Layer Positions")]
    public void Recapture()
    {
        if (viewCamera == null) return;

        cameraStartPosition = viewCamera.transform.position;

        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i] == null) continue;
            layers[i].CaptureStartPosition();
        }

        ready = true;
    }

    /// <summary>Adds a layer created after Start (and baselines it immediately).</summary>
    public void Register(ParallaxLayer layer)
    {
        if (layer == null || layers.Contains(layer)) return;

        layers.Add(layer);
        layer.CaptureStartPosition();
    }

    public void Unregister(ParallaxLayer layer)
    {
        layers.Remove(layer);
    }

    /// <summary>0 pauses every layer's auto scroll, 1 restores the authored speeds.</summary>
    public void SetScrollMultiplier(float multiplier)
    {
        scrollMultiplier = multiplier;
    }
}
