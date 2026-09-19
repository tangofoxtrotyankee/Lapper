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
    private int _generation;

    /// <summary>Fires (on a non-UI thread) when playback finishes or fails.</summary>
    public event EventHandler? PlaybackEnded;

    public SpeechService()
    {
        _player.MediaEnded += (_, _) => PlaybackEnded?.Invoke(this, EventArgs.Empty);
        _player.MediaFailed += (_, _) => PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Returns false when speech is unavailable (no voice, media failure).</summary>
    public async Task<bool> SpeakAsync(string text)
    {
        Stop();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        if (text.Length > MaxSpeechChars)
        {
            text = text[..MaxSpeechChars];
        }

        // Generation guard: overlapping SpeakAsync calls race at the await —
        // whichever synthesis finishes last must not play stale text, and
        // the loser's stream must be disposed, not leaked.
        var generation = ++_generation;
        try
        {
            var stream = await _synthesizer.SynthesizeTextToStreamAsync(text);
            if (generation != _generation)
            {
                stream.Dispose();
                return false;
            }
            _currentSource?.Dispose();
            _currentSource = MediaSource.CreateFromStream(stream, stream.ContentType);
            _player.Source = _currentSource;
            _player.Play();
            return true;
        }
        catch (Exception)
        {
            // Missing TTS voice or media-stack failure: read-aloud is
            // unavailable, never fatal to the app.
            return false;
        }
    }

    public void Stop()
    {
        _generation++;
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
