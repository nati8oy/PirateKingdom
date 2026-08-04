using UnityEngine;

/// <summary>
/// Single owner of the hardware cursor. Applies the game's default cursor on load and hands it
/// back whenever a temporary cursor (targeting reticle, etc.) is finished with.
///
/// Without one owner, every component that calls <c>Cursor.SetCursor</c> ends up restoring the
/// system cursor when it's done, silently discarding whatever the rest of the game had set.
/// Anything that swaps the cursor should go through here.
///
/// Survives scene loads, so the cursor stays consistent between the encounter and (later) the
/// voyage map. Every consumer must tolerate <see cref="Instance"/> being null — the encounter
/// scene is playable without one.
/// </summary>
[DisallowMultipleComponent]
public class CursorManager : MonoBehaviour
{
    private static CursorManager instance;

    /// <summary>Resolved lazily so script execution order can't strand a caller.</summary>
    public static CursorManager Instance
    {
        get
        {
            if (instance == null) instance = FindObjectOfType<CursorManager>();
            return instance;
        }
    }

    [Header("Default Cursor")]
    [Tooltip("Cursor used everywhere unless something temporarily overrides it. Leave empty to use the system cursor.")]
    [SerializeField] private Texture2D defaultCursor;

    [Tooltip("Put the click point at the middle of the image. Turn off to set it by hand below.")]
    [SerializeField] private bool centreHotspot = true;

    [Tooltip("Click point within the cursor image, in pixels from its top-left. Ignored while Centre Hotspot is on.")]
    [SerializeField] private Vector2 hotspot = Vector2.zero;

    [Header("Lifetime")]
    [Tooltip("Keep this object across scene loads so the cursor stays consistent between scenes.")]
    [SerializeField] private bool persistAcrossScenes = true;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[CursorManager] A second instance on '{gameObject.name}' was destroyed — there should only ever be one.", this);
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (persistAcrossScenes) DontDestroyOnLoad(gameObject);

        ApplyDefault();
    }

    /// <summary>Applies the configured default cursor. Call after changing it at runtime.</summary>
    [ContextMenu("Apply Default Cursor")]
    public void ApplyDefault()
    {
        Cursor.SetCursor(defaultCursor, ResolveHotspot(defaultCursor, centreHotspot, hotspot), CursorMode.Auto);
    }

    /// <summary>Temporarily swaps the cursor. Pass a null texture to fall back to the default.</summary>
    public void SetCursor(Texture2D texture, Vector2 cursorHotspot)
    {
        if (texture == null)
        {
            ApplyDefault();
            return;
        }

        Cursor.SetCursor(texture, cursorHotspot, CursorMode.Auto);
    }

    /// <summary>Hands the cursor back to the default. The counterpart to <see cref="SetCursor"/>.</summary>
    public void ResetToDefault()
    {
        ApplyDefault();
    }

    /// <summary>Shared hotspot rule so callers centre their cursors the same way this one does.</summary>
    public static Vector2 ResolveHotspot(Texture2D texture, bool centre, Vector2 explicitHotspot)
    {
        if (texture == null) return Vector2.zero;

        return centre
            ? new Vector2(texture.width * 0.5f, texture.height * 0.5f)
            : explicitHotspot;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
