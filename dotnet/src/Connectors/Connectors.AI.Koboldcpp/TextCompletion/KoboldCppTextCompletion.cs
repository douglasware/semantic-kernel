// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Diagnostics;

namespace Microsoft.SemanticKernel.Connectors.AI.KoboldCpp.TextCompletion;

/// <summary>
/// KoboldCpp text completion service API.
/// Adapted from <see href="https://github.com/koboldCpp/text-generation-webui/tree/main/api-examples"/>
/// </summary>
public sealed class KoboldCppTextCompletion : ITextCompletion
{
    public const string HttpUserAgent = "Microsoft-Semantic-Kernel";
    public const string BlockingUriPath = "/api/v1/generate";

    public static readonly Dictionary<string, Dictionary<string, object>> Presets = new()
    {
        {
            "[Default]", new Dictionary<string, object>()
            {
                { "description", "Known Working Settings." },
                { "temp", 0.7 },
                { "genamt", 80 },
                { "top_k", 0 },
                { "top_p", 0.92 },
                { "top_a", 0 },
                { "typical", 1 },
                { "tfs", 1 },
                { "rep_pen", 1.08 },
                { "rep_pen_range", 256 },
                { "rep_pen_slope", 0.7 },
                { "sampler_order", new List<int> { 6, 0, 1, 2, 3, 4, 5 } }
            }
        },
        {
            "Inverted Mirror", new Dictionary<string, object>()
            {
                { "description", "Good defaults with a different sampler order." },
                { "temp", 0.7 },
                { "genamt", 80 },
                { "top_k", 0 },
                { "top_p", 0.92 },
                { "top_a", 0 },
                { "typical", 1 },
                { "tfs", 1 },
                { "rep_pen", 1.08 },
                { "rep_pen_range", 256 },
                { "rep_pen_slope", 0.7 },
                { "sampler_order", new List<int> { 0, 1, 2, 3, 4, 5, 6 } }
            }
        },
        {
            "Godlike", new Dictionary<string, object>()
            {
                { "description","Makes AI give a descriptive and sensual output."},
                { "temp",0.7},
                { "genamt",80 },
                { "top_k",0 },
                { "top_p",0.5 },
                { "top_a",0.75},
                { "typical",0.19},
                { "tfs",0.97},
                { "rep_pen",1.1 },
                { "rep_pen_range",1024},
                { "rep_pen_slope",0.7 },
                { "sampler_order",new List<int> { 6, 5, 4, 3, 2, 1, 0 } }
            }
        },
        {
            "Mayday", new Dictionary<string, object>()
            {
                { "description","Wacky plot, creativity from AI, crazy stories you want AI to weird out."},
                { "temp",1.05},
                { "genamt",80 },
                { "top_k",0 },
                { "top_p",0.95 },
                { "top_a",0},
                { "typical",1},
                { "tfs",1},
                { "rep_pen",1.1 },
                { "rep_pen_range",1024},
                { "rep_pen_slope",0.7 },
                { "sampler_order",new List<int> { 6, 0, 1, 2, 3, 4, 5 }  }
            }
        },
        {
            "Good Winds", new Dictionary<string, object>()
            {
                { "description","Let AI direct the plot, but still stay logical."},
                { "temp",0.7},
                { "genamt",80 },
                { "top_k",0 },
                { "top_p",1 },
                { "top_a",0},
                { "typical",1},
                { "tfs",0.9},
                { "rep_pen",1.1 },
                { "rep_pen_range",1024},
                { "rep_pen_slope",0.7 },
                { "sampler_order",new List<int> { 6, 0, 1, 2, 3, 4, 5 }  }
            }
        },
        {
            "Liminal Drift", new Dictionary<string, object>()
            {
                { "description","Drives coherent dialogue, responses, and behavior, sometimes surreal situations arise based on information already present in the story."},
                { "temp",0.66},
                { "genamt",80 },
                { "top_k",0 },
                { "top_p",1 },
                { "top_a",0.96},
                { "typical",0.6},
                { "tfs",1},
                { "rep_pen",1.1 },
                { "rep_pen_range",1024},
                { "rep_pen_slope",0.7 },
                { "sampler_order",new List<int> { 6, 4, 5, 1, 0, 2, 3 }  }
            }
        },
        {
            "TavernAI", new Dictionary<string, object>()
            {
                { "description","Preset used in TavernAI."},
                { "temp",0.79},
                { "genamt",80 },
                { "top_k",0 },
                { "top_p",0.9 },
                { "top_a",0},
                { "typical",1},
                { "tfs",0.95},
                { "rep_pen",1.19 },
                { "rep_pen_range",1024},
                { "rep_pen_slope",0.9 },
                { "sampler_order",new List<int> { 6, 0, 1, 2, 3, 4, 5 }  }
            }
        },
        {
            "Storywriter 6B", new Dictionary<string, object>()
            {
                { "description","Optimized settings for relevant output."},
                { "temp",0.72},
                { "genamt",80 },
                { "top_k",0 },
                { "top_p",0.73 },
                { "top_a",0},
                { "typical",1},
                { "tfs",1},
                { "rep_pen",1.1 },
                { "rep_pen_range",1024},
                { "rep_pen_slope",0.2 },
                { "sampler_order",new List<int> { 6, 5, 0, 2, 3, 1, 4 }  }
            }
        },
        {
            "Coherent Creativity 6B", new Dictionary<string, object>()
            {
                { "description","A good balance between coherence, creativity, and quality of prose."},
                { "temp",0.51},
                { "genamt",80 },
                { "top_k",0 },
                { "top_p",1 },
                { "top_a",0},
                { "typical",1},
                { "tfs",0.99},
                { "rep_pen",1.2 },
                { "rep_pen_range",2048},
                { "rep_pen_slope",0 },
                { "sampler_order",new List<int> { 6, 5, 0, 2, 3, 1, 4 }  }
            }
        },
        {
            "Luna Moth 6B", new Dictionary<string, object>()
            {
                { "description","A great degree of creativity without losing coherency."},
                { "temp",1.5},
                { "genamt",80 },
                { "top_k",85 },
                { "top_p",0.24 },
                { "top_a",0},
                { "typical",1},
                { "tfs",1},
                { "rep_pen",1.2 },
                { "rep_pen_range",2048},
                { "rep_pen_slope",0 },
                { "sampler_order",new List<int> { 6, 5, 0, 2, 3, 1, 4 }  }
            }
        },
        {
            "Best Guess 6B", new Dictionary<string, object>()
            {
                { "description","A subtle change with alternative context settings."},
                { "temp",0.8},
                { "genamt",80 },
                { "top_k",100 },
                { "top_p",0.9 },
                { "top_a",0},
                { "typical",1},
                { "tfs",1},
                { "rep_pen",1.5 },
                { "rep_pen_range",2048},
                { "rep_pen_slope",3.4 },
                { "sampler_order",new List<int> { 6, 5, 0, 2, 3, 1, 4 }  }
            }
        },
        {
            "Pleasing Results 6B", new Dictionary<string, object>()
            {
                { "description","Expectable output with alternative context settings."},
                { "temp",0.44},
                { "genamt",80 },
                { "top_k",0 },
                { "top_p",1 },
                { "top_a",0},
                { "typical",1},
                { "tfs",1},
                { "rep_pen",1.5 },
                { "rep_pen_range",2048},
                { "rep_pen_slope",6.8 },
                { "sampler_order",new List<int> { 6, 5, 0, 2, 3, 1, 4 }  }
            }
        },
        {
            "Genesis 13B", new Dictionary<string, object>()
            {
                { "description","Stable and logical, but with scattered creativity."},
                { "temp",0.63},
                { "genamt",80 },
                { "top_k",0 },
                { "top_p",0.98 },
                { "top_a",0},
                { "typical",1},
                { "tfs",0.98},
                { "rep_pen",1.05 },
                { "rep_pen_range",2048},
                { "rep_pen_slope",0.1 },
                { "sampler_order",new List<int> { 6, 2, 0, 3, 5, 1, 4 }  }
            }
        },
        {
            "Basic Coherence 13B", new Dictionary<string, object>()
            {
                { "description","Keep things on track."},
                { "temp",0.59},
                { "genamt",80 },
                { "top_k",0 },
                { "top_p",1 },
                { "top_a",0},
                { "typical",1},
                { "tfs",0.87},
                { "rep_pen",1.1 },
                { "rep_pen_range",2048},
                { "rep_pen_slope",0.3 },
                { "sampler_order",new List<int> { 6, 5, 0, 2, 3, 1, 4 }  }
            }
        },
        {
            "Ouroboros 13B", new Dictionary<string, object>()
            {
                { "description","Versatile, conforms well to poems, lists, chat, etc."},
                { "temp",1.07},
                { "genamt",80 },
                { "top_k",100 },
                { "top_p",1 },
                { "top_a",0},
                { "typical",1},
                { "tfs",0.93},
                { "rep_pen",1.05 },
                { "rep_pen_range",404},
                { "rep_pen_slope",0.8 },
                { "sampler_order",new List<int> { 6, 0, 5, 3, 2, 1, 4 }  }
            }
        },
        {
            "Ace of Spades 13B", new Dictionary<string, object>()
            {
                { "description","Expressive, while still staying focused."},
                { "temp",1.15 },
                { "genamt",80 },
                { "top_k",0 },
                { "top_p",0.95 },
                { "top_a",0},
                { "typical",1},
                { "tfs",0.8},
                { "rep_pen",1.05 },
                { "rep_pen_range",2048},
                { "rep_pen_slope",7 },
                { "sampler_order",new List<int> { 6, 3, 2, 0, 5, 1, 4 }  }
            }
        },
        {
            "Low Rider 13B", new Dictionary<string, object>()
            {
                { "description","Reliable, aimed at story development."},
                { "temp",0.94 },
                { "genamt",80 },
                { "top_k",12 },
                { "top_p",1 },
                { "top_a",0},
                { "typical",1},
                { "tfs",0.94},
                { "rep_pen",1.05 },
                { "rep_pen_range",2048},
                { "rep_pen_slope",0.2 },
                { "sampler_order",new List<int> { 6, 5, 0, 2, 3, 1, 4 }  }
            }
        },
        {
            "Pro Writer 13B", new Dictionary<string, object>()
            {
                { "description","Optimal setting for readability, based on AI-powered mass statistical analysis of Euterpe output."},
                { "temp",1.35 },
                { "genamt",80 },
                { "top_k",0 },
                { "top_p",1 },
                { "top_a",0},
                { "typical",1},
                { "tfs",0.69},
                { "rep_pen",1.15 },
                { "rep_pen_range",2048},
                { "rep_pen_slope",0.1 },
                { "sampler_order",new List<int> { 6, 3, 2, 5, 0, 1, 4 }  }
            }
        },
        {
            "Default 20B", new Dictionary<string, object>()
            {
                { "description","Good starting settings for NeoX 20B."},
                { "temp",0.6 },
                { "genamt",80 },
                { "top_k",0 },
                { "top_p",0.9 },
                { "top_a",0},
                { "typical",1},
                { "tfs",1},
                { "rep_pen",1.04 },
                { "rep_pen_range",2048},
                { "rep_pen_slope",0.7 },
                { "sampler_order",new List<int> { 6, 0, 1, 2, 3, 4, 5 }  }
            }
        }
    };

    private readonly Dictionary<string, object> _preset;
    private readonly UriBuilder _blockingUri;
    private readonly HttpClient _httpClient;
    private readonly bool _useWebSocketsPooling;
    private readonly int _maxNbConcurrentWebSockets;
    private readonly SemaphoreSlim? _concurrentSemaphore;
    private readonly ConcurrentBag<bool>? _activeConnections;
    private readonly ConcurrentBag<ClientWebSocket> _webSocketPool = new();
    private readonly int _keepAliveWebSocketsDuration;
    private readonly ILogger? _logger;
    private long _lastCallTicks = long.MaxValue;

    /// <summary>
    /// Controls the size of the buffer used to received websocket packets
    /// </summary>
    public int WebSocketBufferSize { get; set; } = 2048;

    /// <summary>
    /// Initializes a new instance of the <see cref="KoboldCppTextCompletion"/> class.
    /// </summary>
    /// <param name="endpoint">The service API endpoint to which requests should be sent.</param>
    /// <param name="blockingPort">The port used for handling blocking requests. Default value is 5001</param>
    /// <param name="preset">Preset from Kobold Lite UI, default is "[Default]"</param>
    /// <param name="httpClient">Optional. The HTTP client used for making blocking API requests. If not specified, a default client will be used.</param>
    /// <param name="logger">Application logger</param>
    public KoboldCppTextCompletion(Uri endpoint,
        int blockingPort = 5001,
        Dictionary<string, object>? preset = null,
        HttpClient? httpClient = null,
        ILogger? logger = null)
    {
        Verify.NotNull(endpoint);

        _preset = preset ?? Presets["[Default]"];

        this._blockingUri = new UriBuilder(endpoint)
        {
            Port = blockingPort,
            Path = BlockingUriPath
        };

        this._activeConnections = new();
        this._maxNbConcurrentWebSockets = 0;

        this._httpClient = httpClient ?? new HttpClient(NonDisposableHttpClientHandler.Instance, disposeHandler: false);
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ITextStreamingResult> GetStreamingCompletionsAsync(
        string text,
        CompleteRequestSettings requestSettings,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {

        throw new NotImplementedException();
        yield return null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ITextResult>> GetCompletionsAsync(
        string text,
        CompleteRequestSettings requestSettings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await this.StartConcurrentCallAsync(cancellationToken).ConfigureAwait(false);

            //var completionRequest = this.CreateKoboldCppRequest(text, requestSettings);
            var requestDictionary = _preset.ToDictionary(k => k.Key, v => v.Value);
            requestDictionary["prompt"] = text;
            requestDictionary["n"] = 1;
            requestDictionary["max_context_length"] = 8192;
            requestDictionary["max_length"] = 4096; // requestSettings.MaxTokens ?? 1024;
            requestDictionary["stop_sequence"] = requestSettings.StopSequences;
            using var stringContent = new StringContent(
                JsonSerializer.Serialize(requestDictionary),
                Encoding.UTF8,
                "application/json");

            using var httpRequestMessage = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = this._blockingUri.Uri,
                Content = stringContent,
            };
            httpRequestMessage.Headers.Add("User-Agent", HttpUserAgent);
            _httpClient.Timeout = TimeSpan.FromMinutes(20);

            using var response = await this._httpClient.SendAsync(httpRequestMessage, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            TextCompletionResponse? completionResponse = JsonSerializer.Deserialize<TextCompletionResponse>(body);

            if (completionResponse is null)
            {
                throw new KoboldCppInvalidResponseException<string>(body, "Unexpected response from KoboldCpp API");
            }
            completionResponse.Results.First().Text = completionResponse.Results.First().Text.Replace("<|endoftext|>", "");
            return completionResponse.Results.Select(completionText => new TextCompletionResult(completionText)).ToList();
        }
        catch (Exception e) when (e is not AIException && !e.IsCriticalException())
        {
            throw new AIException(
                AIException.ErrorCodes.UnknownError,
                $"Something went wrong: {e.Message}", e);
        }
        finally
        {
            this.FinishConcurrentCall();
        }
    }

    #region private ================================================================================

    /// <summary>
    /// Creates an KoboldCpp request, mapping CompleteRequestSettings fields to their KoboldCpp API counter parts
    /// </summary>
    /// <param name="text">The text to complete.</param>
    /// <param name="requestSettings">The request settings.</param>
    /// <returns>An KoboldCpp TextCompletionRequest object with the text and completion parameters.</returns>
    private TextCompletionRequest CreateKoboldCppRequest(string text, CompleteRequestSettings requestSettings)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentNullException(nameof(text));
        }

        // Prepare the request using the provided parameters.
        return new TextCompletionRequest()
        {
            Prompt = text,
            MaxNewTokens = requestSettings.MaxTokens,
            Temperature = requestSettings.Temperature,
            TopP = requestSettings.TopP,
            RepetitionPenalty = GetRepetitionPenalty(requestSettings),
            StoppingStrings = requestSettings.StopSequences.ToList()
        };
    }

    /// <summary>
    /// Sets the options for the <paramref name="clientWebSocket"/>, either persistent and provided by the ctor, or transient if none provided.
    /// </summary>
    private void SetWebSocketOptions(ClientWebSocket clientWebSocket)
    {
        clientWebSocket.Options.SetRequestHeader("User-Agent", HttpUserAgent);
    }

    /// <summary>
    /// Converts the semantic-kernel presence penalty, scaled -2:+2 with default 0 for no penalty to the KoboldCpp repetition penalty, strictly positive with default 1 for no penalty. See <see href="https://github.com/koboldCpp/text-generation-webui/blob/main/docs/Generation-parameters.md"/>  and subsequent links for more details.
    /// </summary>
    private static double GetRepetitionPenalty(CompleteRequestSettings requestSettings)
    {
        return 1 + requestSettings.PresencePenalty / 2;
    }

    /// <summary>
    /// That method is responsible for processing the websocket messages that build a streaming response object. It is crucial that it is run asynchronously to prevent a deadlock with results iteration
    /// </summary>
    private async Task ProcessWebSocketMessagesAsync(ClientWebSocket clientWebSocket, TextCompletionStreamingResult streamingResult, CancellationToken cancellationToken)
    {
        var buffer = new byte[this.WebSocketBufferSize];
        var finishedProcessing = false;
        while (!finishedProcessing && !cancellationToken.IsCancellationRequested)
        {
            MemoryStream messageStream = new();
            WebSocketReceiveResult result;
            do
            {
                var segment = new ArraySegment<byte>(buffer);
                result = await clientWebSocket.ReceiveAsync(segment, cancellationToken).ConfigureAwait(false);
                await messageStream.WriteAsync(buffer, 0, result.Count, cancellationToken).ConfigureAwait(false);
            } while (!result.EndOfMessage);

            messageStream.Seek(0, SeekOrigin.Begin);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                string messageText;
                using (var reader = new StreamReader(messageStream, Encoding.UTF8))
                {
                    messageText = await reader.ReadToEndAsync().ConfigureAwait(false);
                }

                var responseObject = JsonSerializer.Deserialize<TextCompletionStreamingResponse>(messageText);

                if (responseObject is null)
                {
                    throw new KoboldCppInvalidResponseException<string>(messageText, "Unexpected response from KoboldCpp API");
                }

                switch (responseObject.Event)
                {
                    case TextCompletionStreamingResponse.ResponseObjectTextStreamEvent:
                        streamingResult.AppendResponse(responseObject);
                        break;
                    case TextCompletionStreamingResponse.ResponseObjectStreamEndEvent:
                        streamingResult.SignalStreamEnd();
                        if (!this._useWebSocketsPooling)
                        {
                            await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Acknowledge stream-end koboldCpp message", CancellationToken.None).ConfigureAwait(false);
                        }

                        finishedProcessing = true;
                        break;
                    default:
                        break;
                }
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                await clientWebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledge Close frame", CancellationToken.None).ConfigureAwait(false);
                finishedProcessing = true;
            }

            if (clientWebSocket.State != WebSocketState.Open)
            {
                finishedProcessing = true;
            }
        }
    }

    /// <summary>
    /// Starts a concurrent call, either by taking a semaphore slot or by pushing a value on the active connections stack
    /// </summary>
    /// <param name="cancellationToken"></param>
    private async Task StartConcurrentCallAsync(CancellationToken cancellationToken)
    {
        if (this._concurrentSemaphore != null)
        {
            await this._concurrentSemaphore!.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            this._activeConnections!.Add(true);
        }
    }

    /// <summary>
    /// Gets the number of concurrent calls, either by reading the semaphore count or by reading the active connections stack count
    /// </summary>
    /// <returns></returns>
    private int GetCurrentConcurrentCallsNb()
    {
        if (this._concurrentSemaphore != null)
        {
            return this._maxNbConcurrentWebSockets - this._concurrentSemaphore!.CurrentCount;
        }

        return this._activeConnections!.Count;
    }

    /// <summary>
    /// Ends a concurrent call, either by releasing a semaphore slot or by popping a value from the active connections stack
    /// </summary>
    private void FinishConcurrentCall()
    {
        if (this._concurrentSemaphore != null)
        {
            this._concurrentSemaphore!.Release();
        }
        else
        {
            this._activeConnections!.TryTake(out _);
        }

        Interlocked.Exchange(ref this._lastCallTicks, DateTime.UtcNow.Ticks);
    }

    private void StartCleanupTask(CancellationToken cancellationToken)
    {
        Task.Factory.StartNew(
            async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await this.FlushWebSocketClientsAsync(cancellationToken).ConfigureAwait(false);
                }
            },
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    /// <summary>
    /// Flushes the web socket clients that have been idle for too long
    /// </summary>
    /// <returns></returns>
    private async Task FlushWebSocketClientsAsync(CancellationToken cancellationToken)
    {
        // In the cleanup task, make sure you handle OperationCanceledException appropriately
        // and make frequent checks on whether cancellation is requested.
        try
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(this._keepAliveWebSocketsDuration, cancellationToken).ConfigureAwait(false);

                // If another call was made during the delay, do not proceed with flushing
                if (DateTime.UtcNow.Ticks - Interlocked.Read(ref this._lastCallTicks) < TimeSpan.FromMilliseconds(this._keepAliveWebSocketsDuration).Ticks)
                {
                    return;
                }

                while (this.GetCurrentConcurrentCallsNb() == 0 && this._webSocketPool.TryTake(out ClientWebSocket clientToDispose))
                {
                    await this.DisposeClientGracefullyAsync(clientToDispose).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException exception)
        {
            this._logger?.LogTrace(message: "FlushWebSocketClientsAsync cleaning task was cancelled", exception: exception);
            while (this._webSocketPool.TryTake(out ClientWebSocket clientToDispose))
            {
                await this.DisposeClientGracefullyAsync(clientToDispose).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Closes and disposes of a client web socket after use
    /// </summary>
    private async Task DisposeClientGracefullyAsync(ClientWebSocket clientWebSocket)
    {
        try
        {
            if (clientWebSocket.State == WebSocketState.Open)
            {
                await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing client before disposal", CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException exception)
        {
            this._logger?.LogTrace(message: "Closing client web socket before disposal was cancelled", exception: exception);
        }
        catch (WebSocketException exception)
        {
            this._logger?.LogTrace(message: "Closing client web socket before disposal raised web socket exception", exception: exception);
        }
        finally
        {
            clientWebSocket.Dispose();
        }
    }

    #endregion
}
