using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace Lapper.Actions;

/// <summary>
/// Local Windows TTS (read_aloud). No audio ever leaves the machine; the
/// synthesized stream lives in memory only. The MediaPlayer stays strongly
/// referenced for the lifetime of the service or playback silently stops.
/// </summary>
public sealed class SpeechService : IDisposable
{
    private const int MaxSpeechChars = 12000;

    private readonly SpeechSynthesizer _synthesizer = new();
    private readonly MediaPlayer _player = new();
    private MediaSource? _currentSource;

    /// <summary>Fires (on a non-UI thread) when playback finishes or fails.</summary>
    public event EventHandler? PlaybackEnded;

    public SpeechService()
    {
        _player.MediaEnded += (_, _) => PlaybackEnded?.Invoke(this, EventArgs.Empty);
        _player.MediaFailed += (_, _) => PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }

    public async Task SpeakAsync(string text)
    {
        Stop();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }
        if (text.Length > MaxSpeechChars)
        {
            text = text[..MaxSpeechChars];
        }

        var stream = await _synthesizer.SynthesizeTextToStreamAsync(text);
        _currentSource = MediaSource.CreateFromStream(stream, stream.ContentType);
        _player.Source = _currentSource;
        _player.Play();
    }

    public void Stop()
    {
        _player.Pause();
        _player.Source = null;
        _currentSource?.Dispose();
        _currentSource = null;
    }

    public void Dispose()
    {
        Stop();
        _player.Dispose();
        _synthesizer.Dispose();
    }
}
