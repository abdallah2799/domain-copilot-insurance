using DomainCopilot.Application.Ocr;

namespace DomainCopilot.Application.Tests.Ocr;

internal sealed class FakeOcrEngine : IOcrEngine
{
    private readonly Queue<(string Text, double Confidence)> _results = new();

    public void Enqueue(string text, double confidence) => _results.Enqueue((text, confidence));

    public Task<(string Text, double ConfidencePercent)> RecognizeAsync(byte[] pngImage, CancellationToken cancellationToken = default) =>
        Task.FromResult(_results.Count > 0 ? _results.Dequeue() : ("", 0.0));
}
