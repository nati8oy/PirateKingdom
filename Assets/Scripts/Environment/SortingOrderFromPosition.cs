using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Derives Order in Layer from world Y, so objects standing lower on screen draw in front.
///
/// Combatants are spawned from a shared prefab and therefore all inherit one authored Order in
/// Layer — with an orthographic camera and equal Z, overlapping instances then sort arbitrarily.
/// Put this on the combatant root (or any prop that shares a Sorting Layer with them) and the
/// order is decided by where it stands instead.
///
/// Authored orders are preserved as <i>relative</i> offsets: each renderer keeps its own
/// hand-set order relative to its siblings, and the whole group is shifted together. That keeps
/// a health-bar canvas above its own body without pinning it above another character's.
/// </summary>
[DisallowMultipleComponent]
public class SortingOrderFromPosition : MonoBehaviour
{
    // Order in Layer is a short; stay well inside it however far an object wanders.
    private const int MaxOrder = 30000;

    [Header("Sorting")]
    [Tooltip("Order-in-Layer steps per world unit of Y. Higher separates characters more firmly, but leaves less headroom before hitting the sorting limits. 100 means two characters half a unit apart sort 50 steps apart.")]
    [SerializeField] private int stepsPerUnit = 100;

    [Tooltip("Added to every renderer under this object, after the Y offset. Use it to lift a whole combatant above scenery sharing its Sorting Layer.")]
    [SerializeField] private int additionalOffset = 0;

    [Header("Updating")]
    [Tooltip("Recalculate every frame. Only needed if this object moves during a fight — leave off for combatants standing on fixed spawn points.")]
    [SerializeField] private bool updateContinuously = false;

    private readonly List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
    private readonly List<int> spriteBaseOrders = new List<int>();
    private readonly List<Canvas> canvases = new List<Canvas>();
    private readonly List<int> canvasBaseOrders = new List<int>();

    private void Start()
    {
        CacheRenderers();
        Refresh();
    }

    private void LateUpdate()
    {
        if (updateContinuously) Refresh();
    }

    /// <summary>Re-reads the renderers under this object and their authored orders.</summary>
    public void CacheRenderers()
    {
        spriteRenderers.Clear();
        spriteBaseOrders.Clear();
        canvases.Clear();
        canvasBaseOrders.Clear();

        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            spriteRenderers.Add(renderer);
            spriteBaseOrders.Add(renderer.sortingOrder);
        }

        // World-space canvases (the per-character health bar / name plate) sort by the same
        // Sorting Layer and Order in Layer as sprites, so they have to ride along.
        foreach (Canvas canvas in GetComponentsInChildren<Canvas>(true))
        {
            if (canvas.renderMode != RenderMode.WorldSpace) continue;

            canvas.overrideSorting = true;
            canvases.Add(canvas);
            canvasBaseOrders.Add(canvas.sortingOrder);
        }
    }

    /// <summary>Applies the order for the object's current position. Call after moving it.</summary>
    [ContextMenu("Refresh Sorting Order")]
    public void Refresh()
    {
        // Negated: a lower Y is nearer the viewer, so it needs the higher order.
        int rowOffset = Mathf.Clamp(
            Mathf.RoundToInt(-transform.position.y * stepsPerUnit) + additionalOffset,
            -MaxOrder, MaxOrder);

        for (int i = 0; i < spriteRenderers.Count; i++)
        {
            if (spriteRenderers[i] == null) continue;
            spriteRenderers[i].sortingOrder = spriteBaseOrders[i] + rowOffset;
        }

        for (int i = 0; i < canvases.Count; i++)
        {
            if (canvases[i] == null) continue;
            canvases[i].sortingOrder = canvasBaseOrders[i] + rowOffset;
        }
    }
}
