using static Sweepling.Tests.Leftovers.Fake;

namespace Sweepling.Tests.Leftovers;

public sealed class RulesTests
{
    [Fact]
    public void The_native_claude_exe_is_an_agent_root() =>
        Assert.True(Fake.Rules.IsAgentRoot(P(1, 0, 0, Claude)));

    [Fact]
    public void Node_running_the_claude_code_package_is_an_agent_root() =>
        Assert.True(Fake.Rules.IsAgentRoot(P(1, 0, 0, Node, @"node C:\npm\node_modules\@anthropic-ai\claude-code\cli.js")));

    [Fact]
    public void Another_claude_named_process_is_not_an_agent_root() =>
        Assert.False(Fake.Rules.IsAgentRoot(P(1, 0, 0, Profile + @"\AppData\Local\AnthropicClaude\claude.exe")));

    [Fact]
    public void Windows_binaries_are_protected() =>
        Assert.True(Fake.Rules.IsProtected(P(1, 0, 0, Cmd, "cmd.exe /c npm run dev")));

    [Fact]
    public void Orcas_daemon_host_is_protected() =>
        Assert.True(Fake.Rules.IsProtected(P(1, 0, 0, Profile + @"\AppData\Local\Orca\daemon-host\1.4.224\Orca.exe")));

    [Fact]
    public void Crash_handlers_are_protected() =>
        Assert.True(Fake.Rules.IsProtected(P(1, 0, 0, @"C:\Program Files\Orca\Orca.exe", "Orca.exe --type=crashpad-handler --no-rate-limit")));

    [Theory]
    [InlineData(@"dotnet C:\sdk\MSBuild.dll /noautoresponse /nologo /nodemode:1 /nodeReuse:true")]
    [InlineData(@"dotnet exec C:\sdk\Roslyn\bincore\VBCSCompiler.dll -pipename:abc")]
    [InlineData(@"dotnet exec C:\sdk\Sdks\Microsoft.NET.Sdk.Razor\tools\rzc.dll server -p abc")]
    public void MSBuild_nodes_and_build_servers_are_protected(string commandLine) =>
        Assert.True(Fake.Rules.IsProtected(P(1, 0, 0, @"C:\Program Files\dotnet\dotnet.exe", commandLine)));

    [Fact]
    public void An_ordinary_dev_server_is_not_protected() =>
        Assert.False(Fake.Rules.IsProtected(P(1, 0, 0, Node, "node next dev --port 3001")));

    [Theory]
    [InlineData(TaskOutput)]
    [InlineData("C:/Users/test/AppData/Local/Temp/claude/C--code-app/0b6c/scratchpad/run.sh")]
    public void A_claude_temp_path_in_the_command_line_ties_a_stray(string path) =>
        Assert.True(Fake.Rules.TiesStray(P(1, 0, 0, Tail, $"tail -f {path}")));

    [Fact]
    public void A_dev_server_started_from_git_bash_ties_a_stray() =>
        Assert.True(Fake.Rules.TiesStray(P(1, 0, 0, Bash, "bash /c/nvm4w/nodejs/npm start -- --port 4300")));

    [Fact]
    public void A_dev_server_started_elsewhere_does_not_tie_a_stray() =>
        Assert.False(Fake.Rules.TiesStray(P(1, 0, 0, Node, "node npm-cli.js start")));

    [Fact]
    public void The_shipped_rules_wait_a_minute_and_read_claude_pid() =>
        Assert.Equal((TimeSpan.FromSeconds(60), "CLAUDE_PID"), (Fake.Rules.Grace, Fake.Rules.AgentPidVariable));

    [Fact]
    public void A_user_rules_file_adds_to_the_shipped_rules()
    {
        const string user = """{ "version": 1, "protect": [ { "name": "my server", "commandLine": "my-server\\.js" } ] }""";

        var rules = Sweepling.Leftovers.Rules.Parse([Sweepling.Leftovers.Rules.ShippedJson, user], Env);

        Assert.True(rules.IsProtected(P(1, 0, 0, Node, "node my-server.js")));
        Assert.True(rules.IsProtected(P(1, 0, 0, Cmd)));
    }

    [Fact]
    public void A_rules_file_of_another_version_is_refused() =>
        Assert.Throws<InvalidDataException>(() => Sweepling.Leftovers.Rules.Parse(["""{ "version": 2 }"""], Env));

    [Fact]
    public void A_rule_that_matches_nothing_is_refused() =>
        Assert.Throws<InvalidDataException>(() => Sweepling.Leftovers.Rules.Parse(["""{ "version": 1, "protect": [ { "name": "empty" } ] }"""], Env));
}
