using System.Text.Json;
using DomainCopilot.Application.Retrieval;
using DomainCopilot.Domain.Documents;
using Microsoft.AspNetCore.Mvc;

namespace DomainCopilot.Api.Controllers;

/// <summary>
/// FR-2's retrieval surface: hybrid dense+keyword search over the knowledge corpus, with
/// version/date-aware filtering (ADR-0005) and a structured refusal signal for low-evidence
/// queries. Returns citations, not a synthesized answer — answer generation is a later, separate
/// concern (the agentic workflow, FR-4/FR-5) built on top of this.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class RetrievalController(HybridRetrievalService retrievalService, AskService askService, ILogger<RetrievalController> logger) : ControllerBase
{
    // Matches the JSON convention every other endpoint gets for free from AddJsonOptions in
    // Program.cs (camelCase, enums as strings) -- this controller writes raw SSE payloads directly
    // to the response body instead of returning an ActionResult the framework would serialize with
    // that configured convention, so it has to be applied explicitly here.
    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    // Some completion providers (found live: nvidia/nemotron-3.5-lightning's free OpenRouter
    // tier) stream a lengthy chain-of-thought "reasoning" field before any real answer content --
    // AskService.AskStreamAsync correctly only ever yields real content, so that reasoning phase is
    // a genuine, potentially long silent gap with zero events, not a bug. Left alone, a gap this
    // long can also read as a dead connection to an intermediary proxy or the browser itself. A
    // periodic SSE comment ping (the `:`-prefixed line the spec designates as "ignore this, keep
    // the connection open") keeps both honest without claiming any real progress happened.
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    [HttpPost("ask")]
    public async Task<ActionResult<AskResult>> Ask([FromBody] AskRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("question is required.");
        }

        return Ok(await askService.AskAsync(request, cancellationToken));
    }

    /// <summary>FR-6's SSE token streaming for ask+citations. A POST, not a GET, because the
    /// browser's native <c>EventSource</c> only supports GET with no request body -- the Angular
    /// client instead uses <c>fetch</c> with a <c>ReadableStream</c> reader, which also gives real
    /// cancellation for free: aborting the fetch closes the connection, which ASP.NET Core surfaces
    /// as <paramref name="cancellationToken"/> being canceled (bound to
    /// <see cref="HttpContext.RequestAborted"/>), which already flows into every downstream await
    /// in <see cref="AskService.AskStreamAsync"/> and the underlying completion-provider HTTP call.
    /// </summary>
    [HttpPost("ask/stream")]
    public async Task AskStream([FromBody] AskRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";

        try
        {
            var enumerator = askService.AskStreamAsync(request, cancellationToken).GetAsyncEnumerator(cancellationToken);
            await using (enumerator.ConfigureAwait(false))
            {
                var moveNextTask = enumerator.MoveNextAsync().AsTask();
                while (true)
                {
                    var heartbeat = Task.Delay(HeartbeatInterval, cancellationToken);
                    var completed = await Task.WhenAny(moveNextTask, heartbeat);

                    if (completed == heartbeat)
                    {
                        await Response.WriteAsync(": keep-alive\n\n", cancellationToken);
                        await Response.Body.FlushAsync(cancellationToken);
                        continue;
                    }

                    if (!await moveNextTask)
                    {
                        break;
                    }

                    var evt = enumerator.Current;
                    var (eventName, payload) = evt.Type switch
                    {
                        AskStreamEventType.Refused => ("refused", (object)new { message = evt.RefusalMessage, chunks = evt.Chunks }),
                        AskStreamEventType.Delta => ("delta", new { text = evt.DeltaText }),
                        AskStreamEventType.Done => ("done", new { citations = evt.Citations, chunks = evt.Chunks }),
                        _ => throw new InvalidOperationException($"Unhandled {nameof(AskStreamEventType)}: {evt.Type}"),
                    };

                    await Response.WriteAsync($"event: {eventName}\n", cancellationToken);
                    await Response.WriteAsync($"data: {JsonSerializer.Serialize(payload, SseJsonOptions)}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);

                    moveNextTask = enumerator.MoveNextAsync().AsTask();
                }
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // The client aborted (navigated away, canceled) -- nothing left to write to.
            // Info, not a warning/error, regardless of which exception shape this surfaced as (a
            // raw OperationCanceledException, or a CompletionProviderException wrapping one from
            // the completion-provider HTTP call being torn down mid-read when the connection closes
            // underneath it) -- checking the token directly, rather than the exception's type, is
            // what actually distinguishes "the client left" from a real provider failure below.
            logger.LogInformation("Ask stream for question '{Question}' was canceled by the client.", request.Question);
        }
        catch (Exception ex)
        {
            if (!Response.HasStarted)
            {
                Response.StatusCode = StatusCodes.Status500InternalServerError;
                return;
            }

            // The response already started as a 200 text/event-stream -- the status code can't
            // change at this point, so the only way to surface a genuine mid-stream failure to the
            // client is an SSE event of its own.
            await Response.WriteAsync("event: error\n", cancellationToken);
            await Response.WriteAsync($"data: {JsonSerializer.Serialize(new { message = ex.Message }, SseJsonOptions)}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<RetrievalResult>> Search(
        [FromQuery] string query,
        [FromQuery] int topK = 5,
        [FromQuery] DateOnly? dateOfLoss = null,
        [FromQuery] string? formVersion = null,
        [FromQuery] DocumentCategory? category = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("query is required.");
        }

        var result = await retrievalService.SearchAsync(
            new RetrievalQuery(query, topK, dateOfLoss, formVersion, category),
            cancellationToken);

        return Ok(result);
    }
}
