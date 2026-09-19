using Lapper.Context.Windows;
using Lapper.Context.Windows.Blocks;
using Lapper.Context.Windows.Uia;
using Lapper.Privacy.Exclusions;
using Lapper.Privacy.Redaction;
using Xunit;

namespace Lapper.Context.Windows.Tests;

/// <summary>
/// Proves the pipeline's security ordering with recording fakes: exclusion
/// strictly before any capture, no pixels without a completed password
/// check, and the post-redaction sensitive-content gate.
/// </summary>
public class AcquisitionPipelineTests
{
    private sealed class FakeExclusionPolicy(bool blocked) : IExclusionPolicy
    {
        public ExclusionDecision Evaluate(WindowIdentity window) =>
            blocked
                ? new ExclusionDecision(true, ExclusionReason.BuiltInSensitiveApp, "test")
                : ExclusionDecision.Allowed;
    }

    private sealed class RecordingUiaStage : IUiaStage
    {
        public int Calls;
        public bool ThrowTimeout;
        public UiaExtraction Result = new(
            UiaOutcome.Success, null,
            [new RawBlock(ContextBlockRoles.Document, new string('x', 200), false, 0)],
            false, 0, false, PasswordCheckCompleted: true);

        public Task<UiaExtraction> ExtractAsync(nint hwnd, bool isBrowser, CancellationToken ct)
        {
            Calls++;
            return ThrowTimeout
                ? throw new CaptureTimeoutException()
                : Task.FromResult(Result);
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingPixelStage : IPixelStage
    {
        public int Calls;
        public bool OcrAvailable => true;

        public Task<IReadOnlyList<RawBlock>?> CaptureAndRecognizeAsync(nint hwnd)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<RawBlock>?>(
                [new RawBlock(ContextBlockRoles.OcrParagraph, "recognized text from pixels, long enough to rank", false, 0)]);
        }
    }

    private static ForegroundWindowInfo Probe() => new(
        0x1234,
        new WindowIdentity(@"C:\apps\some.exe", "some.exe", 42, "A window", "cls"));

    private static ContextAcquisitionService Service(
        bool blocked, RecordingUiaStage uia, RecordingPixelStage pixels) =>
        new(new FakeExclusionPolicy(blocked), new SecretRedactor(), uia, pixels);

    [Fact]
    public async Task ExcludedAppInvokesNoCaptureStageAtAll()
    {
        var uia = new RecordingUiaStage();
        var pixels = new RecordingPixelStage();
        var result = await Service(blocked: true, uia, pixels).AcquireAsync(Probe(), CancellationToken.None);

        Assert.Equal(AcquisitionOutcome.ExcludedApp, result.Outcome);
        Assert.Equal(0, uia.Calls);
        Assert.Equal(0, pixels.Calls);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task UiaTimeoutNeverFallsBackToPixels()
    {
        var uia = new RecordingUiaStage { ThrowTimeout = true };
        var pixels = new RecordingPixelStage();
        var result = await Service(blocked: false, uia, pixels).AcquireAsync(Probe(), CancellationToken.None);

        // Password status is unknown when UIA never answered: fail closed.
        Assert.Equal(0, pixels.Calls);
        Assert.Equal(AcquisitionOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task IncompletePasswordCheckNeverFallsBackToPixels()
    {
        var uia = new RecordingUiaStage
        {
            Result = new UiaExtraction(
                UiaOutcome.NothingExtracted, null, [], false, 0, false, PasswordCheckCompleted: false),
        };
        var pixels = new RecordingPixelStage();
        var result = await Service(blocked: false, uia, pixels).AcquireAsync(Probe(), CancellationToken.None);

        Assert.Equal(1, uia.Calls);
        Assert.Equal(0, pixels.Calls);
        Assert.Equal(AcquisitionOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task FocusedPasswordBlocksWithoutPixels()
    {
        var uia = new RecordingUiaStage
        {
            Result = new UiaExtraction(
                UiaOutcome.SensitiveBlocked, null, [], false, 1, false, PasswordCheckCompleted: true),
        };
        var pixels = new RecordingPixelStage();
        var result = await Service(blocked: false, uia, pixels).AcquireAsync(Probe(), CancellationToken.None);

        Assert.Equal(AcquisitionOutcome.SensitiveContentBlocked, result.Outcome);
        Assert.Equal(0, pixels.Calls);
    }

    [Fact]
    public async Task SufficientUiaTextSkipsPixelsEntirely()
    {
        var uia = new RecordingUiaStage(); // one 200-char document block
        var pixels = new RecordingPixelStage();
        var result = await Service(blocked: false, uia, pixels).AcquireAsync(Probe(), CancellationToken.None);

        Assert.Equal(AcquisitionOutcome.Success, result.Outcome);
        Assert.Equal(0, pixels.Calls);
        Assert.NotNull(result.Snapshot);
    }

    [Fact]
    public async Task SparseUiaTextTriggersOcrFallback()
    {
        var uia = new RecordingUiaStage
        {
            Result = new UiaExtraction(
                UiaOutcome.Success, null,
                [new RawBlock(ContextBlockRoles.Text, "tiny", false, 0)],
                false, 0, false, PasswordCheckCompleted: true),
        };
        var pixels = new RecordingPixelStage();
        var result = await Service(blocked: false, uia, pixels).AcquireAsync(Probe(), CancellationToken.None);

        Assert.Equal(1, pixels.Calls);
        Assert.NotNull(result.Snapshot);
        Assert.True(result.Snapshot!.Flags.UsedOcr);
        Assert.NotNull(result.Snapshot.OcrText);
    }

    [Fact]
    public async Task PrivateKeyOnScreenBlocksTheWholeCapture()
    {
        var uia = new RecordingUiaStage
        {
            Result = new UiaExtraction(
                UiaOutcome.Success, null,
                [new RawBlock(
                    ContextBlockRoles.Document,
                    "-----BEGIN RSA PRIVATE KEY-----\nkeymaterial\n-----END RSA PRIVATE KEY-----\n" +
                    new string('x', 200),
                    false, 0)],
                false, 0, false, PasswordCheckCompleted: true),
        };
        var pixels = new RecordingPixelStage();
        var result = await Service(blocked: false, uia, pixels).AcquireAsync(Probe(), CancellationToken.None);

        Assert.Equal(AcquisitionOutcome.SensitiveContentBlocked, result.Outcome);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task SecretSaturatedScreenBlocksTheWholeCapture()
    {
        var uia = new RecordingUiaStage
        {
            Result = new UiaExtraction(
                UiaOutcome.Success, null,
                [new RawBlock(
                    ContextBlockRoles.Document,
                    "password: one2three | passwd: fourfive6 | pwd: seven8nine " + new string('x', 100),
                    false, 0)],
                false, 0, false, PasswordCheckCompleted: true),
        };
        var pixels = new RecordingPixelStage();
        var result = await Service(blocked: false, uia, pixels).AcquireAsync(Probe(), CancellationToken.None);

        Assert.Equal(AcquisitionOutcome.SensitiveContentBlocked, result.Outcome);
    }
}
