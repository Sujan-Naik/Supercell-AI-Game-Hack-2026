using System;
using System.Collections.Generic;
using System.Data;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization;
using Unity.InferenceEngine.Tokenization.Decoders;
using Unity.InferenceEngine.Tokenization.Mappers;
using Unity.InferenceEngine.Tokenization.Normalizers;
using Unity.InferenceEngine.Tokenization.Padding;
using Unity.InferenceEngine.Tokenization.PostProcessors;
using Unity.InferenceEngine.Tokenization.PostProcessors.Templating;
using Unity.InferenceEngine.Tokenization.PreTokenizers;
using Unity.InferenceEngine.Tokenization.Truncators;
using UnityEngine;

public class RunAllMiniLm : MonoBehaviour
{
    // Gets the vocab from the tokenizer json config file
    static Dictionary<string, int> BuildVocabulary(JObject config)
    {
        var output = new Dictionary<string, int>();
        var vocab = config["model"]["vocab"] as JObject;

        foreach (var (value, id) in vocab!)
            output[value] = id?.Value<int>() ?? throw new DataException($"No id for value {value}");

        var addedTokens = config["added_tokens"] as JArray;
        if (addedTokens != null)
        {
            foreach (var addedToken in addedTokens)
            {
                var content = addedToken["content"]!.Value<string>();
                if (!output.ContainsKey(content))
                {
                    var id = addedToken["id"]!.Value<int>();
                    output.Add(content, id);
                }
            }
        }

        return output;
    }

    // Gets the additional token configurations from the tokenizer json config file
    static IEnumerable<TokenConfiguration> GetAddedTokens(JObject config)
    {
        var addedTokens = config["added_tokens"] as JArray;
        foreach (var addedToken in addedTokens!)
        {
            var id = addedToken["id"]!.Value<int>();
            var value = addedToken["content"]!.Value<string>();
            var wholeWord = addedToken["single_word"]!.Value<bool>();
            var strip = (addedToken["lstrip"]!.Value<bool>() ? Direction.Left : Direction.None) |
                (addedToken["rstrip"]!.Value<bool>() ? Direction.Right : Direction.None);
            var normalized = addedToken["normalized"]!.Value<bool>();
            var special = addedToken["special"]!.Value<bool>();

            yield return new(id, value, wholeWord, strip, normalized, special);
        }
    }

    [SerializeField] TextAsset m_ConfigAsset;
    [SerializeField] string m_Input = @"In the bustling streets of Tokyo, 🏙️ a neon glow envelops the city, as cherry blossoms 🌸 dance in the wind, creating a mesmerizing spectacle.";

    ITokenizer m_Tokenizer;

    // Builds a tokenizer based on the configuration from Hugging Face.
    // https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/blob/main/tokenizer.json
    Tokenizer BuildTokenizer()
    {
        var config = JObject.Parse(m_ConfigAsset.text);

        var vocabulary = BuildVocabulary(config);
        var addedTokens = GetAddedTokens(config);

        vocabulary.TryGetValue("[CLS]", out var clsToken);
        vocabulary.TryGetValue("[SEP]", out var sepToken);
        vocabulary.TryGetValue("[PAD]", out var padToken);

        // Those component are based on the tokenizer.json config.
        {
            var model = new WordPieceMapper(vocabulary, "[UNK]", "##", 100);
            var normalizer = new BertNormalizer(
                cleanText: true, handleCjkChars: true, stripAccents: null, lowerCase: true);
            var preTokenizer = new BertPreTokenizer();
            var truncator = new LongestFirstTruncator(new RightDirectionRangeGenerator(), 128, 0);
            var postProcessor = new TemplatePostProcessor(
                new(Template.Parse("[CLS]:0 $A:0 [SEP]:0")),
                new(Template.Parse("[CLS]:0 $A:0 [SEP]:0 $B:1 [SEP]:1")),
                new[]
                {
                    ("[CLS]", clsToken), ("[SEP]", sepToken)
                });
            var padding =
                new RightPadding(new FixedPaddingSizeProvider(128), new(padToken, "[PAD]"));
            var decoder = new WordPieceDecoder("##", true);

            return new(
                model,
                normalizer: normalizer,
                preTokenizer: preTokenizer,
                truncator: truncator,
                postProcessor: postProcessor,
                paddingProcessor: padding,
                decoder: decoder,
                addedVocabulary: addedTokens);
        }
    }

    void Awake()
    {
        m_Tokenizer = BuildTokenizer();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        var result = m_Tokenizer.Encode(m_Input);

        Debug.Log("Ids = " + string.Join(" ", result.GetIds()));
        Debug.Log("Attention = " + string.Join(" ", result.GetAttentionMask()));
        Debug.Log("Type Type Ids = " + string.Join(" ", result.GetTypeIds()));
        Debug.Log("SpecialMask = " + string.Join(" ", result.GetSpecialMask()));
    }
}
