using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Draws a line from the source of the currently selected action to a legal target while the
/// cursor is over one, so it reads as "this action, going from here to there". The line is hidden
/// otherwise; the hardware cursor carries the feedback for empty space and illegal targets.
///
/// Shows only while an action is selected — which, because TurnManager clears the selection on
/// every turn change, means only during a player's own turn.
///
/// Experimental / self-contained: the whole feature is this component plus one GameObject in the
/// scene. Delete both and nothing else changes.
///
/// Everything is resolved through the camera into world space, which is exact for an orthographic
/// camera, so the line joins a screen-space UI button to a world-space combatant correctly.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
[DisallowMultipleComponent]
public class ActionTargetingLine : MonoBehaviour
{
    /// <summary>What sits under the cursor, as far as the selected action is concerned.</summary>
    private enum TargetState
    {
        /// <summary>Empty space — no combatant under the cursor.</summary>
        None,
        /// <summary>A combatant the selected action may be used on.</summary>
        Valid,
        /// <summary>A combatant, but an illegal one for this action (an ally for an attack, etc.).</summary>
        Invalid
    }

    public enum Anchor
    {
        [Tooltip("Start the line at the character whose turn it is.")]
        ActingCharacter,
        [Tooltip("Start the line at the action button that was clicked.")]
        ActionButton
    }

    [Header("Setup")]
    [Tooltip("Draw the line at all. Turn this off to keep the cursor target feedback on its own — the two are independent.")]
    [SerializeField] private bool showLine = true;

    [Tooltip("Camera used to convert screen positions to world space. Leave empty to use Camera.main.")]
    [SerializeField] private Camera viewCamera;

    [Tooltip("Where the line starts. Acting Character reads as the attack coming from the character; Action Button reads as it coming from the ability you picked.")]
    [SerializeField] private Anchor anchoredTo = Anchor.ActingCharacter;

    [Tooltip("World Z the line is drawn on. Keep it on the combatants' plane (0) unless they move off it.")]
    [SerializeField] private float worldZ = 0f;

    [Header("Shape")]
    [Tooltip("Points along the line. 2 is a straight segment; more is only needed for an arc.")]
    [SerializeField, Min(2)] private int segments = 24;

    [Tooltip("Height of the arc at its midpoint, in world units. 0 draws a straight line.")]
    [SerializeField] private float arcHeight = 0.75f;

    [SerializeField] private float startWidth = 0.12f;
    [SerializeField] private float endWidth = 0.03f;

    [Header("Colour")]
    [Tooltip("The line only appears over a valid target, so a single colour pair covers it. Invalid and empty-space feedback is carried by the cursor.")]
    [SerializeField] private Color startColor = new Color(1f, 0.32f, 0.28f, 1f);
    [SerializeField] private Color endColor = new Color(1f, 0.32f, 0.28f, 0.5f);

    [Header("Cursor")]
    [Tooltip("Fallback cursor for when there is no CursorManager in the scene. With one present it owns the default and this is ignored.")]
    [SerializeField] private Texture2D defaultCursor;

    [Tooltip("Cursor shown while the pointer is over a legal target — a reticle or crosshair. Leave empty to keep the default cursor.")]
    [SerializeField] private Texture2D validTargetCursor;

    [Tooltip("Cursor shown over a combatant the action cannot be used on — a crossed-out or 'no entry' mark. Leave empty to keep the default cursor.")]
    [SerializeField] private Texture2D invalidTargetCursor;

    [Tooltip("Put the cursor's click point at the middle of the image. Turn off to set it by hand below.")]
    [SerializeField] private bool centreCursorHotspot = true;

    [Tooltip("Click point within the cursor image, in pixels from its top-left. Ignored while Centre Cursor Hotspot is on.")]
    [SerializeField] private Vector2 cursorHotspot = Vector2.zero;

    [Tooltip("Layers searched for a target under the cursor. Leave as Everything unless combatants share colliders with scenery.")]
    [SerializeField] private LayerMask targetLayers = ~0;

    [Tooltip("Material for the line. Leave empty to build a default unlit sprite material at runtime.")]
    [SerializeField] private Material lineMaterial;

    [Header("Sorting")]
    [Tooltip("Sorting Layer the line draws on. Front Near puts it above the combatants.")]
    [SerializeField] private string sortingLayer = "Front Near";
    [SerializeField] private int sortingOrder = 100;

    private LineRenderer lineRenderer;
    private CombatController combatController;
    private TargetState appliedCursorState = TargetState.None;

    /// <summary>Toggle the line at runtime. The cursor feedback is unaffected either way.</summary>
    public bool ShowLine
    {
        get => showLine;
        set => showLine = value;
    }

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();

        if (viewCamera == null) viewCamera = Camera.main;

        ConfigureRenderer();
        lineRenderer.enabled = false;
    }

    private void Start()
    {
        combatController = FindObjectOfType<CombatController>();

        if (combatController == null)
        {
            Debug.LogError("[ActionTargetingLine] No CombatController in the scene — targeting line disabled.", this);
            enabled = false;
        }
    }

    private void OnDisable()
    {
        // Never leave a swapped cursor behind — it would persist past this component.
        ApplyCursor(TargetState.None, force: true);

        if (lineRenderer != null) lineRenderer.enabled = false;
    }

    private void ConfigureRenderer()
    {
        lineRenderer.useWorldSpace = true;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.numCapVertices = 4;
        lineRenderer.startWidth = startWidth;
        lineRenderer.endWidth = endWidth;
        lineRenderer.startColor = startColor;
        lineRenderer.endColor = endColor;
        lineRenderer.sortingLayerName = sortingLayer;
        lineRenderer.sortingOrder = sortingOrder;

        if (lineMaterial != null)
        {
            lineRenderer.material = lineMaterial;
            return;
        }

        // LineRenderer with no material renders magenta, so fall back to an unlit sprite shader.
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");

        if (shader != null)
        {
            lineRenderer.material = new Material(shader);
        }
        else
        {
            Debug.LogWarning("[ActionTargetingLine] No line material assigned and no fallback shader found — assign one in the Inspector.", this);
        }
    }

    private void LateUpdate()
    {
        Action selected = combatController.selectedAction;

        if (selected == null || viewCamera == null || Mouse.current == null)
        {
            lineRenderer.enabled = false;
            ApplyCursor(TargetState.None);
            return;
        }

        Vector3 cursorWorld = ScreenToWorld(Mouse.current.position.ReadValue());
        TargetState state = EvaluateTarget(cursorWorld, out Vector3 targetPoint);

        // Cursor first and unconditionally: it reports all three states and must keep working
        // whether or not the line is drawn, and whether or not the line can find its start point.
        ApplyCursor(state);

        if (!showLine || state != TargetState.Valid || !TryResolveStart(selected, out Vector3 start))
        {
            lineRenderer.enabled = false;
            return;
        }

        lineRenderer.enabled = true;
        DrawLine(start, targetPoint);
    }

    /// <summary>
    /// Classifies whatever is under the cursor, and for a valid target reports the point the line
    /// should end on. Validity comes from CombatController, so the feedback can never disagree
    /// with what a click would actually do.
    ///
    /// Distinguishing Invalid from None still matters even though the line ignores both: empty
    /// space isn't a mistake, but hovering a combatant the action can't touch is, and the cursor
    /// draws that distinction.
    /// </summary>
    private TargetState EvaluateTarget(Vector3 worldPoint, out Vector3 targetPoint)
    {
        targetPoint = worldPoint;

        Collider2D[] hits = Physics2D.OverlapPointAll(worldPoint, targetLayers);
        bool foundCombatant = false;

        foreach (Collider2D hit in hits)
        {
            CharacterManager candidate = hit.GetComponentInParent<CharacterManager>();
            if (candidate == null) continue;

            // Any legal target under the cursor wins, even if an illegal one overlaps it.
            if (combatController.IsValidTarget(candidate))
            {
                // Bounds centre rather than transform.position — it's the middle of the artwork
                // regardless of where the character root's pivot sits.
                Vector3 centre = hit.bounds.center;
                targetPoint = new Vector3(centre.x, centre.y, worldZ);

                return TargetState.Valid;
            }

            foundCombatant = true;
        }

        return foundCombatant ? TargetState.Invalid : TargetState.None;
    }

    private void ApplyCursor(TargetState state, bool force = false)
    {
        // Cursor.SetCursor can rebuild the hardware cursor, so only touch it on a state change.
        if (!force && state == appliedCursorState) return;

        appliedCursorState = state;

        Texture2D texture = state == TargetState.Valid ? validTargetCursor
                          : state == TargetState.Invalid ? invalidTargetCursor
                          : null;

        // A state with no texture assigned falls through to the default, so each one is opt-in.
        if (texture != null)
        {
            Vector2 hotspot = CursorManager.ResolveHotspot(texture, centreCursorHotspot, cursorHotspot);

            if (CursorManager.Instance != null)
            {
                CursorManager.Instance.SetCursor(texture, hotspot);
            }
            else
            {
                Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
            }

            return;
        }

        // Hand the cursor back rather than clearing it — resetting to null here would discard the
        // game's default cursor and drop to the system one.
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.ResetToDefault();
            return;
        }

        Cursor.SetCursor(defaultCursor, CursorManager.ResolveHotspot(defaultCursor, centreCursorHotspot, cursorHotspot), CursorMode.Auto);
    }

    private bool TryResolveStart(Action selected, out Vector3 start)
    {
        start = Vector3.zero;

        if (anchoredTo == Anchor.ActingCharacter)
        {
            CharacterManager actor = combatController.GetCurrentCharacter();
            if (actor == null) return false;

            start = new Vector3(actor.transform.position.x, actor.transform.position.y, worldZ);
            return true;
        }

        // Anchor.ActionButton — find the live button holding this action. The buttons are rebuilt
        // every turn, so this is looked up per frame rather than cached.
        ActionButtonHover[] buttons = FindObjectsByType<ActionButtonHover>(FindObjectsSortMode.None);

        foreach (ActionButtonHover button in buttons)
        {
            if (button.AssociatedAction != selected) continue;

            RectTransform rect = button.transform as RectTransform;
            if (rect == null) continue;

            Canvas canvas = button.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null ? canvas.worldCamera : null;

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, rect.position);
            start = ScreenToWorld(screenPoint);
            return true;
        }

        return false;
    }

    private Vector3 ScreenToWorld(Vector2 screenPoint)
    {
        // For an orthographic camera the z passed in is simply the distance along the view axis,
        // so this lands exactly on the worldZ plane.
        float distance = worldZ - viewCamera.transform.position.z;
        Vector3 world = viewCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, distance));
        world.z = worldZ;

        return world;
    }

    private void DrawLine(Vector3 start, Vector3 end)
    {
        int pointCount = Mathf.Approximately(arcHeight, 0f) ? 2 : Mathf.Max(2, segments);
        lineRenderer.positionCount = pointCount;

        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (float)(pointCount - 1);
            Vector3 point = Vector3.Lerp(start, end, t);

            // sin gives 0 at both ends and 1 at the midpoint, so the ends stay pinned.
            point.y += Mathf.Sin(t * Mathf.PI) * arcHeight;

            lineRenderer.SetPosition(i, point);
        }
    }
}
