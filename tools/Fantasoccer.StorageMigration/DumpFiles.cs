using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fantasoccer.StorageMigration;

internal sealed class DumpFiles
{
    private readonly string _root;
    public DumpFiles(string directory)
    {
        if (!Path.IsPathFullyQualified(directory))
            throw new ArgumentException("The dump directory must be an absolute path.");
        _root = Path.GetFullPath(directory);
    }

    public bool Exists => File.Exists(Path.Combine(_root, "manifest.json"));
    public bool HasEntries => Directory.Exists(_root) && Directory.EnumerateFileSystemEntries(_root).Any();

    public FileStream Lock()
    {
        CreateDirectory(_root);
        return new FileStream(Path.Combine(_root, ".migration.lock"), Options(FileMode.OpenOrCreate));
    }

    public FileStream ReadLock() => new(Path.Combine(_root, ".migration.lock"),
        FileMode.Open, FileAccess.Read, FileShare.Read);

    public string BlobPath(string container, string blob) => DataPath("blobs", container + "\0" + blob, ".bin");
    public string TablePath(string table) => DataPath("tables", table, ".jsonl");

    // Hashing names prevents path traversal, reserved-name and case-collision problems
    // for arbitrary blob names. Original names remain in the protected manifest.
    private string DataPath(string category, string key, string extension) => Path.Combine(_root, category,
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + extension);

    public async Task<DumpManifest> ReadManifestAsync(CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(Path.Combine(_root, "manifest.json"));
        var manifest = await JsonSerializer.DeserializeAsync<DumpManifest>(stream, DumpJson.Compact, cancellationToken)
            ?? throw new InvalidDataException("Manifest is empty.");
        if (manifest.FormatVersion != 1 || manifest.Scope != "blob-table-current-state")
            throw new InvalidDataException("Unsupported manifest format or scope.");
        if (manifest.Containers.Select(x => x.Name).Distinct().Count() != manifest.Containers.Count ||
            manifest.Tables.Select(x => x.Name).Distinct().Count() != manifest.Tables.Count ||
            manifest.Containers.Any(container => container.Blobs.Select(x => x.Name).Distinct().Count() != container.Blobs.Count))
            throw new InvalidDataException("Duplicate resources in manifest.");
        return manifest;
    }

    public Task WriteManifestAsync(DumpManifest manifest, CancellationToken cancellationToken) =>
        WriteAtomicAsync(Path.Combine(_root, "manifest.json"),
            stream => JsonSerializer.SerializeAsync(stream, manifest, DumpJson.Indented, cancellationToken), cancellationToken);

    public static async Task WriteAtomicAsync(string path, Func<Stream, Task> write, CancellationToken cancellationToken)
    {
        CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".partial";
        await using (var stream = new FileStream(temporary, Options(FileMode.Create)))
        {
            await write(stream);
            await stream.FlushAsync(cancellationToken);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporary, path, overwrite: true);
    }

    public static async Task<(long Length, string Sha256)> FingerprintAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return (stream.Length, Convert.ToHexStringLower(hash));
    }

    public static async Task<bool> MatchesAsync(string path, long length, string hash, CancellationToken cancellationToken)
    {
        if (!File.Exists(path) || new FileInfo(path).Length != length) return false;
        return (await FingerprintAsync(path, cancellationToken)).Sha256 == hash;
    }

    private static FileStreamOptions Options(FileMode mode)
    {
        var options = new FileStreamOptions
        {
            Mode = mode, Access = FileAccess.ReadWrite, Share = FileShare.None,
            Options = FileOptions.Asynchronous
        };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return options;
    }

    private static void CreateDirectory(string path)
    {
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(path);
        else Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}
