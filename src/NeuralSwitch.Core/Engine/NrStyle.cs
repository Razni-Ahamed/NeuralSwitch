namespace NeuralSwitch.Core.Engine;

/// <summary>The DLSS 5 Neural Rendering model's processing profiles, numbered as the engine numbers them.</summary>
public enum NrStyle
{
    /// <summary>The strongest look: more local contrast and deeper lighting.</summary>
    Default = 0,

    /// <summary>The same detail work with a gentler hand, closer to the game's own tones.</summary>
    Natural = 1,

    /// <summary>Less shine and over-processing, for a film-like look.</summary>
    Cinematic = 2,
}

public static class NrStyles
{
    public static IReadOnlyList<NrStyle> All { get; } = [NrStyle.Default, NrStyle.Natural, NrStyle.Cinematic];

    /// <summary>The name the pipe protocol uses.</summary>
    public static string WireName(this NrStyle style) => style switch
    {
        NrStyle.Default => "default",
        NrStyle.Natural => "natural",
        NrStyle.Cinematic => "cinematic",
        _ => throw new ArgumentOutOfRangeException(nameof(style)),
    };

    public static NrStyle Next(this NrStyle style) => (NrStyle)(((int)style + 1) % All.Count);

    public static bool TryParse(string? text, out NrStyle style)
    {
        foreach (var candidate in All)
        {
            if (string.Equals(text, candidate.WireName(), StringComparison.OrdinalIgnoreCase) ||
                text == ((int)candidate).ToString())
            {
                style = candidate;
                return true;
            }
        }

        style = NrStyle.Default;
        return false;
    }
}
