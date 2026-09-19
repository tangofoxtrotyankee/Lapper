using System.Runtime.InteropServices.WindowsRuntime;
using Lapper.Context.Windows.Blocks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Lapper.Context.Windows.Capture;

public sealed record OcrExtraction(IReadOnlyList<RawBlock> Blocks, string EngineLanguage);

/// <summary>
/// Local OCR over an in-memory frame using the built-in Windows OCR engine.
/// Returns null when no OCR language pack is installed (N/LTSC editions) —
/// the pipeline degrades to UIA-only rather than failing.
/// </summary>
public static class OcrFallback
{
    private const int MaxOcrChars = 8000;

    private static readonly Lazy<OcrEngine?> Engine = new(CreateEngine);

    public static int MaxImageDimension =>
        Engine.Value is null ? 0 : (int)OcrEngine.MaxImageDimension;

    public static bool IsAvailable => Engine.Value is not null;

    private static OcrEngine? CreateEngine()
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is not null)
        {
            return engine;
        }
        foreach (Language language in OcrEngine.AvailableRecognizerLanguages)
        {
            engine = OcrEngine.TryCreateFromLanguage(language);
            if (engine is not null)
            {
                return engine;
            }
        }
        return null;
    }

    public static async Task<OcrExtraction?> RunAsync(CapturedFrame frame)
    {
        var engine = Engine.Value;
        if (engine is null)
        {
            return null;
        }

        SoftwareBitmap? bitmap = null;
        try
        {
            bitmap = SoftwareBitmap.CreateCopyFromBuffer(
                frame.Bgra.AsBuffer(),
                BitmapPixelFormat.Bgra8,
                frame.Width,
                frame.Height,
                BitmapAlphaMode.Ignore);

            var result = await engine.RecognizeAsync(bitmap);

            var lines = new List<OcrLineInfo>(result.Lines.Count);
            foreach (var line in result.Lines)
            {
                double minX = double.MaxValue, minY = double.MaxValue, maxX = 0, maxY = 0;
                foreach (var word in line.Words)
                {
                    minX = Math.Min(minX, word.BoundingRect.X);
                    minY = Math.Min(minY, word.BoundingRect.Y);
                    maxX = Math.Max(maxX, word.BoundingRect.X + word.BoundingRect.Width);
                    maxY = Math.Max(maxY, word.BoundingRect.Y + word.BoundingRect.Height);
                }
                if (line.Text.Length > 0 && maxX > minX)
                {
                    lines.Add(new OcrLineInfo(line.Text, minX, minY, maxX - minX, maxY - minY));
                }
            }

            var blocks = OcrLineGrouper.Group(lines);
            var total = 0;
            var capped = new List<RawBlock>();
            foreach (var block in blocks)
            {
                if (total + block.Text.Length > MaxOcrChars)
                {
                    break;
                }
                total += block.Text.Length;
                capped.Add(block);
            }
            return new OcrExtraction(capped, engine.RecognizerLanguage.LanguageTag);
        }
        finally
        {
            bitmap?.Dispose();
            frame.Dispose(); // zeroes the pixel buffer
        }
    }
}
