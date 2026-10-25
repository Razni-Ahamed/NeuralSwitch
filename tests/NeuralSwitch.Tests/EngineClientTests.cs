using System.Globalization;
using NeuralSwitch.Core.Engine;

namespace NeuralSwitch.Tests;

public sealed class EngineClientTests : IAsyncLifetime
{
    private readonly SimulatedEngine _engine = new($"neuralswitch-test-{Guid.NewGuid():N}");
    private EngineClient _client = null!;

    public Task InitializeAsync()
    {
        _engine.Start();
        _client = new EngineClient(_engine.PipeName);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _engine.DisposeAsync();

    [Fact]
    public async Task Get_reports_the_engine_state()
    {
        _engine.Style = NrStyle.Cinematic;

        var state = await _client.GetAsync();

        Assert.True(state.Ok);
        Assert.Equal(1, state.Protocol);
        Assert.Equal("SimulatedGame.exe", state.Exe);
        Assert.False(state.Enabled);
        Assert.Equal(NrStyle.Cinematic, state.Style);
        Assert.Null(state.Applied);
    }

    [Fact]
    public async Task Set_turns_natural_rendering_on()
    {
        var state = await _client.SetAsync(enabled: true, style: NrStyle.Natural);

        Assert.True(state.Applied);
        Assert.True(state.Enabled);
        Assert.True(state.Running);
        Assert.Equal(NrStyle.Natural, state.Style);
        Assert.True(_engine.Enabled);
    }

    [Fact]
    public async Task Toggle_flips_and_flips_back()
    {
        Assert.True((await _client.ToggleAsync()).Enabled);
        Assert.False((await _client.ToggleAsync()).Enabled);
    }

    [Fact]
    public async Task Failure_is_reported_and_retry_clears_it()
    {
        _engine.Enabled = true;
        _engine.Failure = "nvngx_dlssnr.dll was not found";

        var failed = await _client.GetAsync();
        Assert.False(failed.Running);
        Assert.Equal("nvngx_dlssnr.dll was not found", failed.Failure);

        var retried = await _client.RetryAsync();
        Assert.True(retried.Running);
        Assert.Equal("", retried.Failure);
    }

    [Fact]
    public async Task Not_rendering_answers_with_applied_false()
    {
        _engine.Rendering = false;

        var state = await _client.SetAsync(enabled: true);

        Assert.False(state.Applied);
        Assert.False(state.Rendering);
    }

    [Fact]
    public async Task A_refused_request_throws_with_the_engine_message()
    {
        var e = await Assert.ThrowsAsync<EngineException>(() => _client.SendAsync("set style=vivid"));

        Assert.Contains("style takes default, natural or cinematic", e.Message);
    }

    [Fact]
    public async Task One_bad_setting_changes_nothing()
    {
        await Assert.ThrowsAsync<EngineException>(() => _client.SendAsync("set enabled=true intensity=9"));

        Assert.False(_engine.Enabled);
    }

    [Fact]
    public async Task Several_requests_in_a_row_each_get_an_answer()
    {
        for (var i = 0; i < 20; i++)
            await _client.SetAsync(style: NrStyles.All[i % 3]);

        Assert.Equal(20, _engine.Requests);
        Assert.Equal(NrStyles.All[19 % 3], _engine.Style);
    }

    [Fact]
    public async Task No_game_listening_throws_a_readable_error()
    {
        var nobody = new EngineClient($"neuralswitch-nobody-{Guid.NewGuid():N}");

        var e = await Assert.ThrowsAsync<EngineException>(() => nobody.GetAsync());

        Assert.Contains("did not answer", e.Message);
    }

    [Fact]
    public void Intensity_is_sent_with_a_dot_whatever_the_region()
    {
        var before = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("set intensity=0.75", EngineClient.BuildSet(null, null, 0.75f));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Fact]
    public void Set_builds_every_field_in_order()
    {
        Assert.Equal("set enabled=false style=cinematic intensity=1.5", EngineClient.BuildSet(false, NrStyle.Cinematic, 1.5f));
        Assert.Throws<ArgumentException>(() => EngineClient.BuildSet(null, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => EngineClient.BuildSet(null, null, 2.5f));
    }

    [Fact]
    public void Parses_the_engines_json_as_written_by_the_cpp_side()
    {
        // A line exactly as DlssNr_Remote.cpp formats it.
        const string line = "{\"ok\":true,\"applied\":true,\"protocol\":1,\"pid\":4242,\"exe\":\"Spider-Man2.exe\"," +
                            "\"rendering\":true,\"enabled\":true,\"style\":1,\"styleName\":\"natural\",\"running\":true," +
                            "\"failure\":\"\",\"mode\":\"your settings\",\"compare\":\"yours\",\"intensity\":0.800,\"fps\":58.3,\"nrMs\":2.41}";

        var state = EngineState.Parse(line);

        Assert.Equal(4242, state.Pid);
        Assert.Equal("Spider-Man2.exe", state.Exe);
        Assert.Equal(NrStyle.Natural, state.Style);
        Assert.Equal(0.8f, state.Intensity, 3);
        Assert.Equal(2.41, state.NrMs, 2);
        Assert.True(state.Applied);
    }
}
