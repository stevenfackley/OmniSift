// ============================================================
// OmniSift.Api — Local ONNX Embedding Service
// Implements IEmbeddingService using a local sentence-embedding
// model via Microsoft.ML.OnnxRuntime — no cloud API, no per-token cost.
// ============================================================

using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using OmniSift.Api.Options;
using Pgvector;

namespace OmniSift.Api.Services;

/// <summary>
/// Generates embeddings locally with an ONNX sentence-transformer model.
/// The model graph emits a token-level last_hidden_state; this service applies
/// the configured pooling (<see cref="PoolingStrategy"/>) + L2-normalization.
/// Holds a single thread-safe <c>InferenceSession</c> for the process lifetime,
/// so it is registered as a singleton.
/// </summary>
/// <remarks>
/// Fail-fast: the constructor throws if the model/vocab are missing or the model's
/// output width disagrees with <see cref="EmbeddingOptions.Dimensions"/>, so a
/// misconfigured ONNX provider refuses to start rather than silently degrading.
/// </remarks>
public sealed class OnnxEmbeddingService : IEmbeddingService, IDisposable
{
    private readonly EmbeddingOptions _opts;
    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;
    private readonly List<string> _inputNames;
    private readonly string _outputName;

    public OnnxEmbeddingService(IOptions<EmbeddingOptions> options, ILogger<OnnxEmbeddingService> logger)
    {
        _opts = options.Value;

        var modelPath = Path.GetFullPath(_opts.ModelPath);
        var vocabPath = Path.GetFullPath(_opts.TokenizerPath);

        if (!File.Exists(modelPath))
            throw new FileNotFoundException(
                $"ONNX embedding model not found at '{modelPath}'. Run scripts/fetch-embedding-model.ps1 or set Embedding:ModelPath.",
                modelPath);
        if (!File.Exists(vocabPath))
            throw new FileNotFoundException(
                $"Tokenizer vocab not found at '{vocabPath}'. Set Embedding:TokenizerPath to the model's vocab.txt.",
                vocabPath);

        _session = new InferenceSession(modelPath);
        _inputNames = [.. _session.InputMetadata.Keys];
        _outputName = _session.OutputMetadata.Keys.First();

        using (var vocab = File.OpenRead(vocabPath))
            _tokenizer = BertTokenizer.Create(vocab);

        // Fail-fast on a model/column dimension mismatch (e.g. a 768-dim model
        // pointed at a 384-dim pgvector column).
        var outDims = _session.OutputMetadata[_outputName].Dimensions;
        var hidden = outDims.Length > 0 ? outDims[^1] : -1;
        if (hidden > 0 && hidden != _opts.Dimensions)
            throw new InvalidOperationException(
                $"ONNX model output width {hidden} != configured Embedding:Dimensions {_opts.Dimensions}.");

        logger.LogInformation(
            "ONNX embeddings ready: {Model} dim={Dim} pooling={Pooling} inputs=[{Inputs}] output={Output}",
            Path.GetFileName(modelPath), _opts.Dimensions, _opts.Pooling, string.Join(",", _inputNames), _outputName);
    }

    /// <summary>Single-text path = QUERY path; the instruction prefix is applied here only.</summary>
    public Task<Vector> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var prefixed = string.IsNullOrEmpty(_opts.QueryInstruction) ? text : _opts.QueryInstruction + text;
        return Task.Run(() => Embed(prefixed), cancellationToken);
    }

    /// <summary>Batch path = DOCUMENT path; no instruction prefix (bge convention).</summary>
    public Task<IReadOnlyList<Vector>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var list = texts.ToList();
        if (list.Count == 0)
            return Task.FromResult<IReadOnlyList<Vector>>([]);

        return Task.Run(() =>
        {
            var results = new List<Vector>(list.Count);
            foreach (var t in list)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(Embed(t));
            }
            return (IReadOnlyList<Vector>)results;
        }, cancellationToken);
    }

    private Vector Embed(string text)
    {
        // BertTokenizer adds [CLS]…[SEP]; truncate to the model's max sequence length.
        var ids = _tokenizer.EncodeToIds(text);
        var count = Math.Min(ids.Count, _opts.MaxSequenceLength);

        var inputIds = new long[count];
        var attentionMask = new long[count];
        var tokenTypeIds = new long[count];
        for (var i = 0; i < count; i++)
        {
            inputIds[i] = ids[i];
            attentionMask[i] = 1L;
            tokenTypeIds[i] = 0L;
        }

        var shape = new[] { 1, count };
        var inputs = new List<NamedOnnxValue>(_inputNames.Count);
        foreach (var name in _inputNames)
        {
            var data = name switch
            {
                "attention_mask" => attentionMask,
                "token_type_ids" => tokenTypeIds,
                _ => inputIds
            };
            inputs.Add(NamedOnnxValue.CreateFromTensor(name, new DenseTensor<long>(data, shape)));
        }

        using var outputs = _session.Run(inputs);
        var lastHidden = outputs.First(o => o.Name == _outputName).AsTensor<float>().ToArray();

        var hidden = _opts.Dimensions;
        var pooled = _opts.Pooling == PoolingStrategy.Cls
            ? EmbeddingMath.ClsPool(lastHidden, hidden)
            : EmbeddingMath.MeanPool(lastHidden, OnesMask(count), hidden);

        return new Vector(EmbeddingMath.L2Normalize(pooled));
    }

    private static int[] OnesMask(int count)
    {
        var mask = new int[count];
        Array.Fill(mask, 1);
        return mask;
    }

    public void Dispose() => _session.Dispose();
}
