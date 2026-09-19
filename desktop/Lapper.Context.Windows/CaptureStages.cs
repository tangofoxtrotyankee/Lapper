using Lapper.Context.Windows.Blocks;
using Lapper.Context.Windows.Capture;
using Lapper.Context.Windows.Uia;

namespace Lapper.Context.Windows;

/// <summary>
/// Seam over the UIA stage so the pipeline's security ordering (exclusion
/// before capture, no pixels without a completed password check) is
/// provable with recording fakes.
/// </summary>
public interface IUiaStage : IDisposable
{
    /// <exception cref="CaptureTimeoutException">Hard budget exceeded; the stage self-heals.</exception>
    Task<UiaExtraction> ExtractAsync(nint hwnd, bool isBrowser, CancellationToken ct);
}

/// <summary>Seam over the pixels stage (in-memory window capture + local OCR).</summary>
public interface IPixelStage
{
    bool OcrAvailable { get; }
    Task<IReadOnlyList<RawBlock>?> CaptureAndRecognizeAsync(nint hwnd);
}

internal sealed class UiaStage : IUiaStage
{
    private const int UiaHardTimeoutMs = 1500;

    private readonly CaptureThread _captureThread = new();
    private UiaClient? _client;
    private UiaExtractor? _extractor;

    public async Task<UiaExtraction> ExtractAsync(nint hwnd, bool isBrowser, CancellationToken ct)
    {
        try
        {
            return await _captureThread.RunAsync(
                () =>
                {
                    _client ??= new UiaClient();
                    _extractor ??= new UiaExtractor(_client);
                    return _extractor.Extract(hwnd, isBrowser);
                },
                TimeSpan.FromMilliseconds(UiaHardTimeoutMs),
                ct).ConfigureAwait(false);
        }
        catch (CaptureTimeoutException)
        {
            // Poisoned thread: the COM client dies with it, rebuilt lazily.
            _client = null;
            _extractor = null;
            throw;
        }
    }

    public void Dispose() => _captureThread.Dispose();
}

internal sealed class PixelStage : IPixelStage
{
    public bool OcrAvailable => OcrFallback.IsAvailable;

    public async Task<IReadOnlyList<RawBlock>?> CaptureAndRecognizeAsync(nint hwnd)
    {
        var frame = WindowCapture.Capture(hwnd, OcrFallback.MaxImageDimension);
        if (frame is null)
        {
            return null;
        }
        // OcrFallback disposes (zeroes) the frame in its own finally.
        var ocr = await OcrFallback.RunAsync(frame).ConfigureAwait(false);
        return ocr?.Blocks;
    }
}
