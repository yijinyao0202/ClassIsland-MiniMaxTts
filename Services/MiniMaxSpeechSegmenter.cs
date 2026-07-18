using System.Globalization;
using System.Text;

namespace ClassIsland.MiniMaxTts.Services;

internal static class MiniMaxSpeechSegmenter
{
    private static readonly HashSet<char> SentenceBoundaries =
    [
        '，', '。', '！', '？', '；', '：', '、',
        ',', '.', '!', '?', ';', ':'
    ];

    public static IReadOnlyList<string> Segment(string text, IReadOnlyCollection<string> cachedPhrases)
    {
        var candidates = cachedPhrases
            .Where(IsReusablePhrase)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(x => x.Length)
            .ToArray();
        var segments = new List<string>();

        foreach (var explicitSegment in SplitOnExplicitBoundaries(text))
        {
            SplitByCachedPhrases(explicitSegment, candidates, segments);
        }

        return segments;
    }

    private static IReadOnlyList<string> SplitOnExplicitBoundaries(string text)
    {
        var segments = new List<string>();
        var builder = new StringBuilder();

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (SentenceBoundaries.Contains(character))
            {
                builder.Append(character);
                Flush(builder, segments);
                continue;
            }

            if (!char.IsWhiteSpace(character))
            {
                builder.Append(character);
                continue;
            }

            if (IsChineseWhitespaceBoundary(text, index))
            {
                Flush(builder, segments);
            }
            else if (builder.Length > 0 && builder[^1] != ' ')
            {
                builder.Append(' ');
            }
        }

        Flush(builder, segments);
        return segments;
    }

    private static void SplitByCachedPhrases(string text, IReadOnlyList<string> candidates, ICollection<string> segments)
    {
        if (candidates.Count == 0)
        {
            AddSegment(text, segments);
            return;
        }

        var position = 0;
        while (position < text.Length)
        {
            var match = FindNextMatch(text, position, candidates);
            if (match is null)
            {
                AddSegment(text[position..], segments);
                return;
            }

            if (match.Index > position)
            {
                AddSegment(text[position..match.Index], segments);
            }

            AddSegment(match.Phrase, segments);
            position = match.Index + match.Phrase.Length;
        }
    }

    private static PhraseMatch? FindNextMatch(string text, int startIndex, IReadOnlyList<string> candidates)
    {
        PhraseMatch? bestMatch = null;
        foreach (var candidate in candidates)
        {
            var candidateIndex = startIndex;
            while (candidateIndex < text.Length)
            {
                candidateIndex = text.IndexOf(candidate, candidateIndex, StringComparison.Ordinal);
                if (candidateIndex < 0)
                {
                    break;
                }
                if (IsValidMatchBoundary(text, candidateIndex, candidate))
                {
                    break;
                }
                candidateIndex++;
            }

            if (candidateIndex < 0 ||
                bestMatch is not null && candidateIndex > bestMatch.Index ||
                bestMatch is not null && candidateIndex == bestMatch.Index && candidate.Length <= bestMatch.Phrase.Length)
            {
                continue;
            }

            bestMatch = new PhraseMatch(candidateIndex, candidate);
        }

        return bestMatch;
    }

    private static bool IsValidMatchBoundary(string text, int index, string phrase)
    {
        if (index > 0 && IsAsciiWordCharacter(text[index - 1]) && IsAsciiWordCharacter(phrase[0]))
        {
            return false;
        }

        var endIndex = index + phrase.Length;
        return endIndex >= text.Length ||
               !IsAsciiWordCharacter(text[endIndex]) ||
               !IsAsciiWordCharacter(phrase[^1]);
    }

    private static bool IsChineseWhitespaceBoundary(string text, int whitespaceIndex)
    {
        var previousIndex = whitespaceIndex - 1;
        while (previousIndex >= 0 && char.IsWhiteSpace(text[previousIndex]))
        {
            previousIndex--;
        }

        var nextIndex = whitespaceIndex + 1;
        while (nextIndex < text.Length && char.IsWhiteSpace(text[nextIndex]))
        {
            nextIndex++;
        }

        return previousIndex >= 0 &&
               nextIndex < text.Length &&
               IsCjkCharacter(text[previousIndex]) &&
               IsCjkCharacter(text[nextIndex]);
    }

    private static bool IsReusablePhrase(string phrase)
    {
        var meaningfulText = new string(phrase
            .Where(x => !char.IsWhiteSpace(x) && !SentenceBoundaries.Contains(x))
            .ToArray());
        return StringInfo.ParseCombiningCharacters(meaningfulText).Length >= 2;
    }

    private static bool IsCjkCharacter(char character) =>
        character is >= '\u3400' and <= '\u4dbf' or
        >= '\u4e00' and <= '\u9fff' or
        >= '\uf900' and <= '\ufaff';

    private static bool IsAsciiWordCharacter(char character) =>
        character is >= 'a' and <= 'z' or
        >= 'A' and <= 'Z' or
        >= '0' and <= '9' or '_';

    private static void Flush(StringBuilder builder, ICollection<string> segments)
    {
        AddSegment(builder.ToString(), segments);
        builder.Clear();
    }

    private static void AddSegment(string text, ICollection<string> segments)
    {
        var segment = text.Trim();
        if (segment.Any(x => !char.IsWhiteSpace(x) && !SentenceBoundaries.Contains(x)))
        {
            segments.Add(segment);
        }
    }

    private sealed record PhraseMatch(int Index, string Phrase);
}
