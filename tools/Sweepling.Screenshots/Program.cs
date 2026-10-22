using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Sweepling;
using Sweepling.Leftovers;
using Sweepling.Review;

// Renders the review window from made-up leftovers, in light and dark, for the README. Made-up data keeps
// real processes, folders and command lines out of the pictures, and the same pictures come out every time.
// Usage: dotnet run --project tools/Sweepling.Screenshots -- docs/screenshots
var output = Directory.CreateDirectory(args is [var folder] ? folder : Path.Combine("artifacts", "screenshots"));

using var session = HeadlessUnitTestSession.StartNew(typeof(Shots));

// Waited for here rather than awaited: an await would carry on on the session's own thread, which then can't stop itself.
session.Dispatch(() =>
{
    // Dark first: after a switch from light to dark, the expanders' chevrons render as a bent corner.
    // Each border is the theme's line token from docs/brand/README.md.
    foreach (var (theme, border, name) in new[]
    {
        (ThemeVariant.Dark, Color.Parse("#2E373B"), "review-dark.png"),
        (ThemeVariant.Light, Color.Parse("#DDE5E2"), "review-light.png"),
    })
    {
        Application.Current!.RequestedThemeVariant = theme;

        // Tall enough to show the Session still running group whole.
        var window = new ReviewWindow(Demo.Model()) { Height = 770 };
        window.Show();
        Save(window, border, Path.Combine(output.FullName, name));
        window.Close();
        Console.WriteLine($"Wrote {Path.Combine(output.FullName, name)}");
    }
}, CancellationToken.None).GetAwaiter().GetResult();

// At twice the pixels, for sharp text on high-DPI screens, with rounded corners and a hairline border
// so the window stands out on a page of the same color.
static void Save(ReviewWindow window, Color border, string path)
{
    var size = window.ClientSize;
    var pixels = new PixelSize((int)size.Width * 2, (int)size.Height * 2);
    var dpi = new Vector(192, 192);
    using var frame = new RenderTargetBitmap(pixels, dpi);
    frame.Render(window);

    using var framed = new RenderTargetBitmap(pixels, dpi);
    using (var context = framed.CreateDrawingContext())
    {
        var outline = new RoundedRect(new Rect(size), 8);
        using (context.PushClip(outline))
        {
            // The source rectangle is in the frame's pixels, the destination in layout units.
            context.DrawImage(frame, new Rect(0, 0, pixels.Width, pixels.Height), new Rect(size));
        }

        context.DrawRectangle(null, new Pen(new SolidColorBrush(border), 1), outline.Deflate(0.5, 0.5));
    }

    framed.Save(path, new PngBitmapEncoderOptions());
}

internal static class Shots
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

internal static class Demo
{
    private const string Node = @"C:\Program Files\nodejs\node.exe";
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 15, 0, 0, TimeSpan.Zero);

    // Three leftovers of ended sessions and one stray of a session that's still working.
    private static readonly (Group Group, string Folder, long Memory)[] Groups =
    [
        (Of(SessionState.Ended, TimeSpan.FromDays(2.3), [3000],
            (4120, Node, $"\"{Node}\" D:\\projects\\storefront\\node_modules\\next\\dist\\bin\\next dev --turbopack"),
            (4188, Node, $"\"{Node}\" D:\\projects\\storefront\\node_modules\\next\\dist\\server\\lib\\start-server.js"),
            (4231, Node, $"\"{Node}\" D:\\projects\\storefront\\node_modules\\next\\dist\\compiled\\jest-worker\\processChild.js")),
            @"D:\projects\storefront", 405_000_000),
        (Of(SessionState.Ended, TimeSpan.FromHours(9.4), [5173],
            (6012, Node, $"\"{Node}\" D:\\projects\\docs-site\\node_modules\\vite\\bin\\vite.js"),
            (6040, @"D:\projects\docs-site\node_modules\@esbuild\win32-x64\esbuild.exe", "esbuild.exe --service=0.25.10 --ping")),
            @"D:\projects\docs-site", 124_000_000),
        (Of(SessionState.Ended, TimeSpan.FromHours(30.2), [],
            (7311, @"C:\Program Files\Git\usr\bin\tail.exe", "/usr/bin/tail -f /tmp/claude/tasks/b7.output")),
            @"D:\projects\api", 5_200_000),
        (Of(SessionState.Running, TimeSpan.FromMinutes(25), [4200],
            (8820, Node, $"\"{Node}\" D:\\projects\\admin\\node_modules\\@angular\\cli\\bin\\ng serve --port 4200")),
            @"D:\projects\admin", 238_000_000),
    ];

    public static ReviewViewModel Model()
    {
        var model = new ReviewViewModel(
            new ReviewFacts(g => Find(g).Folder, g => Find(g).Memory),
            _ => [],
            (_, _, _, _) => { },
            () => Now);
        model.Show([.. Groups.Select(g => g.Group)]);
        return model;
    }

    private static (Group Group, string Folder, long Memory) Find(Group group) => Groups.Single(g => g.Group == group);

    private static Group Of(SessionState session, TimeSpan age, int[] ports, params (int Pid, string Image, string CommandLine)[] processes)
    {
        var members = processes.Select((p, i) => new Proc(p.Pid, i == 0 ? 100 : processes[0].Pid, Now - age, p.Image, p.CommandLine)).ToList();
        return new Group(session, Tie.Environment, [members[0]], members, ports);
    }
}
