using NeuralSwitch.Core.Engine;

namespace NeuralSwitch.Tests;

public sealed class DiscoveryAndStyleTests
{
    [Fact]
    public void Finds_games_by_their_pipe_names_only()
    {
        string[] pipes =
        [
            @"\\.\pipe\neuralswitch-4242",
            @"\\.\pipe\neuralswitch-17",
            @"\\.\pipe\neuralswitch-test-abc",
            @"\\.\pipe\neuralswitch-",
            @"\\.\pipe\mojo.1234",
            "neuralswitch-99",
        ];

        Assert.Equal([17, 99, 4242], EngineDiscovery.FindRunning(() => pipes));
    }

    [Fact]
    public async Task Finds_a_live_simulated_game()
    {
        await using var engine = new SimulatedEngine(EngineClient.PipeNameFor(999_000 + Random.Shared.Next(999))).Start();
        var pid = int.Parse(engine.PipeName[EngineClient.PipePrefix.Length..]);

        // The pipe exists once the server is waiting for a client.
        for (var i = 0; i < 50 && !EngineDiscovery.FindRunning().Contains(pid); i++)
            await Task.Delay(20);

        Assert.Contains(pid, EngineDiscovery.FindRunning());
    }

    [Theory]
    [InlineData("natural", NrStyle.Natural)]
    [InlineData("NATURAL", NrStyle.Natural)]
    [InlineData("0", NrStyle.Default)]
    [InlineData("cinematic", NrStyle.Cinematic)]
    public void Parses_style_names_and_numbers(string text, NrStyle expected)
    {
        Assert.True(NrStyles.TryParse(text, out var style));
        Assert.Equal(expected, style);
    }

    [Fact]
    public void Next_cycles_through_all_three()
    {
        Assert.Equal(NrStyle.Natural, NrStyle.Default.Next());
        Assert.Equal(NrStyle.Cinematic, NrStyle.Natural.Next());
        Assert.Equal(NrStyle.Default, NrStyle.Cinematic.Next());
        Assert.False(NrStyles.TryParse("vivid", out _));
    }
}
