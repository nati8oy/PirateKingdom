using UnityEngine;

/// <summary>
/// One parallax layer. Holds the layer's authored depth and its own drift speed; the driving is
/// done by <see cref="ParallaxController"/> so there is a single update owner and a shared depth
/// scale. Drop this on the empty GameObject that parents the layer's sprites.
///
/// This script never writes Z. Draw order is Sorting Layer / Order in Layer on the renderers —
/// with an orthographic camera, Z contributes nothing visually.
/// </summary>
[DisallowMultipleComponent]
public class ParallaxLayer : MonoBehaviour
{
    // A layer sitting exactly on the lens (depth == -depthScale) is a divide-by-zero; back off instead.
    private const float MinDenominator = 0.05f;
    // Stops a badly authored foreground depth from flinging the layer across the screen.
    private const float MinFactor = -3f;

    [Header("Depth")]
    [Tooltip("How far this layer sits from the combat plane, in the same units as the controller's Depth Scale. " +
             "0 = the combat plane itself (no parallax — the camera slides past it at full rate). " +
             "Positive = behind it, drifts less on screen the larger it gets. " +
             "Negative = in front of it (Front Middle / Front Near), drifts more.")]
    [SerializeField] private float depth = 0f;

    [Header("Camera Parallax")]
    [Tooltip("Apply the camera's horizontal movement to this layer.")]
    [SerializeField] private bool followCameraX = true;
    [Tooltip("Apply the camera's vertical movement to this layer. Turn off to keep a layer level while the camera rises or shakes vertically.")]
    [SerializeField] private bool followCameraY = true;

    [Header("Auto Scroll")]
    [Tooltip("Constant drift in world units per second, independent of the camera. Negative X scrolls left. " +
             "Leave at zero for a layer that only reacts to the camera.")]
    [SerializeField] private Vector2 autoScrollSpeed = Vector2.zero;

    [Header("Runtime (read only)")]
    [Tooltip("The parallax factor the controller derived from Depth. 0 = locked to the world, 1 = pinned to the camera. Shown for tuning — editing it does nothing.")]
    [SerializeField] private float resolvedParallaxFactor;

    private Vector3 startPosition;
    private Vector2 scrollOffset;
    private bool captured;

    public float Depth => depth;
    public Vector2 AutoScrollSpeed => autoScrollSpeed;
    public float ResolvedParallaxFactor => resolvedParallaxFactor;

    /// <summary>
    /// Re-baselines the layer to wherever it currently sits and clears accumulated scroll.
    /// Called by the controller on Start; call it again if you reposition a layer at runtime.
    /// </summary>
    [ContextMenu("Recapture Start Position")]
    public void CaptureStartPosition()
    {
        startPosition = transform.position;
        scrollOffset = Vector2.zero;
        captured = true;
    }

    /// <summary>
    /// Driven by <see cref="ParallaxController"/> every LateUpdate.
    /// <paramref name="cameraDelta"/> is the camera's <b>total</b> displacement from its own start
    /// position, not a per-frame delta — an absolute reference can't accumulate positional error.
    /// </summary>
    public void Tick(Vector2 cameraDelta, float depthScale, float scrollMultiplier, float deltaTime)
    {
        if (!captured) CaptureStartPosition();

        resolvedParallaxFactor = CalculateParallaxFactor(depthScale);

        // Scroll is accumulated rather than derived from elapsed time, so changing autoScrollSpeed
        // or scrollMultiplier at runtime eases in instead of snapping. Unlike the camera offset
        // there's no ground-truth position here for accumulated error to drift away from.
        scrollOffset += autoScrollSpeed * (scrollMultiplier * deltaTime);

        Vector3 position = transform.position;
        position.x = startPosition.x + scrollOffset.x + (followCameraX ? cameraDelta.x * resolvedParallaxFactor : 0f);
        position.y = startPosition.y + scrollOffset.y + (followCameraY ? cameraDelta.y * resolvedParallaxFactor : 0f);
        transform.position = position; // Z deliberately left alone — see the class comment.
    }

    /// <summary>
    /// depth / (depth + depthScale). Yields 0 at the combat plane, 0.5 at depth == depthScale,
    /// and approaches 1 (pinned to the camera, i.e. the horizon) as depth grows.
    /// </summary>
    private float CalculateParallaxFactor(float depthScale)
    {
        if (depthScale <= 0f) return 0f;

        float denominator = depth + depthScale;
        if (denominator < MinDenominator) denominator = MinDenominator;

        return Mathf.Clamp(depth / denominator, MinFactor, 1f);
    }
}
