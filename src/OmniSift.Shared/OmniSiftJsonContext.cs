// ============================================================
// OmniSift.Shared — JSON Source Generation Context
// Compile-time serialization for all API DTOs
// ============================================================

using System.Text.Json.Serialization;
using OmniSift.Shared.DTOs;

namespace OmniSift.Shared;

/// <summary>
/// Source-generated JsonSerializerContext for all shared DTOs.
/// Opt-in to compile-time JSON serialization, eliminating runtime
/// reflection and enabling Native AOT compatibility.
/// </summary>
[JsonSerializable(typeof(AgentQueryRequest))]
[JsonSerializable(typeof(AgentQueryResponse))]
[JsonSerializable(typeof(ConversationMessage))]
[JsonSerializable(typeof(SourceCitation))]
[JsonSerializable(typeof(DataSourceDto))]
[JsonSerializable(typeof(List<DataSourceDto>))]
[JsonSerializable(typeof(WebIngestionRequest))]
[JsonSerializable(typeof(IngestionResponse))]
[JsonSerializable(typeof(List<ConversationMessage>))]
[JsonSerializable(typeof(List<SourceCitation>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
public partial class OmniSiftJsonContext : JsonSerializerContext { }
