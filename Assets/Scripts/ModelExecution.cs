using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Unity.InferenceEngine;
using SharpToken;
using UnityEditor;

public class ModelExecution : MonoBehaviour
{

    Worker m_Worker;

    private GptEncoding tokenizer;

    void LoadTokenizerFiles()
    {
        tokenizer = GptEncoding.GetEncoding("r50k_base");

    }

    public List<int> GetTokensFromString(string word)
    {
        return tokenizer.Encode(word);
    }

    public string DecodeTokens(IEnumerable<int> tokensIds)
    {
        return tokenizer.Decode(tokensIds.ToArray());
    }

    public Tensor<int> GetTensorFromString(string input)
    {
        var tokenIds = GetTokensFromString(input);

        var shape = new TensorShape(1, tokenIds.Count);
        var tensor = new Tensor<int>(shape, tokenIds.ToArray());

        return tensor;
    }

    string TensorToToken(Tensor<float> cpuTensor)
    {
        List<int> predictedTokens = new List<int>();

        for (int pos = 0; pos < cpuTensor.shape[1]; pos++)
        {
            int bestTokenId = 0;
            float bestLogit = cpuTensor[0, pos, 0];

            for (int v = 1; v < cpuTensor.shape[2]; v++)
            {
                float logit = cpuTensor[0, pos, v];
                if (logit > bestLogit)
                {
                    bestLogit = logit;
                    bestTokenId = v;
                }
            }
            predictedTokens.Add(bestTokenId);
        }

        return DecodeTokens(predictedTokens);
    }

    void PromptModel(string prompt)
    {
        var tokenIds = GetTokensFromString(prompt);
        int seqLength = tokenIds.Count;

        var inputTensor = new Tensor<int>(new TensorShape(1, seqLength), tokenIds.ToArray());
        var attentionMask = new Tensor<int>(new TensorShape(1, seqLength), 
            Enumerable.Repeat(1, seqLength).ToArray());
        

        m_Worker.Schedule(inputTensor, attentionMask);

        var outputTensor = m_Worker.PeekOutput() as Tensor<float>;
        var cpuTensor = outputTensor.ReadbackAndClone();
        
        Debug.Log($"Output shape: {cpuTensor.shape}");
        Debug.Log($"Output text:  {TensorToToken(cpuTensor)}");
        
        cpuTensor.Dispose();
        inputTensor.Dispose();
    }
    
    void OnEnable()
    {
        LoadTokenizerFiles();
        
        ModelAsset modelAsset = Resources.Load("gpt-2") as ModelAsset;
        var runtimeModel = ModelLoader.Load(modelAsset);

        m_Worker = new Worker(runtimeModel, BackendType.GPUCompute);
        
        PromptModel("Hi, how are you ?");
       
    }

    void Update()
    {
    }

    void OnDisable()
    {
        // Clean up Sentis resources.
        m_Worker.Dispose();
    }
}
