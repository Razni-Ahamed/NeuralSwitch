using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeuralSwitch.Core.Engine;

/// <summary>One answer from a running game's engine: what Neural Rendering is set to and what it is doing.</summary>
public sealed record EngineState
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public bool Ok { get; init; }

    /// <summary>For a change: whether the game applied it before answering. False while the game is not rendering;
    /// the change then lands on its next frame.</summary>
    public bool? Applied { get; init; }

    public string? Error { get; init; }
    public int Protocol { get; init; }
    public int Pid { get; init; }
    public string Exe { get; init; } = "";

    /// <summary>The game has drawn a frame in the last second. False while paused, minimised or loading.</summary>
    public bool Rendering { get; init; }

    public bool Enabled { get; init; }
    public NrStyle Style { get; init; }
    public float Intensity { get; init; } = 1.0f;

    /// <summary>The model is loaded and running on the game's frames.</summary>
    public bool Running { get; init; }

    /// <summary>Why the model is not running, when it tried and failed. Empty otherwise.</summary>
    public string Failure { get; init; } = "";

    public string Mode { get; init; } = "";

    /// <summary>The engine's comparison mode: "yours", "vanilla" or "off".</summary>
    public string Compare { get; init; } = "yours";

    public double Fps { get; init; }

    [JsonPropertyName("nrMs")]
    public double NrMs { get; init; }

    public static EngineState Parse(string line) =>
        JsonSerializer.Deserialize<EngineState>(line, Json) ?? throw new EngineException("The game sent an empty answer.");
}

public sealed class EngineException(string message, Exception? inner = null) : Exception(message, inner);
