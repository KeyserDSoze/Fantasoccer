using Azure;
using Fantasoccer.StorageMigration;

return await RunAsync(args);

static async Task<int> RunAsync(string[] arguments)
{
    if (arguments.Length == 0 || arguments is ["--help"])
    {
        Console.WriteLine("""
            Fantasoccer Storage migration tools (Blob + Table current state only)
            export --directory /absolute/protected/path [--resume] [--source-quiesced] [--allow-remote-source] [--include-tables]
              Blob-only by default; --include-tables explicitly queries Table Storage too.
              Reads FANTASOCCER_EXPORT_STORAGE from the environment. Source is never modified.
            verify --directory /absolute/protected/path
              Verifies manifest, payload checksums, Table types and counts; no network needed.
            restore-local --directory /absolute/protected/path [--public-local-files] [--include-tables]
              Reads FANTASOCCER_RESTORE_STORAGE; only dedicated local Azurite accounts accepted.
            Excludes queues, file shares, old blob versions/snapshots, tags and access policies.
            This is a migration dataset, NOT a full Azure account disaster-recovery backup.
            """);
        return arguments.Length == 0 ? 2 : 0;
    }

    using var cancellation = new CancellationTokenSource();
    ConsoleCancelEventHandler cancel = (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
    Console.CancelKeyPress += cancel;
    try
    {
        var command = arguments[0];
        if (command is not ("export" or "verify" or "restore-local")) throw new ArgumentException("Unknown command.");
        string? directory = null;
        var flags = new HashSet<string>();
        for (var index = 1; index < arguments.Length; index++)
        {
            if (arguments[index] == "--directory" && directory is null && index + 1 < arguments.Length)
                directory = arguments[++index];
            else if (command == "export" && arguments[index] is "--resume" or "--source-quiesced" or "--allow-remote-source" or "--include-tables")
            {
                if (!flags.Add(arguments[index])) throw new ArgumentException("Duplicate option.");
            }
            else if (command == "restore-local" && arguments[index] is "--public-local-files" or "--include-tables")
            {
                if (!flags.Add(arguments[index])) throw new ArgumentException("Duplicate option.");
            }
            else throw new ArgumentException("Invalid option.");
        }
        if (directory is null) throw new ArgumentException("Missing --directory.");
        DumpManifest manifest;
        switch (command)
        {
            case "export":
                manifest = await new StorageExporter(Endpoints("FANTASOCCER_EXPORT_STORAGE")).ExportAsync(directory,
                    flags.Contains("--resume"), flags.Contains("--source-quiesced"), flags.Contains("--allow-remote-source"), cancellation.Token,
                    includeTables: flags.Contains("--include-tables"));
                break;
            case "restore-local":
                manifest = await DumpArchive.RestoreLocalAsync(directory, Endpoints("FANTASOCCER_RESTORE_STORAGE"), cancellation.Token,
                    publicLocalFiles: flags.Contains("--public-local-files"), includeTables: flags.Contains("--include-tables"));
                break;
            default:
                manifest = await DumpArchive.VerifyAsync(directory, cancellation.Token);
                break;
        }
        Console.WriteLine($"{command}: OK; exportId={manifest.ExportId}; containers={manifest.Containers.Count}; " +
                          $"blobs={manifest.Containers.Sum(x => x.Blobs.Count)}; tables={manifest.Tables.Count}; " +
                          $"entities={manifest.Tables.Sum(x => x.EntityCount)}.");
        Console.WriteLine($"Archive scope: {manifest.Scope}; see exclusions in manifest. Table download/restore requires --include-tables. Consistency requires all source writers to be stopped.");
        return 0;
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine("Operation cancelled. Export may be resumed with --resume.");
        return 130;
    }
    catch (Exception exception)
    {
        // Azure/IO exception messages can contain signed URLs, account details or data.
        // Never print the raw exception, arguments, environment or connection strings.
        Console.Error.WriteLine(exception is RequestFailedException storage
            ? $"Operation failed: Storage HTTP {storage.Status} ({storage.ErrorCode ?? "Unknown"})."
            : $"Operation failed ({exception.GetType().Name}). Check command options, protected manifest and environment configuration.");
        return 1;
    }
    finally { Console.CancelKeyPress -= cancel; }

    static StorageEndpoints Endpoints(string variable) => new(Environment.GetEnvironmentVariable(variable)
        ?? throw new InvalidOperationException($"Missing {variable}."));
}
