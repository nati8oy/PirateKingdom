using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class ParryVisualFeedback : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject parryWindowIndicator;
    [SerializeField] private Image parryTimerBar;
    [SerializeField] private TMP_Text parryStatusText;
    
    [Header("Visual Settings")]
    [SerializeField] private Color parryActiveColor = Color.yellow;
    [SerializeField] private Color parrySuccessColor = Color.green;
    [SerializeField] private Color parryFailedColor = Color.red;
    [SerializeField] private float feedbackDisplayTime = 1.0f;
    
    private ParrySystem parrySystem;
    private Coroutine feedbackCoroutine;
    private bool wasMyParryWindowActive = false;
    
    // Attack countdown tracking
    private bool isTrackingAttack = false;
    private float attackStartTime;
    private float totalAttackTime;
    
    private void Awake()
    {
        // Find ParrySystem on the same GameObject or its parent/children
        parrySystem = GetComponent<ParrySystem>();
        if (parrySystem == null)
        {
            parrySystem = GetComponentInParent<ParrySystem>();
        }
        if (parrySystem == null)
        {
            parrySystem = GetComponentInChildren<ParrySystem>();
        }
        
        if (parrySystem == null)
        {
            Debug.LogWarning($"[ParryVisualFeedback] No ParrySystem found on {gameObject.name} or its hierarchy!");
            return;
        }
        
        // Subscribe to parry system events
        parrySystem.OnParryWindowOpened += OnParryWindowOpened;
        parrySystem.OnParryWindowClosed += OnParryWindowClosed;
        parrySystem.OnParrySuccess += OnParrySuccess;
        parrySystem.OnParryFailed += OnParryFailed;
        
        // Initialize UI state
        if (parryWindowIndicator != null)
            parryWindowIndicator.SetActive(false);
            
        if (parryStatusText != null)
            parryStatusText.text = "";
    }
    
    private void OnDestroy()
    {
        if (parrySystem != null)
        {
            parrySystem.OnParryWindowOpened -= OnParryWindowOpened;
            parrySystem.OnParryWindowClosed -= OnParryWindowClosed;
            parrySystem.OnParrySuccess -= OnParrySuccess;
            parrySystem.OnParryFailed -= OnParryFailed;
        }
    }
    
    private void Update()
    {
        if (parryTimerBar != null)
        {
            if (isTrackingAttack)
            {
                // Show countdown for the entire attack duration
                float elapsedTime = Time.time - attackStartTime;
                float normalizedTime = 1f - (elapsedTime / totalAttackTime);
                parryTimerBar.fillAmount = Mathf.Clamp01(normalizedTime);
            }
            else if (parrySystem != null && parrySystem.IsParryWindowActive)
            {
                // Show parry window countdown (backup in case attack tracking fails)
                float remainingTime = parrySystem.GetRemainingParryTime();
                float normalizedTime = remainingTime / parrySystem.ParryWindowDuration;
                parryTimerBar.fillAmount = normalizedTime;
            }
        }
    }
    
    private void OnParryWindowOpened()
    {
        wasMyParryWindowActive = true;
        
        if (parryWindowIndicator != null)
        {
            parryWindowIndicator.SetActive(true);
        }
        
        if (parryTimerBar != null)
        {
            parryTimerBar.color = parryActiveColor;
            // Don't set fillAmount to 1.0f here anymore since we're tracking the full attack
        }
    }
    
    private void OnParryWindowClosed()
    {
        // Don't immediately set wasMyParryWindowActive to false here
        // Let the success/failed events handle it first
        isTrackingAttack = false;
        
        if (parryWindowIndicator != null)
        {
            parryWindowIndicator.SetActive(false);
        }
        
        if (parryTimerBar != null)
        {
            parryTimerBar.fillAmount = 0.0f;
        }
        
        // Delay clearing the flag to allow success/failed events to process
        StartCoroutine(ClearParryWindowFlagDelayed());
    }
    
    private System.Collections.IEnumerator ClearParryWindowFlagDelayed()
    {
        yield return new WaitForEndOfFrame(); // Wait one frame
        wasMyParryWindowActive = false;
    }
    
    private void OnParrySuccess()
    {
        if (wasMyParryWindowActive)
        {
            ShowFeedbackMessage("PARRY!", parrySuccessColor);
            Debug.Log($"[ParryVisualFeedback] Showing PARRY SUCCESS on {gameObject.name}");
        }
        else
        {
            Debug.Log($"[ParryVisualFeedback] Not showing PARRY SUCCESS on {gameObject.name} - wasMyParryWindowActive: {wasMyParryWindowActive}");
        }
    }
    
    private void OnParryFailed()
    {
        if (wasMyParryWindowActive)
        {
            ShowFeedbackMessage("FAILED", parryFailedColor);
            Debug.Log($"[ParryVisualFeedback] Showing PARRY FAILED on {gameObject.name}");
        }
        else
        {
            Debug.Log($"[ParryVisualFeedback] Not showing PARRY FAILED on {gameObject.name} - wasMyParryWindowActive: {wasMyParryWindowActive}");
        }
    }
    
    /// <summary>
    /// Call this when an enemy starts attacking this character
    /// </summary>
    public void StartAttackCountdown(float attackDuration)
    {
        isTrackingAttack = true;
        attackStartTime = Time.time;
        totalAttackTime = attackDuration;
        
        if (parryTimerBar != null)
        {
            parryTimerBar.fillAmount = 1.0f;
            parryTimerBar.color = parryActiveColor;
        }
        
        if (parryWindowIndicator != null)
        {
            parryWindowIndicator.SetActive(true);
        }
    }
    
    private void ShowFeedbackMessage(string message, Color color)
    {
        if (parryStatusText != null)
        {
            if (feedbackCoroutine != null)
            {
                StopCoroutine(feedbackCoroutine);
            }
            
            feedbackCoroutine = StartCoroutine(DisplayFeedbackCoroutine(message, color));
        }
        else
        {
            Debug.LogWarning($"[ParryVisualFeedback] parryStatusText is null on {gameObject.name}!");
        }
    }
    
    private System.Collections.IEnumerator DisplayFeedbackCoroutine(string message, Color color)
    {
        parryStatusText.text = message;
        parryStatusText.color = color;
        
        Debug.Log($"[ParryVisualFeedback] Displaying '{message}' for {feedbackDisplayTime}s on {gameObject.name}");
        
        yield return new WaitForSeconds(feedbackDisplayTime);
        
        parryStatusText.text = "";
        Debug.Log($"[ParryVisualFeedback] Cleared text on {gameObject.name}");
    }
}