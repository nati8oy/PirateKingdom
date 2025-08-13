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
    
    private void Awake()
    {
        parrySystem = FindObjectOfType<ParrySystem>();
        
        if (parrySystem == null)
        {
            Debug.LogWarning("[ParryVisualFeedback] No ParrySystem found in scene!");
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
        if (parrySystem != null && parrySystem.IsParryWindowActive && parryTimerBar != null)
        {
            float remainingTime = parrySystem.GetRemainingParryTime();
            float normalizedTime = remainingTime / parrySystem.ParryWindowDuration;
            parryTimerBar.fillAmount = normalizedTime;
        }
    }
    
    private void OnParryWindowOpened()
    {
        if (parryWindowIndicator != null)
        {
            parryWindowIndicator.SetActive(true);
        }
        
        if (parryTimerBar != null)
        {
            parryTimerBar.color = parryActiveColor;
            parryTimerBar.fillAmount = 1.0f;
        }
    }
    
    private void OnParryWindowClosed()
    {
        if (parryWindowIndicator != null)
        {
            parryWindowIndicator.SetActive(false);
        }
        
        if (parryTimerBar != null)
        {
            parryTimerBar.fillAmount = 0.0f;
        }
    }
    
    private void OnParrySuccess()
    {
        ShowFeedbackMessage("PARRY SUCCESS!", parrySuccessColor);
    }
    
    private void OnParryFailed()
    {
        ShowFeedbackMessage("PARRY FAILED", parryFailedColor);
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
    }
    
    private System.Collections.IEnumerator DisplayFeedbackCoroutine(string message, Color color)
    {
        parryStatusText.text = message;
        parryStatusText.color = color;
        
        yield return new WaitForSeconds(feedbackDisplayTime);
        
        parryStatusText.text = "";
    }
}