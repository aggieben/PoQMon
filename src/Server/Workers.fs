namespace PoQMon
open System.IO
open System.Threading.Tasks
open Microsoft.Extensions.Options
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

type LogFileWatcherOptions() =
    member val DataDir = "" with get,set
    member val LogFormat = "" with get,set

type LogFileWatcher(logger:ILogger<LogFileWatcher>, optionsMonitor:IOptionsMonitor<LogFileWatcherOptions>) =
    inherit BackgroundService()

    let handleFileChanged (fsea:FileSystemEventArgs) =
        sprintf "%s changed: %O" fsea.FullPath fsea.ChangeType
        |> logger.LogTrace

    let handlePathCreated (fsea:FileSystemEventArgs) =
        sprintf "%s created: %O" fsea.FullPath fsea.ChangeType
        |> logger.LogTrace

    let handlePathRenamed (rea:RenamedEventArgs) =
        sprintf "%s renamed to %s (%O)" rea.OldFullPath rea.FullPath rea.ChangeType
        |> logger.LogTrace

    let handlePathDeleted (fsea:FileSystemEventArgs) =
        sprintf "%s deleted (%O)" fsea.FullPath fsea.ChangeType
        |> logger.LogTrace

    let configureWatcher path format =
        sprintf "configuring watcher with path: '%s' and format: '%s'" path format
        |> logger.LogTrace

        if not (Directory.Exists(path)) then
            failwithf "data directory does not exist: %s" path

        let watcher = new FileSystemWatcher()
        watcher.Path <- path
        watcher.IncludeSubdirectories <- true
        watcher.Filter <- "*.csv"
        watcher.NotifyFilter <- watcher.NotifyFilter ||| NotifyFilters.LastAccess
        watcher.NotifyFilter <- watcher.NotifyFilter ||| NotifyFilters.Size

        watcher.Changed.Add(handleFileChanged)
        watcher.Created.Add(handlePathCreated)
        watcher.Renamed.Add(handlePathRenamed)
        watcher.Deleted.Add(handlePathDeleted)
        watcher

    let _options = optionsMonitor.CurrentValue
    let _watcher = configureWatcher _options.DataDir _options.LogFormat

    override __.Dispose() =
        _watcher.Dispose()
        base.Dispose()

    override __.ExecuteAsync(stopToken) =
        async {
            logger.LogTrace "enabling filewatcher events..."
            _watcher.EnableRaisingEvents <- true
            while not stopToken.IsCancellationRequested do
                do! Async.Sleep 500
            logger.LogTrace "cancelling..."
            _watcher.EnableRaisingEvents <- false
        }
        |> Async.StartAsTask
        :> Task