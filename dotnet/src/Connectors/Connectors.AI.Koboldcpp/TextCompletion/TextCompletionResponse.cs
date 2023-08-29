// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Connectors.AI.KoboldCpp.TextCompletion;

/// <summary>
/// HTTP Schema for KoboldCpp completion response. Contains a list of results. Adapted from <see href="https://github.com/koboldCpp/text-generation-webui/blob/main/extensions/api/blocking_api.py"/>
/// </summary>
public sealed class TextCompletionResponse
{
    /// <summary>
    /// A field used by KoboldCpp to return results from the blocking API.
    /// </summary>
    [JsonPropertyName("results")]
    public List<TextCompletionResponseText> Results { get; set; } = new();
}

/// <summary>
/// HTTP Schema for an single KoboldCpp result as part of a completion response.
/// </summary>
public sealed class TextCompletionResponseText
{
    /// <summary>
    /// Completed text.
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; } = string.Empty;
}
