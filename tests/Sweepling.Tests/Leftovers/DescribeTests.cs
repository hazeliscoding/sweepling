using Sweepling.Leftovers;
using static Sweepling.Tests.Leftovers.Fake;

namespace Sweepling.Tests.Leftovers;

public sealed class DescribeTests
{
    private static string What(params Proc[] members) =>
        Describe.What(new Group(SessionState.Ended, Tie.Traced, [members[0]], members, []));

    [Theory]
    [InlineData(@"node C:\app\node_modules\next\dist\bin\next dev --turbopack", "Next.js dev server")]
    [InlineData(@"node C:\app\node_modules\@angular\cli\bin\ng serve --port 4300", "Angular dev server")]
    [InlineData(@"node C:\app\node_modules\vite\bin\vite.js", "Vite dev server")]
    [InlineData(@"node C:\app\node_modules\webpack\bin\webpack.js serve", "webpack dev server")]
    [InlineData(@"dotnet watch run", ".NET watch")]
    public void A_dev_server_is_named_by_its_tool(string commandLine, string expected) =>
        Assert.Equal(expected, What(P(1, 0, 0, Node, commandLine)));

    [Fact]
    public void The_most_specific_member_names_the_group() =>
        Assert.Equal("Next.js dev server", What(P(1, 0, 0, Node, @"node npm-cli.js run dev"), P(2, 1, 0, Node, @"node C:\app\node_modules\next\dist\bin\next dev")));

    [Theory]
    [InlineData(@"node C:\nodejs\node_modules\npm\bin\npm-cli.js run dev", "npm run dev")]
    [InlineData(@"bash /c/nvm4w/nodejs/npm start -- --port 4300", "npm start")]
    public void An_npm_script_is_named_by_its_script(string commandLine, string expected) =>
        Assert.Equal(expected, What(P(1, 0, 0, Node, commandLine)));

    [Fact]
    public void A_tail_on_task_output_is_a_task_output_watcher() =>
        Assert.Equal("Task output watcher", What(P(1, 0, 0, Tail, $"tail -n +1 -f {TaskOutput}"), P(2, 0, 0, Grep, "grep --line-buffered -E .")));

    [Fact]
    public void A_line_buffered_grep_alone_is_a_watcher_filter() =>
        Assert.Equal("Watcher filter", What(P(1, 0, 0, Grep, "grep --line-buffered -E \"ok |DONE\"")));

    [Fact]
    public void A_script_run_by_a_shell_is_a_shell_script() =>
        Assert.Equal("Shell script", What(P(1, 0, 0, @"C:\Program Files\Git\usr\bin\sh.exe", @"sh C:\Users\test\AppData\Local\Temp\claude\x\scratchpad\run.sh 1 2")));

    [Fact]
    public void An_mcp_server_is_named_so() =>
        Assert.Equal("MCP server", What(P(1, 0, 0, Node, "node C:/tools/desktop-commander-mcp/dist/index.js")));

    [Fact]
    public void Anything_else_is_named_by_its_program() =>
        Assert.Equal("python", What(P(1, 0, 0, @"C:\Python\python.exe", "python train.py")));

    [Theory]
    [InlineData(@"C:\Users\test\AppData\Local\Temp\claude\C--repos-pdfcraft\0b6c\tasks\b1.output", @"C:\repos\pdfcraft")]
    [InlineData("C:/Users/test/AppData/Local/Temp/claude/C--code-gil-sweep/0b6c/tasks/b1.output", @"C:\code\gil-sweep")]
    public void A_claude_temp_path_names_its_project_folder(string path, string expected)
    {
        string[] existing = [@"C:\repos", @"C:\repos\pdfcraft", @"C:\code", @"C:\code\gil-sweep"];

        Assert.Equal(expected, Describe.ProjectFromClaudeTemp($"tail -f {path}", d => existing.Contains(d, StringComparer.OrdinalIgnoreCase)));
    }

    [Fact]
    public void A_project_folder_that_doesnt_exist_is_shown_as_claude_wrote_it() =>
        Assert.Equal("C--gone-app", Describe.ProjectFromClaudeTemp(@"tail -f C:\Users\test\AppData\Local\Temp\claude\C--gone-app\0b6c\x.output", _ => false));

    [Fact]
    public void A_groups_folder_is_its_first_native_members_folder()
    {
        var bash = P(1, 0, 0, Bash, "bash /c/nvm4w/nodejs/npm start");
        var node = P(2, 1, 0, Node, "node ng serve --port 4300");
        var group = new Group(SessionState.Ended, Tie.CommandLine, [bash], [bash, node], [4300]);

        Assert.Equal(@"C:\code\portfolio", Describe.Folder(group, p => p.Pid == 2 ? @"C:\code\portfolio" : null, _ => false));
    }

    [Fact]
    public void A_group_of_git_bash_watchers_takes_its_folder_from_a_claude_temp_path()
    {
        var tail = P(1, 0, 0, Tail, $"tail -n +1 -f {TaskOutput}");
        string[] existing = [@"C:\code", @"C:\code\app"];

        Assert.Equal(@"C:\code\app", Describe.Folder(new Group(SessionState.Ended, Tie.CommandLine, [tail], [tail], []), _ => null, existing.Contains));
    }

    [Fact]
    public void A_group_with_no_folder_says_so() =>
        Assert.Null(Describe.Folder(new Group(SessionState.Ended, Tie.Environment, [P(1, 0, 0, Node)], [P(1, 0, 0, Node)], []), _ => null, _ => true));

    [Fact]
    public void A_command_line_without_a_claude_temp_path_has_no_project() =>
        Assert.Null(Describe.ProjectFromClaudeTemp("node server.js", _ => true));
}
