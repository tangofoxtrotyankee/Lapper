using System.Diagnostics;
using Lapper.Context.Windows.Blocks;
using Lapper.Context.Windows.Capture;
using Lapper.Context.Windows.Uia;
using Lapper.Contracts;
using Lapper.Privacy.Exclusions;
using Lapper.Privacy.Gate;
using Lapper.Privacy.Redaction;

namespace Lapper.Context.Windows;

public enum AcquisitionOutcome
{
    Success,
    Degraded,
    ExcludedApp,
    SensitiveContentBlocked,
    NoForegroundWindow,
    Failed,
}

/// <summary>Metadata-only stage timings; safe to log.</summary>
public sealed record AcquisitionTimings(int ProbeMs, int UiaMs, int OcrMs, int TotalMs);

/// <summary>Metadata-only flags; safe to log.</summary>
public sealed record AcquisitionFlags(
    bool UsedUia,
    bool UsedOcr,
    bool OcrUnavailable,
    bool TruncatedByBudget,
    int PasswordControlsSkipped,
    int RedactionCount,
    bool RetriedForAccessibilityWarmup);

public sealed record ContextSnapshot
{
    public required string ProcessName { get; init; }
    /// <summary>Wire category string ("email", "browser", ...).</summary>
    public required string Category { get; init; }
    /// <summary>LOCAL-ONLY: never serialized into API requests.</summary>
    public required string LocalWindowTitle { get; init; }
    public string? SelectedText { get; init; }
    public required IReadOnlyList<ContextBlock> Blocks { get; init; }
    public string? OcrText { get; init; }
    public required AcquisitionFlags Flags { get; init; }

    public override string ToString() =>
        $"ContextSnapshot(blocks={Blocks.Count}, selected={SelectedText?.Length ?? 0} chars)";
}

public sealed record ContextAcquisitionResult
{
    public required AcquisitionOutcome Outcome { get; init; }
    public ExclusionDecision? Exclusion { get; init; }
    public ContextSnapshot? Snapshot { get; init; }
    public required AcquisitionTimings Timings { get; init; }
}

public interface IContextAcquisitionService
{
    /// <summary>
    /// The probe MUST be taken before any Lapper window is activated —
    /// otherwise Lapper would capture itself.
    /// </summary>
    Task<ContextAcquisitionResult> AcquireAsync(ForegroundWindowInfo probe, CancellationToken ct);
}

/// <summary>
/// The Phase 2 pipeline (docs/02-architecture.md): probe → exclusion check
/// BEFORE any capture → UIA (password gate before text) → OCR fallback →
/// redaction → normalize → rank. Screen content only ever lives in memory
/// inside the returned snapshot.
/// </summary>
public sealed class ContextAcquisitionService(
    IExclusionPolicy exclusionPolicy,
    ISecretRedactor redactor) : IContextAcquisitionService, IDisposable
{
    private const int UiaHardTimeoutMs = 1500;
    private const int MinUsefulBlocks = 2;
    private const int MinUsefulChars = 80;

    private readonly CaptureThread _captureThread = new();
    private UiaClient? _uiaClient;
    private UiaExtractor? _uiaExtractor;

    public async Task<ContextAcquisitionResult> AcquireAsync(
        ForegroundWindowInfo probe,
        CancellationToken ct)
    {
        var total = Stopwatch.StartNew();
        var probeWatch = Stopwatch.StartNew();
        probeWatch.Stop();

        // Exclusion BEFORE any UIA, OCR or pixel access. Fail closed.
        var exclusion = exclusionPolicy.Evaluate(probe.Identity);
        if (exclusion.IsBlocked)
        {
            return Result(AcquisitionOutcome.ExcludedApp, exclusion, null,
                Timings((int)probeWatch.ElapsedMilliseconds, 0, 0, total));
        }

        var category = AppCategoryMap.Map(probe.Identity.ProcessName);
        var isBrowser = category == "browser";

        UiaExtraction extraction;
        var uiaWatch = Stopwatch.StartNew();
        try
        {
            extraction = await _captureThread.RunAsync(
                () =>
                {
                    _uiaClient ??= new UiaClient();
                    _uiaExtractor ??= new UiaExtractor(_uiaClient);
                    return _uiaExtractor.Extract(probe.Hwnd, isBrowser);
                },
                TimeSpan.FromMilliseconds(UiaHardTimeoutMs),
                ct).ConfigureAwait(false);
        }
        catch (CaptureTimeoutException)
        {
            // Poisoned thread: the client dies with it and is rebuilt lazily.
            _uiaClient = null;
            _uiaExtractor = null;
            extraction = new UiaExtraction(UiaOutcome.NothingExtracted, null, [], true, 0, false);
        }
        uiaWatch.Stop();
        ct.ThrowIfCancellationRequested();

        if (extraction.Outcome == UiaOutcome.SensitiveBlocked ||
            SensitiveContextGate.Evaluate(
                extraction.Outcome == UiaOutcome.SensitiveBlocked,
                extraction.PasswordControlsSkipped,
                0) == GateDecision.Block)
        {
            return Result(AcquisitionOutcome.SensitiveContentBlocked, exclusion, null,
                Timings((int)probeWatch.ElapsedMilliseconds, (int)uiaWatch.ElapsedMilliseconds, 0, total));
        }

        // OCR fallback only when UIA yielded too little.
        var ocrWatch = Stopwatch.StartNew();
        var ocrBlocks = new List<RawBlock>();
        var usedOcr = false;
        var ocrUnavailable = false;
        var uiaTextChars = extraction.Blocks.Sum(b => b.Text.Length);
        if (extraction.Blocks.Count < MinUsefulBlocks || uiaTextChars < MinUsefulChars)
        {
            if (!OcrFallback.IsAvailable)
            {
                ocrUnavailable = true;
            }
            else
            {
                var frame = WindowCapture.Capture(probe.Hwnd, OcrFallback.MaxImageDimension);
                if (frame is not null)
                {
                    var ocr = await OcrFallback.RunAsync(frame).ConfigureAwait(false);
                    if (ocr is not null)
                    {
                        ocrBlocks.AddRange(ocr.Blocks);
                        usedOcr = true;
                    }
                }
            }
        }
        ocrWatch.Stop();
        ct.ThrowIfCancellationRequested();

        // Redact secrets in EVERYTHING before normalization/ranking.
        var redactionCount = 0;
        RawBlock RedactBlock(RawBlock block)
        {
            var result = redactor.Redact(block.Text);
            redactionCount += result.RedactionCount;
            return block with { Text = result.Text };
        }
        var allBlocks = extraction.Blocks.Concat(ocrBlocks).Select(RedactBlock).ToList();
        string? selectedText = null;
        if (extraction.SelectedText is { } selection)
        {
            var redacted = redactor.Redact(selection);
            redactionCount += redacted.RedactionCount;
            selectedText = redacted.Text;
        }

        var normalized = BlockNormalizer.Normalize(allBlocks);
        var uiaRanked = BlockRanker.Rank(
            normalized.Where(b => b.Role != ContextBlockRoles.OcrParagraph).ToList());
        var ocrText = string.Join(
            "\n\n",
            normalized.Where(b => b.Role == ContextBlockRoles.OcrParagraph).Select(b => b.Text));

        if (uiaRanked.Count == 0 && ocrText.Length == 0 && selectedText is null)
        {
            return Result(AcquisitionOutcome.Failed, exclusion, null,
                Timings((int)probeWatch.ElapsedMilliseconds, (int)uiaWatch.ElapsedMilliseconds,
                    (int)ocrWatch.ElapsedMilliseconds, total));
        }

        var flags = new AcquisitionFlags(
            UsedUia: extraction.Blocks.Count > 0,
            UsedOcr: usedOcr,
            OcrUnavailable: ocrUnavailable,
            TruncatedByBudget: extraction.TruncatedByBudget,
            PasswordControlsSkipped: extraction.PasswordControlsSkipped,
            RedactionCount: redactionCount,
            RetriedForAccessibilityWarmup: extraction.RetriedForAccessibilityWarmup);

        var snapshot = new ContextSnapshot
        {
            ProcessName = probe.Identity.ProcessName,
            Category = category,
            LocalWindowTitle = probe.Identity.WindowTitle,
            SelectedText = selectedText,
            Blocks = uiaRanked,
            OcrText = ocrText.Length > 0 ? ocrText : null,
            Flags = flags,
        };

        var outcome = extraction.TruncatedByBudget || ocrUnavailable
            ? AcquisitionOutcome.Degraded
            : AcquisitionOutcome.Success;
        return Result(outcome, exclusion, snapshot,
            Timings((int)probeWatch.ElapsedMilliseconds, (int)uiaWatch.ElapsedMilliseconds,
                (int)ocrWatch.ElapsedMilliseconds, total));
    }

    private static ContextAcquisitionResult Result(
        AcquisitionOutcome outcome,
        ExclusionDecision? exclusion,
        ContextSnapshot? snapshot,
        AcquisitionTimings timings) =>
        new() { Outcome = outcome, Exclusion = exclusion, Snapshot = snapshot, Timings = timings };

    private static AcquisitionTimings Timings(int probeMs, int uiaMs, int ocrMs, Stopwatch total) =>
        new(probeMs, uiaMs, ocrMs, (int)total.ElapsedMilliseconds);

    public void Dispose() => _captureThread.Dispose();
}
