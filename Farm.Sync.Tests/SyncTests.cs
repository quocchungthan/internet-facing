using System.Text.Json;
using AffineSync;
using Farm.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AffineSync.Tests;

public sealed class SyncTests
{
    private static readonly DateTimeOffset Timestamp = DateTimeOffset.Parse("2026-01-02T03:04:05Z");
    private static readonly CancellationToken Token = CancellationToken.None;

    [Fact]
    public async Task MissingCheckpointUsesBaselineAndAtomicWritesRoundTrip()
    {
        using var files = new TestFiles();
        var store = new CheckpointStore(files.Path("state.json"));
        Assert.Equal(CheckpointStore.Baseline, await store.ReadAsync(Token));
        await store.WriteAsync(Timestamp, Token);
        await store.WriteAsync(Timestamp.AddSeconds(1), Token);
        Assert.Equal(Timestamp.AddSeconds(1), await store.ReadAsync(Token));
        Assert.Single(Directory.GetFiles(files.Directory));
    }

    [Fact]
    public async Task CorruptCheckpointFailsInsteadOfResetting()
    {
        using var files = new TestFiles();
        await File.WriteAllTextAsync(files.Path("state.json"), "{}");
        await Assert.ThrowsAsync<JsonException>(() => new CheckpointStore(files.Path("state.json")).ReadAsync(Token));
    }

    [Fact]
    public async Task CancelledCheckpointWriteRetainsPreviousStateAndCleansStagingFile()
    {
        using var files = new TestFiles();
        var store = new CheckpointStore(files.Path("state.json"));
        await store.WriteAsync(Timestamp, Token);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.WriteAsync(Timestamp.AddDays(1), cancelled.Token));
        Assert.Equal(Timestamp, await store.ReadAsync(Token));
        Assert.Single(Directory.GetFiles(files.Directory));
    }

    [Fact]
    public void ConfigurationDefaultsAndValidation()
    {
        string[] names = ["DATABASE_CONNECTION_STRING", "SYNC_INTERVAL_SECONDS", "SYNC_STATE_PATH", "AFFINE_READER_PATH"];
        var original = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var name in names) Environment.SetEnvironmentVariable(name, null);
            Assert.Throws<InvalidOperationException>(WorkerConfiguration.FromEnvironment);
            Environment.SetEnvironmentVariable("DATABASE_CONNECTION_STRING", "Host=localhost;Database=test");
            var configuration = WorkerConfiguration.FromEnvironment();
            Assert.Equal(60, configuration.IntervalSeconds);
            Assert.Equal("/state/checkpoint.json", configuration.StatePath);
            Assert.Equal("reader/export.mjs", configuration.ReaderPath);
            foreach (var value in new[] { "0", "-1", "invalid" })
            {
                Environment.SetEnvironmentVariable("SYNC_INTERVAL_SECONDS", value);
                Assert.Throws<InvalidOperationException>(WorkerConfiguration.FromEnvironment);
            }
        }
        finally
        {
            foreach (var name in names) Environment.SetEnvironmentVariable(name, original[name]);
        }
    }

    [Fact]
    public void OwnershipLockPreventsConcurrentWorkers()
    {
        using var files = new TestFiles();
        var store = new CheckpointStore(files.Path("state.json"));
        using var ownership = store.AcquireLock();
        Assert.Throws<IOException>(() => store.AcquireLock());
    }

    [Fact]
    public async Task UnchangedDocumentsAreFilteredAndNewerDocumentIsWritten()
    {
        var state = new MemoryState { Timestamp = Timestamp };
        var writer = new RecordingWriter();
        var reader = new FixedReader(SnapshotOf(Document("old", Timestamp.AddSeconds(-1)), Document("equal", Timestamp)));
        var runner = new SyncRunner(reader, state, writer);
        Assert.Equal(2, await runner.RunAsync(Token));
        reader.Snapshot = SnapshotOf(Document("old", Timestamp.AddSeconds(-1)), Document("equal", Timestamp),
            Document("new", Timestamp.AddSeconds(1)));
        Assert.Equal(1, await runner.RunAsync(Token));
        Assert.Equal("new", Assert.Single(writer.Documents).Id);
        Assert.Equal(Timestamp.AddSeconds(1), state.Timestamp);
        Assert.Equal(0, await runner.RunAsync(Token));
        Assert.Equal(2, writer.Calls);
    }

    [Fact]
    public async Task EmptySnapshotDoesNotAdvanceCheckpoint()
    {
        var state = new MemoryState();
        var writer = new RecordingWriter();
        Assert.Equal(0, await new SyncRunner(new FixedReader(SnapshotOf()), state, writer).RunAsync(Token));
        Assert.Equal(CheckpointStore.Baseline, state.Timestamp);
        Assert.Equal(0, writer.Calls);
    }

    [Fact]
    public async Task ReaderFailureDoesNotAdvanceAndCanRetry()
    {
        var state = new MemoryState();
        var writer = new RecordingWriter();
        var reader = new FixedReader(SnapshotOf(Document("one", Timestamp))) { Fail = true };
        var runner = new SyncRunner(reader, state, writer);
        await Assert.ThrowsAsync<IOException>(() => runner.RunAsync(Token));
        Assert.Equal(CheckpointStore.Baseline, state.Timestamp);
        Assert.Equal(0, writer.Calls);
        reader.Fail = false;
        Assert.Equal(1, await runner.RunAsync(Token));
        Assert.Equal(Timestamp, state.Timestamp);
    }

    [Fact]
    public async Task DatabaseFailureDoesNotAdvanceAndCanRetry()
    {
        var state = new MemoryState();
        var writer = new RecordingWriter { Fail = true };
        var runner = new SyncRunner(new FixedReader(SnapshotOf(Document("one", Timestamp))), state, writer);
        await Assert.ThrowsAsync<IOException>(() => runner.RunAsync(Token));
        Assert.Equal(CheckpointStore.Baseline, state.Timestamp);
        writer.Fail = false;
        Assert.Equal(1, await runner.RunAsync(Token));
        Assert.Equal(2, writer.Calls);
        Assert.Equal(Timestamp, state.Timestamp);
    }

    [Fact]
    public async Task CheckpointFailureReplaysAfterDatabaseCommit()
    {
        var state = new MemoryState { FailWrites = true };
        var writer = new RecordingWriter();
        var runner = new SyncRunner(new FixedReader(SnapshotOf(Document("one", Timestamp))), state, writer);
        await Assert.ThrowsAsync<IOException>(() => runner.RunAsync(Token));
        Assert.Equal(1, writer.Calls);
        Assert.Equal(CheckpointStore.Baseline, state.Timestamp);
        state.FailWrites = false;
        Assert.Equal(1, await runner.RunAsync(Token));
        Assert.Equal(2, writer.Calls);
        Assert.Equal(Timestamp, state.Timestamp);
    }

    [Fact]
    public async Task SnapshotBelowGlobalWatermarkStillProcessesUnknownDocuments()
    {
        var state = new MemoryState { Timestamp = Timestamp.AddDays(2) };
        var writer = new RecordingWriter();
        Assert.Equal(1, await new SyncRunner(new FixedReader(SnapshotOf(Document("late", Timestamp))), state, writer).RunAsync(Token));
        Assert.Equal(1, writer.Calls);
        Assert.Equal(Timestamp.AddDays(2), state.Timestamp);
    }

    [Fact]
    public async Task LateCommitsAndEqualTimestampContentChangesAreNotLost()
    {
        using var files = new TestFiles();
        var state = new CheckpointStore(files.Path("state.json"));
        var writer = new RecordingWriter();
        var reader = new FixedReader(SnapshotOf(
            Document("slow", Timestamp.AddHours(-2), [Metadata(1) with { UpdatedAtUtc = Timestamp.AddHours(-2) }]),
            Document("fast", Timestamp)));
        var runner = new SyncRunner(reader, state, writer);
        Assert.Equal(2, await runner.RunAsync(Token));
        reader.Snapshot = SnapshotOf(
            Document("slow", Timestamp.AddHours(-1), [Metadata(1) with { UpdatedAtUtc = Timestamp.AddHours(-1) }]),
            Document("fast", Timestamp));
        Assert.Equal(1, await runner.RunAsync(Token));
        Assert.Equal("slow", Assert.Single(writer.Documents).Id);
        reader.Snapshot = reader.Snapshot with
        {
            Documents = [reader.Snapshot.Documents[0] with
            {
                Metadata = [reader.Snapshot.Documents[0].Metadata[0] with { Title = "Late equal-timestamp content" }]
            }, reader.Snapshot.Documents[1]]
        };
        Assert.Equal(1, await runner.RunAsync(Token));
        Assert.Equal(0, await new SyncRunner(reader, new CheckpointStore(files.Path("state.json")), writer).RunAsync(Token));
        var persisted = await state.LoadAsync(Token);
        Assert.Equal(Timestamp, persisted.CapturedAtUtc);
        Assert.Equal(Timestamp.AddHours(-1), persisted.Documents!["slow"].UpdatedAtUtc);
        Assert.Equal(64, persisted.Documents["slow"].Sha256.Length);
    }

    [Fact]
    public async Task ReaderDependencyHashChangesTriggerReplayBelowPreviousTimestamp()
    {
        using var files = new TestFiles();
        var store = new CheckpointStore(files.Path("state.json"));
        var reader = new FixedReader(SnapshotOf(Document("one", Timestamp) with { ContentHash = "dependency-version-a" }));
        var writer = new RecordingWriter();
        var runner = new SyncRunner(reader, store, writer);
        Assert.Equal(1, await runner.RunAsync(Token));
        reader.Snapshot = SnapshotOf(Document("one", Timestamp.AddSeconds(-1)) with { ContentHash = "dependency-version-b" });
        Assert.Equal(1, await runner.RunAsync(Token));
        Assert.Equal("dependency-version-b", (await store.LoadAsync(Token)).Documents!["one"].ContentHash);
        Assert.Equal(0, await new SyncRunner(reader, new CheckpointStore(files.Path("state.json")), writer).RunAsync(Token));
    }

    [Fact]
    public void ContractAcceptsPascalCaseRowsAndRejectsMissingRequiredFields()
    {
        var snapshot = SnapshotOf(Document("one", Timestamp, [Metadata(1)], [Bookmark(1)]));
        Assert.Equal(1, Assert.Single(Snapshot.Parse(JsonSerializer.Serialize(snapshot)).Documents).Metadata[0].Id);
        Assert.Throws<JsonException>(() => Snapshot.Parse("""{"capturedAtUtc":"2026-01-01T00:00:00Z"}"""));
        var incomplete = JsonSerializer.Serialize(snapshot).Replace("\"IsActive\":false,", "");
        Assert.Throws<JsonException>(() => Snapshot.Parse(incomplete));
        Assert.Throws<JsonException>(() => Snapshot.Parse("not JSON"));
        Assert.Throws<InvalidDataException>(() => Snapshot.Parse("null"));
    }

    [Fact]
    public void ValidationRejectsDuplicatesAndInvalidFields()
    {
        Assert.Throws<InvalidDataException>(() => SnapshotOf(Document("same", Timestamp), Document("same", Timestamp)).Validate());
        Assert.Throws<InvalidDataException>(() => SnapshotOf(Document("one", Timestamp, [Metadata(1)]),
            Document("two", Timestamp, [Metadata(1)])).Validate());
        Assert.Throws<InvalidDataException>(() => SnapshotOf(Document("one", Timestamp, [Metadata(0)])).Validate());
        Assert.Throws<InvalidDataException>(() => SnapshotOf(Document("one", Timestamp, [Metadata(1) with { Title = new string('a', 257) }])).Validate());
        Assert.Throws<InvalidDataException>(() => SnapshotOf(Document("one", Timestamp, [Metadata(1) with { Description = null! }])).Validate());
        Assert.Throws<InvalidDataException>(() => SnapshotOf(Document("one", Timestamp, bookmarks: [Bookmark(1), Bookmark(1)])).Validate());
        Assert.Throws<InvalidDataException>(() => SnapshotOf(Document("one", Timestamp, bookmarks: [Bookmark(1) with { CreatedAtUtc = Timestamp.AddDays(2) }])).Validate());
    }

    [Fact]
    public void ValidationRejectsUnsafeTimestamps()
    {
        Assert.Throws<InvalidDataException>(() => SnapshotOf(Document("one", Timestamp.AddDays(2))).Validate());
        Assert.Throws<InvalidDataException>(() => SnapshotOf(Document("one", Timestamp.ToOffset(TimeSpan.FromHours(1)))).Validate());
        Assert.Throws<InvalidDataException>(() => SnapshotOf(Document("one", default)).Validate());
        Assert.Throws<InvalidDataException>(() => SnapshotOf(Document("one", Timestamp, [Metadata(1) with { UpdatedAtUtc = Timestamp.AddDays(1) }])).Validate());
        Assert.Throws<InvalidDataException>(() => (SnapshotOf() with { CapturedAtUtc = DateTimeOffset.UtcNow.AddDays(1) }).Validate());
    }

    [Fact]
    public async Task UpsertsAreIdempotentPreserveCreationAndDoNotDeleteMissingRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AuraFarming>().UseSqlite(connection).Options;
        await using (var database = new AuraFarming(options))
        {
            await database.Database.EnsureCreatedAsync();
            await database.WebsiteMetadata.ExecuteDeleteAsync();
            await database.PlatformBookmarks.ExecuteDeleteAsync();
        }
        var writer = new DatabaseWriter(() => new AuraFarming(options));
        var first = Document("one", Timestamp, [Metadata(1), Metadata(2)], [Bookmark(1), Bookmark(2)]);
        await writer.WriteAsync([first], Token);
        await writer.WriteAsync([first], Token);
        var changed = Document("one", Timestamp.AddDays(1),
            [Metadata(1) with { CreatedAtUtc = Timestamp, UpdatedAtUtc = Timestamp.AddDays(1), Title = "Updated" }],
            [Bookmark(1) with { CreatedAtUtc = Timestamp, IsActive = true, DisplayOrder = 7 }]);
        await writer.WriteAsync([changed], Token);
        await writer.WriteAsync([changed], Token);
        await using var verify = new AuraFarming(options);
        Assert.Equal(2, await verify.WebsiteMetadata.CountAsync());
        Assert.Equal(2, await verify.PlatformBookmarks.CountAsync());
        var metadata = await verify.WebsiteMetadata.SingleAsync(row => row.Id == 1);
        var bookmark = await verify.PlatformBookmarks.SingleAsync(row => row.Id == 1);
        Assert.Equal("Updated", metadata.Title);
        Assert.Equal(Timestamp.AddDays(-1).UtcDateTime, metadata.CreatedAtUtc);
        Assert.Equal(Timestamp.AddDays(-1).UtcDateTime, bookmark.CreatedAtUtc);
        Assert.True(bookmark.IsActive);
        Assert.Equal(7, bookmark.DisplayOrder);
        Assert.False((await verify.PlatformBookmarks.SingleAsync(row => row.Id == 2)).IsActive);
    }

    [Fact]
    public async Task FailureRollsBackAllDocuments()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AuraFarming>().UseSqlite(connection).Options;
        await using (var database = new AuraFarming(options))
        {
            await database.Database.EnsureCreatedAsync();
            await database.WebsiteMetadata.ExecuteDeleteAsync();
            await database.PlatformBookmarks.ExecuteDeleteAsync();
            await database.Database.ExecuteSqlRawAsync(
                """CREATE TRIGGER reject_second BEFORE INSERT ON "WebsiteMetadata" WHEN NEW."Id" = 2 BEGIN SELECT RAISE(ABORT, 'forced test failure'); END;""");
        }
        var writer = new DatabaseWriter(() => new AuraFarming(options));
        await Assert.ThrowsAsync<DbUpdateException>(() => writer.WriteAsync([
            Document("one", Timestamp, [Metadata(1)]), Document("two", Timestamp, [Metadata(2)])], Token));
        await using var verify = new AuraFarming(options);
        Assert.Equal(0, await verify.WebsiteMetadata.CountAsync());
    }

    [Fact]
    public async Task InvalidDocumentIsSkippedAndOthersStillSync()
    {
        var state = new MemoryState();
        var writer = new RecordingWriter();
        var skipped = new List<string>();
        var reader = new FixedReader(SnapshotOf(
            Document("good", Timestamp), Document("bad", Timestamp, [Metadata(0)]), Document("good2", Timestamp, [Metadata(7)])));
        var runner = new SyncRunner(reader, state, writer) { ReportSkipped = (id, _) => skipped.Add(id) };
        Assert.Equal(2, await runner.RunAsync(Token));
        Assert.Equal(["bad"], skipped);
        Assert.Equal(["good", "good2"], writer.Documents.Select(document => document.Id).Order());
    }

    [Fact]
    public async Task DuplicateIdAcrossDocumentsSkipsOnlyTheLaterOne()
    {
        var writer = new RecordingWriter();
        var skipped = new List<string>();
        var reader = new FixedReader(SnapshotOf(Document("a", Timestamp, [Metadata(1)]), Document("b", Timestamp, [Metadata(1)])));
        Assert.Equal(1, await new SyncRunner(reader, new MemoryState(), writer) { ReportSkipped = (id, _) => skipped.Add(id) }.RunAsync(Token));
        Assert.Equal(["b"], skipped);
    }

    [Fact]
    public async Task SnapshotLevelFaultStillFailsThePass()
    {
        var future = SnapshotOf(Document("a", Timestamp)) with { CapturedAtUtc = DateTimeOffset.UtcNow.AddDays(1) };
        await Assert.ThrowsAsync<InvalidDataException>(() => new SyncRunner(new FixedReader(future), new MemoryState(), new RecordingWriter()).RunAsync(Token));
    }

    [Fact]
    public void HealthcheckTracksLastSuccessAndBackoffIsCapped()
    {
        using var files = new TestFiles();
        var health = new SyncHealth(files.Path("state.json"));
        var maxAge = SyncHealth.MaxAge(60);
        Assert.False(health.IsHealthy(Timestamp, maxAge));
        health.MarkSuccess(Timestamp);
        Assert.True(health.IsHealthy(Timestamp.Add(maxAge), maxAge));
        Assert.False(health.IsHealthy(Timestamp.Add(maxAge).AddSeconds(1), maxAge));
        Assert.Equal(TimeSpan.FromSeconds(60), SyncHealth.Backoff(60, 0));
        Assert.Equal(TimeSpan.FromSeconds(240), SyncHealth.Backoff(60, 2));
        Assert.Equal(TimeSpan.FromSeconds(900), SyncHealth.Backoff(60, 30));
    }

    [Fact]
    public async Task NodeInvocationSupportsSpacesAndRejectsNonzeroExitAndBadJson()
    {
        using var files = new TestFiles();
        var path = files.Path("reader with spaces.mjs");
        var snapshot = SnapshotOf(Document("one", Timestamp));
        await File.WriteAllTextAsync(path, $"console.log({JsonSerializer.Serialize(JsonSerializer.Serialize(snapshot))});");
        Assert.Equal(Timestamp, (await new NodeReader(path).ReadAsync(Token)).Documents[0].UpdatedAtUtc);
        await File.WriteAllTextAsync(path, "console.error('expected test diagnostic'); process.exit(3);");
        await Assert.ThrowsAsync<InvalidOperationException>(() => new NodeReader(path).ReadAsync(Token));
        await File.WriteAllTextAsync(path, "console.log('invalid');");
        await Assert.ThrowsAsync<JsonException>(() => new NodeReader(path).ReadAsync(Token));
    }

    [Fact]
    public async Task NodeCancellationKillsChildProcess()
    {
        using var files = new TestFiles();
        var path = files.Path("never-completes.mjs");
        await File.WriteAllTextAsync(path, "setInterval(() => {}, 1000);");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new NodeReader(path).ReadAsync(cancellation.Token));
    }

    [Theory]
    [InlineData("https://github.com/o/r/issues/7", true)]
    [InlineData("https://github.com/o/r/pull/7", false)]
    [InlineData("https://example.com/o/r/issues/7", false)]
    public void SpecifiedIssueUrlMustBeAGitHubIssueLink(string url, bool valid)
    {
        var snapshot = SnapshotOf(Document("doc", Timestamp, [Metadata(1) with { SpecifiedIssueUrl = url }]));
        if (valid) snapshot.Validate();
        else Assert.Throws<InvalidDataException>(snapshot.Validate);
    }

    private static Snapshot SnapshotOf(params SourceDocument[] documents) =>
        new() { CapturedAtUtc = Timestamp.AddDays(1), Documents = documents.ToList() };
    private static SourceDocument Document(string id, DateTimeOffset updated, List<MetadataRow>? metadata = null, List<BookmarkRow>? bookmarks = null) =>
        new() { Id = id, UpdatedAtUtc = updated, Metadata = metadata ?? [], Bookmarks = bookmarks ?? [] };
    private static MetadataRow Metadata(int id) => new()
    {
        Id = id, DeveloperName = "Developer", Title = "Title", Description = "Description",
        RepositoryUrl = "https://github.com/example/repository",
        CreatedAtUtc = Timestamp.AddDays(-1), UpdatedAtUtc = Timestamp
    };
    private static BookmarkRow Bookmark(int id) => new()
    {
        Id = id, Platform = "GitHub", Url = "https://github.com/example", PlatformId = "example",
        Description = "", Color = "#000000", DisplayOrder = 0, IsActive = false, CreatedAtUtc = Timestamp.AddDays(-1)
    };

    private sealed class FixedReader(Snapshot snapshot) : ISnapshotReader
    {
        public bool Fail { get; set; }
        public Snapshot Snapshot { get; set; } = snapshot;
        public Task<Snapshot> ReadAsync(CancellationToken cancellationToken) =>
            Fail ? throw new IOException("reader failure") : Task.FromResult(Snapshot);
    }

    private sealed class MemoryState : ICheckpointStore
    {
        public DateTimeOffset Timestamp { get; set; } = CheckpointStore.Baseline;
        public bool FailWrites { get; set; }
        public Dictionary<string, DocumentCheckpoint>? Documents { get; private set; }
        public Task<SyncCheckpoint> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new SyncCheckpoint { CapturedAtUtc = Timestamp, Documents = Documents });
        public async Task SaveAsync(SyncCheckpoint checkpoint, CancellationToken cancellationToken)
        {
            await WriteAsync(checkpoint.CapturedAtUtc, cancellationToken);
            Documents = checkpoint.Documents;
        }
        public Task<DateTimeOffset> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Timestamp);
        public Task WriteAsync(DateTimeOffset timestamp, CancellationToken cancellationToken)
        {
            if (FailWrites) throw new IOException("checkpoint failure");
            Timestamp = timestamp;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingWriter : IDatabaseWriter
    {
        public bool Fail { get; set; }
        public int Calls { get; private set; }
        public IReadOnlyList<SourceDocument> Documents { get; private set; } = [];
        public Task WriteAsync(IReadOnlyList<SourceDocument> documents, CancellationToken cancellationToken)
        {
            Calls++;
            if (Fail) throw new IOException("database failure");
            Documents = documents;
            return Task.CompletedTask;
        }
    }

    private sealed class TestFiles : IDisposable
    {
        public string Directory { get; } = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), ".test-artifacts", Guid.NewGuid().ToString("N"));
        public TestFiles() => System.IO.Directory.CreateDirectory(Directory);
        public string Path(string name) => System.IO.Path.Combine(Directory, name);
        public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
    }
}
