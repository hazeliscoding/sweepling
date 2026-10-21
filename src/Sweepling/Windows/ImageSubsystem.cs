namespace Sweepling.Windows;

/// <summary>Tells a windowed (GUI) app from a console program by the subsystem in its executable's PE header.</summary>
internal sealed class ImageSubsystem
{
    private const ushort WindowsGui = 2;

    private readonly Dictionary<string, bool> _seen = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>An image that can't be read counts as windowed, so Sweepling never offers it.</summary>
    public bool IsWindowed(string imagePath)
    {
        if (!_seen.TryGetValue(imagePath, out var windowed))
        {
            _seen[imagePath] = windowed = Read(imagePath) is not { } subsystem || subsystem == WindowsGui;
        }

        return windowed;
    }

    private static ushort? Read(string imagePath)
    {
        try
        {
            using var file = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new BinaryReader(file);
            file.Position = 0x3C;
            var header = reader.ReadInt32();

            // PE signature (4 bytes), then the COFF file header (20 bytes), then Subsystem at 68 into the optional header.
            file.Position = header;
            if (reader.ReadUInt32() != 0x00004550)
            {
                return null;
            }

            file.Position = header + 4 + 20 + 68;
            return reader.ReadUInt16();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
