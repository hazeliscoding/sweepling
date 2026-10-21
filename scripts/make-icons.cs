#:package Svg.Skia@5.2.3
#:property ManagePackageVersionsCentrally=false

// Renders docs/brand/tray.svg into the multi-size .ico used by the exe, and each tray state in
// docs/brand/tray/ into the icon the tray shows for it. Run from the repo root: dotnet run scripts/make-icons.cs
using SkiaSharp;
using Svg.Skia;

var brand = Path.Combine("docs", "brand");
WriteIcon(Path.Combine(brand, "tray.svg"), Path.Combine(brand, "sweepling.ico"), [16, 20, 24, 32, 40, 48, 64, 256]);

// 16, 20 and 24 px are the tray at 100%, 125% and 150% scaling; 32 to 48 cover 200% to 300%.
var trayIcons = Path.Combine("src", "Sweepling", "Assets", "Tray");
Directory.CreateDirectory(trayIcons);
foreach (var state in Directory.GetFiles(Path.Combine(brand, "tray"), "tray-*.svg").Order())
{
    WriteIcon(state, Path.Combine(trayIcons, Path.ChangeExtension(Path.GetFileName(state), ".ico")), [16, 20, 24, 32, 40, 48]);
}

static void WriteIcon(string source, string target, int[] sizes)
{
    using var svg = new SKSvg();
    svg.Load(source);
    var picture = svg.Picture ?? throw new InvalidOperationException($"{source} did not render");
    var pngs = sizes.Select(size => Render(picture, size)).ToList();

    using var ico = File.Create(target);
    using var writer = new BinaryWriter(ico);
    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)sizes.Length);
    var offset = 6 + 16 * sizes.Length;
    for (var i = 0; i < sizes.Length; i++)
    {
        // A 256 px entry is stored as 0 in the one-byte width and height fields.
        writer.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
        writer.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(pngs[i].Length);
        writer.Write(offset);
        offset += pngs[i].Length;
    }

    foreach (var png in pngs)
    {
        writer.Write(png);
    }

    Console.WriteLine($"Wrote {target} ({string.Join(", ", sizes)})");
}

static byte[] Render(SKPicture picture, int size)
{
    using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
    var canvas = surface.Canvas;
    canvas.Clear(SKColors.Transparent);
    var scale = size / picture.CullRect.Width;
    canvas.Scale(scale);
    canvas.Translate(-picture.CullRect.Left, -picture.CullRect.Top);
    canvas.DrawPicture(picture);
    canvas.Flush();
    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    return data.ToArray();
}
