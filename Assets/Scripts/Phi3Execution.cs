using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.InferenceEngine;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization;
using Unity.InferenceEngine.Tokenization.Decoders;
using Unity.InferenceEngine.Tokenization.Mappers;
using Unity.InferenceEngine.Tokenization.PreTokenizers;


public class Phi3Execution : MonoBehaviour
{
    [Header("Assets")]
    public ModelAsset modelAsset;
    public TextAsset tokenizerConfig;

    [Header("Settings")]
    public BackendType backend = BackendType.CPU; // REQUIRED for Phi-3 stability
    [Range(0.1f, 2f)] public float temperature = 0.8f;
    public int maxTokens = 64;
    public int maxSequenceLength = 128;

    [Header("State")]
    public CompanionState currentState = CompanionState.FOLLOW;

    [Header("Debug")]
    public bool enableDebugLogs = true;

    // Phi models EOS tokens
    private readonly int[] eosTokens = { 32000, 32001, 32007 };

    private Worker worker;
    private Tokenizer tokenizer;

    private readonly List<int> tokens = new();
    private int initialPromptLength;
    private bool isRunning;
    private bool isInitialized;

    private Coroutine generationCoroutine;
    private string fullText = "";

    // Persistent tensors (FIXED SIZE)
    private Tensor<int> inputIds;
    private Tensor<int> attentionMask;

    // ======================================================
    // Unity lifecycle
    // ======================================================

    void Start()
    {
        if (modelAsset == null || tokenizerConfig == null)
        {
            Debug.LogError("Missing ModelAsset or TokenizerConfig");
            return;
        }

        InitializeTokenizer();
        InitializeModel();
        AllocateTensors();
    }

    void OnDestroy()
    {
        worker?.Dispose();
        inputIds?.Dispose();
        attentionMask?.Dispose();
    }

    // ======================================================
    // Initialization
    // ======================================================

    void InitializeTokenizer()
    {
        tokenizer = BuildPhiTokenizer(tokenizerConfig.text);
        if (enableDebugLogs)
            Debug.Log("Tokenizer initialized");
    }

    void InitializeModel()
    {
        var model = ModelLoader.Load(modelAsset);

        foreach (var i in model.inputs)
            Debug.Log($"Model input: {i.name}");

        foreach (var o in model.outputs)
            Debug.Log($"Model output: {o}");

        worker = new Worker(model, backend);
        isInitialized = true;

        if (enableDebugLogs)
            Debug.Log("Model initialized");
    }

    void AllocateTensors()
    {
        inputIds = new Tensor<int>(new TensorShape(1, maxSequenceLength));
        attentionMask = new Tensor<int>(new TensorShape(1, maxSequenceLength));
    }

    // ======================================================
    // Public API
    // ======================================================

    public void SetState(CompanionState state)
    {
        if (state == currentState)
            return;

        currentState = state;

        if (isInitialized && !isRunning)
            GenerateResponse(state);
    }

    public void GenerateResponse(CompanionState state)
    {
        if (!isInitialized || isRunning)
            return;

        string prompt = BuildPromptForState(state);
        StartGeneration(prompt);
    }

    public bool IsGenerating => isRunning;
    public string GetLastResponse() => ExtractAssistantResponse();

    // ======================================================
    // Prompt
    // ======================================================

    string BuildPromptForState(CompanionState state)
    {
        string system =
            "<|system|>\n" +
            "You are a helpful AI companion. Respond concisely in 1–2 sentences.\n";

        string user = state switch
        {
            CompanionState.FOLLOW => "<|user|>\nFollow me.\n",
            CompanionState.PROTECT => "<|user|>\nProtect me.\n",
            CompanionState.HEAL => "<|user|>\nHeal me.\n",
            CompanionState.RUN_AROUND => "<|user|>\nScout ahead.\n",
            _ => ""
        };

        return system + user + "<|assistant|>\n";
    }

    // ======================================================
    // Generation
    // ======================================================

    void StartGeneration(string prompt)
    {
        fullText = prompt;
        tokens.Clear();

        tokens.AddRange(tokenizer.Encode(prompt).GetIds());

        if (tokens.Count >= maxSequenceLength)
        {
            Debug.LogError("Prompt too long for fixed buffer");
            return;
        }

        initialPromptLength = tokens.Count;
        isRunning = true;

        if (generationCoroutine != null)
            StopCoroutine(generationCoroutine);

        generationCoroutine = StartCoroutine(GenerationLoop());
    }

    IEnumerator GenerationLoop()
    {
        while (isRunning)
        {
            if (tokens.Count - initialPromptLength >= maxTokens)
            {
                StopGeneration("Max tokens reached");
                yield break;
            }

            yield return RunInferenceStep();
        }
    }

    IEnumerator RunInferenceStep()
    {
        FillBuffers();

        var iterator = worker.ScheduleIterable(inputIds, attentionMask);
        while (iterator.MoveNext())
            yield return null;

        var logitsTensor = worker.PeekOutput("logits") as Tensor<float>;
        if (logitsTensor == null)
        {
            StopGeneration("Logits missing");
            yield break;
        }

        using var logits = logitsTensor.ReadbackAndClone();

        int vocabSize = logits.shape[logits.shape.rank - 1];
        int seqLen = logits.shape.rank == 3 ? logits.shape[1] : 1;
        int offset = (seqLen - 1) * vocabSize;

        int nextToken = SampleWithTemperature(logits, offset, vocabSize);

        if (eosTokens.Contains(nextToken))
        {
            StopGeneration("EOS");
            yield break;
        }

        tokens.Add(nextToken);
        fullText += tokenizer.Decode(new[] { nextToken });

        yield return null;
    }

    // ======================================================
    // Buffer fill (FIXED SHAPE)
    // ======================================================

    void FillBuffers()
    {
        for (int i = 0; i < maxSequenceLength; i++)
        {
            if (i < tokens.Count)
            {
                inputIds[0, i] = tokens[i];
                attentionMask[0, i] = 1;
            }
            else
            {
                inputIds[0, i] = 0;
                attentionMask[0, i] = 0;
            }
        }
    }

    // ======================================================
    // Sampling
    // ======================================================

    int SampleWithTemperature(Tensor<float> logits, int offset, int vocabSize)
    {
        float max = float.MinValue;
        for (int i = 0; i < vocabSize; i++)
            max = Mathf.Max(max, logits[offset + i]);

        float sum = 0f;
        float r = UnityEngine.Random.value;

        for (int i = 0; i < vocabSize; i++)
        {
            float p = Mathf.Exp((logits[offset + i] - max) / temperature);
            sum += p;
            if (r <= sum)
                return i;
        }

        return vocabSize - 1;
    }

    // ======================================================
    // Tokenizer (Phi-3 compatible)
    // ======================================================

    Tokenizer BuildPhiTokenizer(string json)
    {
        var cfg = JObject.Parse(json);

        var vocab = cfg["model"]["vocab"]
            .ToObject<Dictionary<string, int>>();

        var merges = cfg["model"]["merges"]
            .Select(m =>
            {
                var parts = m.Value<string>().Split(' ');
                return new MergePair(parts[0], parts[1]);
            })
            .ToList();

        return new Tokenizer(
            new BpeMapper(vocab, merges),
            preTokenizer: new ByteLevelPreTokenizer(false),
            decoder: new ByteLevelDecoder()
        );
    }

    // ======================================================
    // Helpers
    // ======================================================

    void StopGeneration(string reason)
    {
        isRunning = false;

        if (enableDebugLogs)
        {
            Debug.Log($"Stopped: {reason}");
            Debug.Log(GetLastResponse());
        }
    }

    string ExtractAssistantResponse()
    {
        int idx = fullText.LastIndexOf("<|assistant|>\n");
        if (idx < 0) return "";
        return fullText.Substring(idx + 14).Trim();
    }
}
