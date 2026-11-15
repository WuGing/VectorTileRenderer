namespace VectorTileRenderer.Tests;

public class TileCacheTests
{
    private string directory;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "cache-prune-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
        foreach (var child in Directory.GetDirectories(directory))
        {
            foreach (var file in Directory.GetFiles(child)) File.Delete(file);
            Directory.Delete(child);
        }
        Directory.Delete(directory);
    }

    [Test]
    public void Prune_RemovesOldestWrittenTilesToMeetByteLimit()
    {
        var oldest = WriteTile(1, 10, 4);
        var middle = WriteTile(2, 20, 3);
        var newest = WriteTile(3, 30, 1, 64);
        var result = TileCache.Prune(directory, 30);
        Assert.That(File.Exists(oldest), Is.False);
        Assert.That(File.Exists(middle), Is.False);
        Assert.That(File.Exists(newest), Is.True);
        Assert.That(result.DeletedFiles, Is.EqualTo(2));
        Assert.That(result.DeletedBytes, Is.EqualTo(30));
        Assert.That(result.RemainingBytes, Is.EqualTo(30));
        Assert.That(result.FailedFiles, Is.Zero);
    }

    [Test]
    public void Prune_ExpiresOldTilesEvenWhenBelowByteLimit()
    {
        var old = WriteTile(1, 10, 10);
        var fresh = WriteTile(2, 20, 1);
        var result = TileCache.Prune(directory, 1000, TimeSpan.FromDays(5));
        Assert.That(File.Exists(old), Is.False);
        Assert.That(File.Exists(fresh), Is.True);
        Assert.That(result.RemainingBytes, Is.EqualTo(20));
    }

    [Test]
    public void Prune_PreservesTemporaryUnrelatedAndNestedFiles()
    {
        var tile = WriteTile(1, 10, 3);
        var temporary = tile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, "active writer");
        var unrelated = Path.Combine(directory, "photo.png");
        File.WriteAllText(unrelated, "owned by someone else");
        var child = Directory.CreateDirectory(Path.Combine(directory, "nested"));
        var nested = Path.Combine(child.FullName, Path.GetFileName(tile));
        File.WriteAllText(nested, "nested");
        var result = TileCache.Prune(directory, 0);
        Assert.That(result.DeletedFiles, Is.EqualTo(1));
        Assert.That(result.RemainingBytes, Is.Zero);
        Assert.That(new[] { temporary, unrelated, nested }.All(File.Exists), Is.True);
    }

    [Test]
    [Platform("Win")]
    public void Prune_ReportsLockedFileAndContinuesWithOtherTiles()
    {
        var locked = WriteTile(1, 10, 3);
        var other = WriteTile(2, 20, 1);
        using var stream = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);
        var result = TileCache.Prune(directory, 0);
        Assert.That(File.Exists(locked), Is.True);
        Assert.That(File.Exists(other), Is.False);
        Assert.That(result.FailedFiles, Is.EqualTo(1));
        Assert.That(result.RemainingBytes, Is.EqualTo(10));
    }

    [Test]
    public void Prune_MissingDirectoryDoesNotCreateIt()
    {
        var missing = Path.Combine(directory, "missing");
        Assert.That(TileCache.Prune(missing, 0).RemainingBytes, Is.Zero);
        Assert.That(Directory.Exists(missing), Is.False);
    }

    [Test]
    public void Prune_RejectsInvalidLimitsBeforeDeletingAnything()
    {
        var tile = WriteTile(1, 10, 1);
        Assert.That(() => TileCache.Prune(directory, -1), Throws.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(() => TileCache.Prune(directory, 0, TimeSpan.FromSeconds(-1)), Throws.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(() => TileCache.Prune("", 0), Throws.TypeOf<ArgumentException>());
        Assert.That(File.Exists(tile), Is.True);
    }

    [Test]
    public void Prune_RejectsLinkedDirectoryAndPreservesFileLinkTarget()
    {
        var target = WriteTile(1, 10, 3);
        var linkedDirectory = Path.Combine(directory, "linked-directory");
        var linkedFile = Path.Combine(directory, $"2x2-8-{new string('b', 12)}.png");
        try
        {
            try
            {
                Directory.CreateSymbolicLink(linkedDirectory, directory);
                File.CreateSymbolicLink(linkedFile, target);
            }
            catch (UnauthorizedAccessException)
            {
                Assert.Ignore("Creating symbolic links requires platform privileges.");
            }
            Assert.That(() => TileCache.Prune(linkedDirectory, 0), Throws.TypeOf<IOException>());
            // Keep the target outside this pass by renaming it to a non-cache filename.
            var outside = Path.Combine(directory, "keep.dat");
            File.Delete(linkedFile);
            File.Move(target, outside);
            File.CreateSymbolicLink(linkedFile, outside);
            var result = TileCache.Prune(directory, 0);
            Assert.That(result.DeletedFiles, Is.Zero);
            Assert.That(File.Exists(linkedFile), Is.True);
            Assert.That(File.ReadAllBytes(outside), Has.Length.EqualTo(10));
        }
        finally
        {
            if (File.Exists(linkedFile)) File.Delete(linkedFile);
            if (Directory.Exists(linkedDirectory)) Directory.Delete(linkedDirectory);
        }
    }

    private string WriteTile(int x, int bytes, int daysOld, int hashLength = 12)
    {
        var path = Path.Combine(directory, $"{x}x2-8-{new string('a', hashLength)}.png");
        File.WriteAllBytes(path, new byte[bytes]);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-daysOld));
        return path;
    }
}
