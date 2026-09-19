using System.Text;

namespace Lapper.Shell.Core;

/// <summary>
/// Extracts the "orientation" string field from a STREAMING JSON prefix.
/// The orient endpoint streams the model's structured-output JSON verbatim
/// as deltas; rendering them raw would show JSON to the user. This state
/// machine surfaces just the orientation sentence, character by character,
/// tolerating chunk boundaries anywhere — including mid-escape-sequence.
/// </summary>
public sealed class OrientationDeltaExtractor
{
    private enum State
    {
        SearchingKey,
        AwaitingColon,
        AwaitingOpeningQuote,
        InString,
        Escape,
        Unicode,
        Done,
    }

    private const string Key = "\"orientation\"";

    private readonly StringBuilder _text = new();
    private readonly StringBuilder _unicode = new(4);
    private State _state = State.SearchingKey;
    private int _keyIndex;

    /// <summary>The orientation text visible so far.</summary>
    public string Text => _text.ToString();

    public bool IsComplete => _state == State.Done;

    /// <summary>Feeds a raw delta; returns true if the visible text grew.</summary>
    public bool Feed(string chunk)
    {
        var grew = false;
        foreach (var c in chunk)
        {
            switch (_state)
            {
                case State.SearchingKey:
                    if (c == Key[_keyIndex])
                    {
                        _keyIndex++;
                        if (_keyIndex == Key.Length)
                        {
                            _state = State.AwaitingColon;
                        }
                    }
                    else
                    {
                        _keyIndex = c == Key[0] ? 1 : 0;
                    }
                    break;

                case State.AwaitingColon:
                    if (c == ':')
                    {
                        _state = State.AwaitingOpeningQuote;
                    }
                    else if (!char.IsWhiteSpace(c))
                    {
                        _state = State.SearchingKey;
                        _keyIndex = 0;
                    }
                    break;

                case State.AwaitingOpeningQuote:
                    if (c == '"')
                    {
                        _state = State.InString;
                    }
                    else if (!char.IsWhiteSpace(c))
                    {
                        _state = State.SearchingKey;
                        _keyIndex = 0;
                    }
                    break;

                case State.InString:
                    if (c == '\\')
                    {
                        _state = State.Escape;
                    }
                    else if (c == '"')
                    {
                        _state = State.Done;
                    }
                    else
                    {
                        _text.Append(c);
                        grew = true;
                    }
                    break;

                case State.Escape:
                    _state = State.InString;
                    switch (c)
                    {
                        case 'n':
                            _text.Append('\n');
                            grew = true;
                            break;
                        case 't':
                            _text.Append('\t');
                            grew = true;
                            break;
                        case 'r':
                            break;
                        case 'u':
                            _unicode.Clear();
                            _state = State.Unicode;
                            break;
                        default:
                            _text.Append(c); // \" \\ \/ etc.
                            grew = true;
                            break;
                    }
                    break;

                case State.Unicode:
                    _unicode.Append(c);
                    if (_unicode.Length == 4)
                    {
                        if (ushort.TryParse(
                                _unicode.ToString(),
                                System.Globalization.NumberStyles.HexNumber,
                                null,
                                out var code))
                        {
                            _text.Append((char)code);
                            grew = true;
                        }
                        _state = State.InString;
                    }
                    break;

                case State.Done:
                    return grew;
            }
        }
        return grew;
    }
}
