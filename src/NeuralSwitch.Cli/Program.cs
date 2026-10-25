using System.Globalization;
using NeuralSwitch.Core;
using NeuralSwitch.Core.Engine;
using NeuralSwitch.Core.Games;
using NeuralSwitch.Core.Install;
using NeuralSwitch.Core.Model;

const string Usage = """
    neuralswitch-cli - DLSS 5 Neural Rendering control for running games

    While a game runs:
      status                         what each running game's engine is doing
      on | off | toggle              switch Neural Rendering
      style <default|natural|cinematic>
      intensity <0-2>
      retry                          try the model again after a failure
        --pid <id>                   pick the game when more than one is running

    Setting up:
      games [folder...]              DLSS games in your library folders (or the folders given)
      model                          where NVIDIA's nvngx_dlssnr.dll was found
      install <game folder> [--model <dll>] [--engine <folder>] [--style <s>] [--on]
              [--without-model]        engine only, to check a game before you have NVIDIA's model
      uninstall <game folder>

    Testing:
      simulate [--for <seconds>]     a pretend game the app can connect to, until Enter (or for that long)
    """;

var settings = AppSettings.Load();
var argv = args.ToList();

string? Option(string name)
{
    var i = argv.IndexOf(name);

    if (i < 0)
        return null;

    if (i + 1 >= argv.Count)
        throw new ArgumentException($"{name} needs a value.");

    var value = argv[i + 1];
    argv.RemoveRange(i, 2);
    return value;
}

bool Flag(string name) => argv.Remove(name);

try
{
    // Every option comes out of argv before the positional arguments are read, wherever it was typed.
    var pid = Option("--pid") is { } p ? int.Parse(p, CultureInfo.InvariantCulture) : (int?)null;
    var modelOption = Option("--model");
    var engineOption = Option("--engine");
    var styleOption = Option("--style");
    var startOn = Flag("--on");
    var withoutModel = Flag("--without-model");
    var forSeconds = Option("--for") is { } f ? int.Parse(f, CultureInfo.InvariantCulture) : (int?)null;
    var command = argv.Count > 0 ? argv[0].ToLowerInvariant() : "help";
    var rest = argv.Skip(1).ToList();

    switch (command)
    {
        case "status":
            var running = EngineDiscovery.FindRunning();

            if (running.Count == 0)
                Console.WriteLine("No running game has the NeuralSwitch engine loaded.");

            foreach (var id in running)
                Print(await new EngineClient(id).GetAsync());

            return 0;

        case "on" or "off":
            Print(await Client(pid).SetAsync(enabled: command == "on"));
            return 0;

        case "toggle":
            Print(await Client(pid).ToggleAsync());
            return 0;

        case "retry":
            Print(await Client(pid).RetryAsync());
            return 0;

        case "style":
            if (rest.Count != 1 || !NrStyles.TryParse(rest[0], out var style))
                throw new ArgumentException("style takes default, natural or cinematic.");

            Print(await Client(pid).SetAsync(style: style));
            return 0;

        case "intensity":
            if (rest.Count != 1 || !float.TryParse(rest[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var intensity))
                throw new ArgumentException("intensity takes a number from 0 to 2, like 0.8.");

            Print(await Client(pid).SetAsync(intensity: intensity));
            return 0;

        case "games":
            var folders = rest.Count > 0 ? rest : settings.LibraryFolders;

            foreach (var game in GameScanner.Scan(folders))
            {
                var state = Installer.GetState(game) switch
                {
                    InstallState.Installed => "prepared",
                    InstallState.OtherOptiScaler => "other OptiScaler install",
                    _ => game.HasAntiCheat ? $"blocked: {string.Join(", ", game.AntiCheat)}" : "not prepared",
                };

                Console.WriteLine($"{game.Name,-32} DLSS {game.DlssVersion,-12} {state,-26} {game.ExePath}");
            }

            return 0;

        case "model":
            var models = ModelLocator.Find(GameScanner.Scan(settings.LibraryFolders).Select(g => g.Root));

            if (models.Count == 0)
                Console.WriteLine("NVIDIA's Neural Rendering model was not found. It comes with games that ship DLSS 5, and NVIDIA's driver downloads it the first time one of them runs.");

            foreach (var m in models)
                Console.WriteLine($"{(m.IsSignedByNvidia ? "signed by NVIDIA" : "NOT signed by NVIDIA"),-22} v{m.Version,-14} {m.Size / 1048576.0,7:N1} MB  {m.Path}");

            return 0;

        case "install":
            var target = GameAt(rest);
            var engine = EnginePackage.Find(engineOption ?? settings.EngineDirectory)
                         ?? throw new InstallBlockedException("The engine was not found. Build it with engine\\build-engine.ps1.");
            var modelPath = withoutModel ? null :
                            modelOption ?? settings.ModelPath ??
                            ModelLocator.Find([target.Root]).FirstOrDefault(m => m.IsSignedByNvidia)?.Path ??
                            throw new InstallBlockedException("No NVIDIA-signed nvngx_dlssnr.dll was found. Pass one with --model, or use --without-model to test the engine alone.");
            var startStyle = settings.DefaultStyle;

            if (styleOption is not null && !NrStyles.TryParse(styleOption, out startStyle))
                throw new ArgumentException("--style takes default, natural or cinematic.");

            var record = Installer.Install(target, engine, modelPath,
                                           new InstallOptions { Style = startStyle, StartEnabled = startOn, WithoutModel = withoutModel });
            Console.WriteLine($"Prepared {target.Name}: the engine loads as {record.ProxyName}. Start the game, then use 'neuralswitch-cli on'.");

            if (withoutModel)
                Console.WriteLine("Without NVIDIA's model, switching Neural Rendering on reports it as missing. Everything else can be checked.");

            return 0;

        case "uninstall":
            var installed = GameAt(rest);
            Installer.Uninstall(installed);
            Console.WriteLine($"Removed the engine from {installed.Name}. The folder is as it was.");
            return 0;

        case "simulate":
            await using (var simulated = new SimulatedEngine().Start())
            {
                if (forSeconds is { } seconds)
                {
                    Console.WriteLine($"Simulating a game on \\\\.\\pipe\\{simulated.PipeName} for {seconds} s.");
                    await Task.Delay(TimeSpan.FromSeconds(seconds));
                }
                else
                {
                    Console.WriteLine($"Simulating a game on \\\\.\\pipe\\{simulated.PipeName}. Open NeuralSwitch to control it; press Enter to stop.");
                    Console.ReadLine();
                }
            }

            return 0;

        default:
            Console.WriteLine(Usage);
            return command is "help" or "--help" or "-h" ? 0 : 2;
    }
}
catch (Exception e) when (e is EngineException or InstallBlockedException or ArgumentException or FormatException or IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}

static EngineClient Client(int? pid)
{
    if (pid is { } id)
        return new EngineClient(id);

    var running = EngineDiscovery.FindRunning();

    return running.Count switch
    {
        0 => throw new EngineException("No running game has the NeuralSwitch engine loaded."),
        1 => new EngineClient(running[0]),
        _ => throw new EngineException($"Several games are running ({string.Join(", ", running)}); pick one with --pid."),
    };
}

static Game GameAt(List<string> rest)
{
    if (rest.Count != 1)
        throw new ArgumentException("Give the game's folder, e.g. \"C:\\Games\\Marvel's Spider-Man 2\".");

    return GameScanner.Inspect(Path.GetFullPath(rest[0].TrimEnd('\\', '/')))
           ?? throw new ArgumentException($"{rest[0]} does not look like a game that ships DLSS.");
}

static void Print(EngineState s)
{
    var status = !s.Enabled ? "off" :
                 s.Failure.Length > 0 ? $"failed: {s.Failure}" :
                 s.Running ? "running" : "starting";

    Console.WriteLine($"{s.Exe} (pid {s.Pid}): Neural Rendering {status}, style {s.Style.WireName()}, intensity {s.Intensity:0.00}" +
                      (s.Rendering ? $", {s.Fps:0} fps" + (s.Running ? $", {s.NrMs:0.0} ms per frame" : "") : ", game not rendering"));

    if (s.Applied == false)
        Console.WriteLine("  (the game is not rendering right now; the change applies on its next frame)");
}
