using System.Linq;
using System.Text.RegularExpressions;

namespace WuGing.VectorTileRenderer;

/// <summary>Explicit maintenance of a caller-owned rendered-tile cache directory.</summary>
public static class TileCache
{
    private static readonly Regex tileName = new(
        @"\A-?[0-9]+x-?[0-9]+--?[0-9]+(?:[.,][0-9]+)?(?:[Ee][+-]?[0-9]+)?-(?:[a-fA-F0-9]{12}|[a-fA-F0-9]{64})\.png\z",
        RegexOptions.CultureInvariant);

    /// <summary>Removes expired PNGs, then oldest-written PNGs until maxBytes is reached.</summary>
    /// <remarks>
    /// Only top-level rendered-tile filenames are eligible. Call only for a directory you own.
    /// Temporary files, subdirectories, and file links are excluded; linked directory paths
    /// are rejected. Age is measured from last write, not last access. Reads/publication in
    /// this process share a lock; other processes and future writes can exceed the limits.
    /// Per-file IO/permission failures are counted; directory enumeration failures propagate.
    /// Byte counts are a snapshot of eligible files only, excluding unreadable file metadata.
    /// The caller must prevent external path/link replacement during maintenance.
    /// </remarks>
    public static TileCachePruneResult Prune(string cachePath, long maxBytes, TimeSpan? maxAge = null)
    {
        if (string.IsNullOrWhiteSpace(cachePath)) throw new ArgumentException("A cache directory is required.", nameof(cachePath));
        if (maxBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        if (maxAge.HasValue && maxAge.Value < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maxAge));

        var directory = new DirectoryInfo(Path.GetFullPath(cachePath));
        var result = new TileCachePruneResult();
        lock (Renderer.cacheLock)
        {
            RejectLinkedDirectories(directory);
            if (!directory.Exists) return result;

            var files = new List<(string Path, long Bytes, DateTime Written)>();
            foreach (var path in Directory.GetFiles(directory.FullName))
            {
                if (!tileName.IsMatch(Path.GetFileName(path))) continue;
                try
                {
                    var file = new FileInfo(path);
                    if ((file.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0) continue;
                    var bytes = file.Length;
                    files.Add((path, bytes, file.LastWriteTimeUtc));
                    result.RemainingBytes = checked(result.RemainingBytes + bytes);
                }
                catch (IOException) { result.FailedFiles++; }
                catch (UnauthorizedAccessException) { result.FailedFiles++; }
            }

            var now = DateTime.UtcNow;
            foreach (var file in files.OrderBy(f => f.Written).ThenBy(f => f.Path, StringComparer.Ordinal))
            {
                var expired = maxAge.HasValue && now - file.Written > maxAge.Value;
                if (!expired && result.RemainingBytes <= maxBytes) continue;
                try
                {
                    // Recheck before deletion; never follow a replaced file or directory link.
                    RejectLinkedDirectories(directory);
                    var attributes = File.GetAttributes(file.Path);
                    if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0) continue;
                    File.Delete(file.Path);
                    result.RemainingBytes -= file.Bytes;
                    result.DeletedBytes += file.Bytes;
                    result.DeletedFiles++;
                }
                catch (IOException) { result.FailedFiles++; }
                catch (UnauthorizedAccessException) { result.FailedFiles++; }
            }
        }
        return result;
    }

    private static void RejectLinkedDirectories(DirectoryInfo directory)
    {
        for (var current = directory; current != null; current = current.Parent)
        {
            current.Refresh();
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Cache maintenance does not support linked directory paths.");
        }
    }
}
