using Sweepling.Leftovers;
using Sweepling.Windows;
using static Sweepling.Tests.Windows.RealProcesses;

namespace Sweepling.Tests.Windows;

public sealed class ProcessDetailsTests
{
    [Fact]
    public void A_process_reports_its_folder_and_private_memory()
    {
        var folder = Directory.CreateTempSubdirectory("sweepling-test-");
        using var sleeper = StartSleeper(Marker(), folder.FullName);
        try
        {
            var seen = Assert.Single(new ProcessReader(null).Take().Processes, p => p.Pid == sleeper.Id);

            Assert.Equal(folder.FullName.TrimEnd('\\'), ProcessDetails.CurrentDirectory(seen), ignoreCase: true);
            Assert.True(ProcessDetails.PrivateBytes(seen) > 1 << 20);
        }
        finally
        {
            Stop(sleeper);
            sleeper.WaitForExit();
            folder.Delete();
        }
    }

    [Fact]
    public void A_pid_that_now_belongs_to_another_process_reports_nothing()
    {
        using var sleeper = StartSleeper(Marker());
        try
        {
            var stale = new Proc(sleeper.Id, 0, new DateTimeOffset(sleeper.StartTime).AddSeconds(-1), @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", "");

            Assert.Equal((null, null), (ProcessDetails.CurrentDirectory(stale), ProcessDetails.PrivateBytes(stale)));
        }
        finally
        {
            Stop(sleeper);
        }
    }

    [Fact]
    public void A_git_bash_program_is_an_msys_program()
    {
        const string bash = @"C:\Program Files\Git\usr\bin\bash.exe";
        Assert.SkipUnless(File.Exists(bash), "Git for Windows isn't installed here.");

        Assert.True(ProcessDetails.IsMsys(bash));
        Assert.False(ProcessDetails.IsMsys(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe")));
    }
}
