using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Routes a click on this combatant to the <see cref="CombatController"/> as a target selection.
///
/// Supports both presentation setups, so the sprite conversion can be done one prefab at a time:
///  - <b>World sprite body:</b> implements <see cref="IPointerClickHandler"/>. Needs a Collider2D
///    on this GameObject and a Physics2DRaycaster on the camera — the same pair that
///    <see cref="TargetHoverHandler"/> needs for hover, so adding it lights up both.
///  - <b>UI body:</b> <see cref="CharacterClicked"/> is still public and still wired to the
///    Button's OnClick on PlayerCharacter.prefab.
/// </summary>
public class ClickableCharacter : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private CombatController combatController;

    private CharacterManager characterManager;

    private void Awake()
    {
        // GetComponentInParent so this works whether it sits on the combatant root or on a
        // dedicated hit-box child of it.
        characterManager = GetComponentInParent<CharacterManager>();

        if (characterManager == null)
        {
            Debug.LogError($"No CharacterManager found on or above {gameObject.name}!", this);
        }
    }

    private void Start()
    {
        // Find the CombatController if not assigned
        if (combatController == null)
            combatController = FindObjectOfType<CombatController>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        CharacterClicked();
    }

    /// <summary>Also invoked directly by the UI Button's OnClick while the body is still a UI Image.</summary>
    public void CharacterClicked()
    {
        if (combatController == null)
        {
            Debug.LogError($"No CombatController available for {gameObject.name}!", this);
            return;
        }

        if (characterManager == null)
        {
            Debug.LogError($"No CharacterManager found on or above {gameObject.name}!", this);
            return;
        }

        // TryExecuteActionOnTarget performs the action and clears the selection on success, so
        // there is nothing to follow up with here.
        if (combatController.TryExecuteActionOnTarget(characterManager))
        {
            Debug.Log($"Action executed on {characterManager.name}");
        }
    }
}
