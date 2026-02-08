using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CompanionHandler : MonoBehaviour
{
    [Header("References")]
    public Phi3Execution phi3Execution;
    
    [Header("UI References")]
    public TMP_InputField inputField;
    public Button submitButton;
    public TextMeshProUGUI responseText;
    public TextMeshProUGUI statusText;
    
    [Header("Manual Testing (Inspector)")]
    [TextArea(3, 5)]
    public string manualInput = "";
    
    [Header("Settings")]
    public bool useAIForStateParsing = false; // Set to true to use AI to determine state from text
    
    private CompanionState lastState = CompanionState.FOLLOW;

    void Start()
    {
        if (phi3Execution == null)
        {
            phi3Execution = GetComponent<Phi3Execution>();
            if (phi3Execution == null)
            {
                Debug.LogError("Phi4Execution component not found!");
                return;
            }
        }
        
        // Setup UI listeners if UI elements are assigned
        if (submitButton != null)
        {
            submitButton.onClick.AddListener(OnSubmitButtonClicked);
        }
        
        if (inputField != null)
        {
            inputField.onSubmit.AddListener((text) => OnSubmitButtonClicked());
        }
        
        UpdateStatusText("Ready. Waiting for input...");
    }

    void Update()
    {
        // Update status text if generating
        if (phi3Execution != null && phi3Execution.IsGenerating)
        {
            UpdateStatusText($"Generating response for: {phi3Execution.currentState}...");
        }
    }

    // Called from inspector button or UI button
    public void OnSubmitButtonClicked()
    {
        string input = "";
        
        // Use UI input field if available, otherwise use manual input
        if (inputField != null && !string.IsNullOrEmpty(inputField.text))
        {
            input = inputField.text;
            inputField.text = ""; // Clear input field
        }
        else if (!string.IsNullOrEmpty(manualInput))
        {
            input = manualInput;
        }
        else
        {
            Debug.LogWarning("No input provided!");
            UpdateStatusText("Error: No input provided!");
            return;
        }
        
        ProcessInput(input);
    }

    void ProcessInput(string input)
    {
        if (phi3Execution.IsGenerating)
        {
            Debug.LogWarning("AI is already generating a response. Please wait.");
            UpdateStatusText("Busy: Already generating...");
            return;
        }
        
        CompanionState detectedState;
        
        if (useAIForStateParsing)
        {
            // Use AI to parse the intent and determine state
            detectedState = ParseStateWithAI(input);
        }
        else
        {
            // Use keyword matching
            detectedState = ParseStateFromKeywords(input);
        }
        
        Debug.Log($"Input: '{input}' -> Detected State: {detectedState}");
        UpdateStatusText($"Command: '{input}' -> {detectedState}");
        
        lastState = detectedState;
        phi3Execution.SetState(detectedState);
        phi3Execution.GenerateResponse(detectedState);
        
        // Start coroutine to check for completion
        StartCoroutine(WaitForResponse());
    }

    CompanionState ParseStateFromKeywords(string input)
    {
        string lowerInput = input.ToLower();
        
        // FOLLOW keywords
        if (ContainsAny(lowerInput, new[] { "follow", "come with me", "stay close", "behind me", "come here" }))
        {
            return CompanionState.FOLLOW;
        }
        
        // PROTECT keywords
        if (ContainsAny(lowerInput, new[] { "protect", "guard", "defend", "shield", "watch my back", "cover me" }))
        {
            return CompanionState.PROTECT;
        }
        
        // HEAL keywords
        if (ContainsAny(lowerInput, new[] { "heal", "help", "hurt", "injured", "health", "restore", "fix me", "need healing" }))
        {
            return CompanionState.HEAL;
        }
        
        // RUN_AROUND keywords
        if (ContainsAny(lowerInput, new[] { "scout", "explore", "run around", "check area", "search", "patrol", "look around" }))
        {
            return CompanionState.RUN_AROUND;
        }
        
        // Default to last state if no keywords match
        Debug.LogWarning($"No keywords matched for: '{input}'. Using last state: {lastState}");
        return lastState;
    }

    CompanionState ParseStateWithAI(string input)
    {
        // Create a special prompt to have the AI determine the intent
        string systemPrompt = "<|im_start|>system\nYou are a command parser. Respond with ONLY one word: FOLLOW, PROTECT, HEAL, or RUN_AROUND based on the user's intent.<|im_end|>\n";
        string userPrompt = $"<|im_start|>user\n{input}<|im_end|>\n<|im_start|>assistant\n";
        
        // This is a simplified version - you'd need to modify Phi4Execution to support custom prompts
        // For now, fall back to keyword matching
        Debug.LogWarning("AI parsing not fully implemented yet. Using keyword matching.");
        return ParseStateFromKeywords(input);
    }

    bool ContainsAny(string text, string[] keywords)
    {
        foreach (string keyword in keywords)
        {
            if (text.Contains(keyword))
            {
                return true;
            }
        }
        return false;
    }

    System.Collections.IEnumerator WaitForResponse()
    {
        yield return new WaitUntil(() => !phi3Execution.IsGenerating);
        
        string response = phi3Execution.GetLastResponse();
        Debug.Log($"AI Response: {response}");
        
        if (responseText != null)
        {
            responseText.text = $"<b>{lastState}:</b>\n{response}";
        }
        
        UpdateStatusText($"Complete: {lastState}");
    }

    void UpdateStatusText(string status)
    {
        if (statusText != null)
        {
            statusText.text = status;
        }
    }

    // Public methods for external scripts or UI buttons
    public void SetFollowState()
    {
        ProcessInput("follow me");
    }

    public void SetProtectState()
    {
        ProcessInput("protect me");
    }

    public void SetHealState()
    {
        ProcessInput("heal me");
    }

    public void SetRunAroundState()
    {
        ProcessInput("scout the area");
    }

    // Debug helpers
    void OnValidate()
    {
        // Auto-find Phi4Execution if not assigned
        if (phi3Execution == null)
        {
            phi3Execution = GetComponent<Phi3Execution>();
        }
    }
}