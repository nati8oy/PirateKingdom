using UnityEngine;

[CreateAssetMenu(fileName = "Enemy", menuName = "Scriptable Objects/Enemy")]
public class Enemy : Character
{
    [Header("AI Behavior Settings")]
    [SerializeField, Range(0.1f, 1.0f)] 
    public float healThreshold = 0.3f;
    
    [SerializeField] 
    public bool showDebugInfo = false;
    
    [Header("Action Weights (Higher = More Likely)")]
    [SerializeField, Range(0f, 1f)] 
    public float attackWeight = 0.7f;
    
    [SerializeField, Range(0f, 1f)] 
    public float healWeight = 0.9f; // High priority when needed
    
    [SerializeField, Range(0f, 1f)] 
    public float buffWeight = 0.15f;
    
    [SerializeField, Range(0f, 1f)] 
    public float debuffWeight = 0.2f;
    
    [Header("Randomization")]
    [SerializeField, Range(0f, 0.5f)] 
    public float randomnessAmount = 0.1f; // Add some unpredictability
}