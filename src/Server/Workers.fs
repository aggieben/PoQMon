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
        sprintf "handleFileChanged: %A" fsea
        |> logger.LogTrace

    let handlePathCreated (fsea:FileSystemEventArgs) =
        sprintf "handlePathCreated: %A" fsea
        |> logger.LogTrace

    let handlePathRenamed (fsea:FileSystemEventArgs) =
        sprintf "handlePathRenamed: %A" fsea
        |> logger.LogTrace

    let handlePathDeleted (fsea:FileSystemEventArgs) =
        sprintf "handlePathDeleted: %A" fsea
        |> logger.LogTrace

    let configureWatcher path format =
        sprintf "configuring watcher with path: '%s' and format: '%s'" path format
        |> logger.LogTrace

        if not (Directory.Exists(path)) then
            failwithf "data directory does not exist: %s" path

        let watcher = new FileSystemWatcher()
        watcher.Path <- path
        // watcher.Path <- (Path.Join(path,"log"))
        // watcher.Filter <-
        //     match format with
        //     | "csv" -> "postgresql-*.csv"
        //     | _ -> failwith "unsupported log format"
        watcher.Filter <- ""
        watcher.IncludeSubdirectories <- true
        watcher.Changed.Add(handleFileChanged)
        watcher.Created.Add(handlePathCreated)
        watcher.Renamed.Add(handlePathRenamed)
        watcher.Deleted.Add(handlePathDeleted)
        watcher

    override this.ExecuteAsync(stopToken) =
        let options = optionsMonitor.CurrentValue

        use watcher = configureWatcher options.DataDir options.LogFormat
        async {
            logger.LogTrace "enabling filewatcher events..."
            watcher.EnableRaisingEvents <- true
            while not stopToken.IsCancellationRequested do
                do! Async.Sleep 500
            logger.LogTrace "cancelling..."
            watcher.EnableRaisingEvents <- false
        }
        |> Async.StartAsTask
        :> Task