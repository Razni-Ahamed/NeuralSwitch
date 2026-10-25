using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace NeuralSwitch.Core.Engine;

/// <summary>
/// A stand-in for a game with the patched engine: serves the same pipe protocol, with the same rules and
/// error messages as dlssnr/DlssNr_Remote.cpp, without a game or a GPU. For tests, and for trying the app
/// (<c>neuralswitch-cli simulate</c>).
/// </summary>
public sealed class SimulatedEngine : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly object _lock = new();
    private Task? _loop;

    public SimulatedEngine(string? pipeName = null, string exe = "SimulatedGame.exe")
    {
        Pid = Environment.ProcessId;
        PipeName = pipeName ?? EngineClient.PipeNameFor(Pid);
        Exe = exe;
    }

    public string PipeName { get; }
    public int Pid { get; }
    public string Exe { get; }

    public bool Enabled { get; set; }
    public NrStyle Style { get; set; } = NrStyle.Natural;
    public float Intensity { get; set; } = 1.0f;
    public bool Rendering { get; set; } = true;

    /// <summary>Set to make the model fail the way a missing or refused nvngx_dlssnr.dll does.</summary>
    public string Failure { get; set; } = "";

    public int Requests { get; private set; }

    public SimulatedEngine Start()
    {
        if (_loop is not null)
            return this;

        // Created here rather than on the loop's thread, so the pipe exists as soon as Start returns and a
        // client connecting right away cannot race it. Like the engine: one instance, reused for every
        // connection.
        var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        _loop = Task.Run(() => ServeAsync(pipe, _stop.Token));
        return this;
    }

    private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken stop)
    {
        await using var owned = pipe;

        while (!stop.IsCancellationRequested)
        {
            try
            {
                await pipe.WaitForConnectionAsync(stop).ConfigureAwait(false);
                await SessionAsync(pipe, stop).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // The client went away mid-request.
            }

            // Always, as the engine does: a pipe the client has closed reads as not connected (broken), and
            // waiting on it again without disconnecting first would fail on every turn of this loop.
            try
            {
                pipe.Disconnect();
            }
            catch (Exception e) when (e is InvalidOperationException or IOException)
            {
            }
        }
    }

    private async Task SessionAsync(NamedPipeServerStream pipe, CancellationToken stop)
    {
        using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
        await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { NewLine = "\n" };

        while (await reader.ReadLineAsync(stop).ConfigureAwait(false) is { } line)
        {
            await writer.WriteLineAsync(Handle(line.TrimEnd('\r'))).ConfigureAwait(false);
            await writer.FlushAsync(stop).ConfigureAwait(false);
        }
    }

    internal string Handle(string line)
    {
        lock (_lock)
        {
            Requests++;
            var words = line.ToLowerInvariant().Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0)
                return Error("empty request");

            switch (words[0])
            {
                case "hello" or "get":
                    return State(null);
                case "toggle" when words.Length == 1:
                    Enabled = !Enabled;
                    return State(Rendering);
                case "retry" when words.Length == 1:
                    Failure = "";
                    return State(Rendering);
                case "set" when words.Length > 1:
                    return Set(words[1..]);
                default:
                    return Error("unknown request " + words[0]);
            }
        }
    }

    private string Set(string[] pairs)
    {
        bool? enabled = null;
        NrStyle? style = null;
        float? intensity = null;

        foreach (var pair in pairs)
        {
            var eq = pair.IndexOf('=');

            if (eq < 0)
                return Error("expected key=value, got " + pair);

            var (key, value) = (pair[..eq], pair[(eq + 1)..]);

            switch (key)
            {
                case "enabled":
                    enabled = value switch { "1" or "true" or "on" => true, "0" or "false" or "off" => false, _ => null };

                    if (enabled is null)
                        return Error("enabled takes true or false");

                    break;
                case "style":
                    if (!NrStyles.TryParse(value, out var s))
                        return Error("style takes default, natural or cinematic (or 0, 1, 2)");

                    style = s;
                    break;
                case "intensity":
                    if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var i) || !(i >= 0f && i <= 2f))
                        return Error("intensity takes a number from 0 to 2");

                    intensity = i;
                    break;
                default:
                    return Error("unknown setting " + key);
            }
        }

        // Validated in full before anything changes, as the engine queues one request or none.
        Enabled = enabled ?? Enabled;
        Style = style ?? Style;
        Intensity = intensity ?? Intensity;
        return State(Rendering);
    }

    private string State(bool? applied)
    {
        var running = Enabled && Rendering && Failure.Length == 0;

        return JsonSerializer.Serialize(new EngineState
        {
            Ok = true,
            Applied = applied,
            Protocol = 1,
            Pid = Pid,
            Exe = Exe,
            Rendering = Rendering,
            Enabled = Enabled,
            Style = Style,
            Intensity = Intensity,
            Running = running,
            Failure = Failure,
            Mode = running ? "your settings" : "",
            Compare = "yours",
            Fps = Rendering ? 60.0 : 0.0,
            NrMs = running ? 2.4 : 0.0,
        }, EngineState.Json);
    }

    private static string Error(string message) => JsonSerializer.Serialize(new { ok = false, error = message });

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);

        if (_loop is not null)
            await _loop.ConfigureAwait(false);

        _stop.Dispose();
    }
}
