using Sweepling.Leftovers;

namespace Sweepling.Tests.Leftovers;

public sealed class CommandLineMaskTests
{
    private const string Profile = @"C:\Users\test";

    // Token-shaped values are built at run time, so no literal ever reaches the history.
    private static readonly string GitHubToken = "gh" + "p_" + new string('x', 36);

    [Theory]
    [InlineData(@"tail -f C:\Users\test\AppData\Local\Temp\x.output", @"tail -f %USERPROFILE%\AppData\Local\Temp\x.output")]
    [InlineData("node C:/Users/test/app/server.js", "node %USERPROFILE%/app/server.js")]
    public void Replaces_the_user_profile_path(string commandLine, string expected) =>
        Assert.Equal(expected, CommandLineMask.Apply(commandLine, Profile));

    // Claude Code names a project's temp folder after its path, with a hyphen for everything but letters and digits.
    [Theory]
    [InlineData(@"C:\Users\test", @"tail -f C:\Users\test\AppData\Local\Temp\claude\C--Users-test-app\t\x.output", @"tail -f %USERPROFILE%\AppData\Local\Temp\claude\%USERPROFILE%-app\t\x.output")]
    [InlineData(@"C:\Users\first.last", @"cat C--Users-first-last--claude\x", @"cat %USERPROFILE%--claude\x")]
    public void Replaces_the_user_profile_in_a_claude_code_project_name(string profile, string commandLine, string expected) =>
        Assert.Equal(expected, CommandLineMask.Apply(commandLine, profile));

    [Fact]
    public void Masks_a_token_with_a_known_prefix() =>
        Assert.Equal("git push https://<masked>@github.com/a/b", CommandLineMask.Apply($"git push https://{GitHubToken}@github.com/a/b", Profile));

    [Theory]
    [InlineData("deploy --token=abc123", "deploy --token=<masked>")]
    [InlineData("deploy --api-key abc123", "deploy --api-key <masked>")]
    [InlineData("run PASSWORD=hunter2 app", "run PASSWORD=<masked> app")]
    public void Masks_values_after_secret_looking_names(string commandLine, string expected) =>
        Assert.Equal(expected, CommandLineMask.Apply(commandLine, Profile));

    [Fact]
    public void Masks_a_long_opaque_string() =>
        Assert.Equal("curl -H x:<masked>", CommandLineMask.Apply("curl -H x:" + new string('Q', 20) + new string('7', 20), Profile));

    [Fact]
    public void Shortens_a_long_command_line()
    {
        var masked = CommandLineMask.Apply("node " + string.Join(' ', Enumerable.Repeat("--flag", 100)), Profile);

        Assert.Equal(CommandLineMask.MaxLength + 1, masked.Length);
        Assert.EndsWith("…", masked);
    }

    [Fact]
    public void Leaves_an_ordinary_command_line_readable() =>
        Assert.Equal("node next dev --turbopack --port 3001", CommandLineMask.Apply("node next dev --turbopack --port 3001", Profile));
}
