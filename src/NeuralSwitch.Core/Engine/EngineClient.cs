using System.Globalization;
using System.IO.Pipes;
using System.Text;

namespace NeuralSwitch.Core.Engine;

/// <summary>
/// Talks to the patched engine inside one running game over its pipe, \\.\pipe\neuralswitch-&lt;pid&gt;.
/// Each call is one connection: a line of text out, a line of JSON back. See docs/PROTOCOL.md.
/// </summary>
public sealed class EngineClient(string pipeName)
{
    public const string PipePrefix = "neuralswitch-";

    // The engine serves one connection at a time, so a short wait covers another request in flight.
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(750);

    // The engine waits up to half a second for the game's next frame before answering a change.
    private static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(3);

    public EngineClient(int processId) : this(PipeNameFor(processId))
    {
    }

    public string PipeName { get; } = pipeName;

    public static string PipeNameFor(int processId) => PipePrefix + processId.ToString(CultureInfo.InvariantCulture);

    public Task<EngineState> GetAsync(CancellationToken cancel = default) => SendAsync("get", cancel);

    public Task<EngineState> ToggleAsync(CancellationToken cancel = default) => SendAsync("toggle", cancel);

    /// <summary>Clears the engine's failure latch so the model is tried again without restarting the game.</summary>
    public Task<EngineState> RetryAsync(CancellationToken cancel = default) => SendAsync("retry", cancel);

    public Task<EngineState> SetAsync(bool? enabled = null, NrStyle? style = null, float? intensity = null,
                                      CancellationToken cancel = default) =>
        SendAsync(BuildSet(enabled, style, intensity), cancel);

    internal static string BuildSet(bool? enabled, NrStyle? style, float? intensity)
    {
        var request = new StringBuilder("set");

        if (enabled is { } on)
            request.Append(" enabled=").Append(on ? "true" : "false");

        if (style is { } s)
            request.Append(" style=").Append(s.WireName());

        if (intensity is { } i)
        {
            if (!(i >= 0f && i <= 2f))
                throw new ArgumentOutOfRangeException(nameof(intensity), "Intensity goes from 0 to 2.");

            // The engine parses with from_chars: always a dot, whatever the PC's region settings say.
            request.Append(" intensity=").Append(i.ToString("0.###", CultureInfo.InvariantCulture));
        }

        if (request.Length == 3)
            throw new ArgumentException("Nothing to set.");

        return request.ToString();
    }

    public async Task<EngineState> SendAsync(string request, CancellationToken cancel = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(AnswerTimeout);

        await using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        try
        {
            await pipe.ConnectAsync(ConnectTimeout, timeout.Token).ConfigureAwait(false);
        }
        catch (TimeoutException e)
        {
            throw new EngineException("The game did not answer. It may have closed.", e);
        }
        catch (IOException e)
        {
            throw new EngineException("The game did not answer. It may have closed.", e);
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(request + "\n");
            await pipe.WriteAsync(bytes, timeout.Token).ConfigureAwait(false);
            await pipe.FlushAsync(timeout.Token).ConfigureAwait(false);

            using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
            var line = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false)
                       ?? throw new EngineException("The game closed the connection without answering.");

            var state = EngineState.Parse(line);

            if (!state.Ok)
                throw new EngineException(state.Error ?? "The game refused the request.");

            return state;
        }
        catch (OperationCanceledException e) when (!cancel.IsCancellationRequested)
        {
            throw new EngineException("The game took too long to answer.", e);
        }
        catch (IOException e)
        {
            throw new EngineException("The connection to the game broke.", e);
        }
    }
}
