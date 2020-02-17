namespace PoQMon
open System
open System.IO
open System.Threading.Tasks
open Microsoft.Extensions.Options
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.FSharpLu
open Shared

type LogFileWatcherOptions() =
    member val DataDir = "" with get,set
    member val LogFormat = "csv" with get,set
    member val FrequencyMs = 1000 with get,set

type private FileStatus =
    { fileInfo : FileInfo
      offset : int64 }

type LogFileWatcher(logger:ILogger<LogFileWatcher>,
                    optionsMonitor:IOptionsMonitor<LogFileWatcherOptions>,
                    updater:MailboxProcessor<LogfileUpdate>) =
    inherit BackgroundService()

    let _options = optionsMonitor.CurrentValue

    let getFormatPattern =
        function
        | "csv" -> "*.csv"
        | f -> failwithf "unsupported format: %s" f


    // FileStatus -> {| status; content; |}
    let getChanges (fstatus:FileStatus) =
        async {
            let fs = fstatus.fileInfo.OpenRead()
            fs.Seek(fstatus.offset, SeekOrigin.Begin) |> ignore
            let buf = fs.Length - fstatus.offset |> (int >> Array.zeroCreate)
            let! _ = fs.ReadAsync(buf, int fstatus.offset, buf.Length) |> Async.AwaitTask

            return ({fstatus with offset = fstatus.offset + (int64 buf.Length)} ,buf)
        }

    let publishChanges (fstatus:FileStatus,newdata:byte[]) =
        async {
            { timestamp = DateTimeOffset fstatus.fileInfo.LastWriteTimeUtc
              path = fstatus.fileInfo.FullName
              data = newdata }
            |> updater.Post
            return fstatus
        }

    let rec queryFiles (dinfo:DirectoryInfo) (pattern:string) (fsMap:Map<string,FileStatus>) since =
        async {
            let entries =
                query {
                    for f in dinfo.EnumerateFiles(pattern) do
                    where (f.CreationTime > since
                           || f.LastWriteTime > since)
                    select f
                }

            // snapshot the time *before* reading to make sure there aren't any little gaps
            let newSince = DateTime.UtcNow

            let! statuses =
                entries
                |> Seq.map
                    (fun fi ->
                        match Map.tryFind fi.FullName fsMap with
                        | Some fs -> { fs with fileInfo = fi }
                        | None -> { fileInfo = fi; offset = 0L }
                        |> getChanges
                        |> Async.bind publishChanges)
                |> Async.Parallel

            let newMap =
                statuses
                |> Seq.fold
                     (fun map fs ->
                        let key = fs.fileInfo.FullName
                        match Map.tryFind key fsMap with
                        | Some _ ->
                            Map.remove key map
                            |> Map.add key fs
                        | None ->
                            Map.add key fs map)
                     Map.empty

            do! Async.Sleep _options.FrequencyMs

            return! queryFiles dinfo pattern newMap newSince
        }

    override __.ExecuteAsync(stopToken) =
        async {
            logger.LogTrace "monitoring log files"
            let dirinfo = DirectoryInfo(Path.Combine(_options.DataDir, "log"))

            if not dirinfo.Exists then
                failwithf "%s does not exist" dirinfo.FullName

            let pattern = getFormatPattern _options.LogFormat

            do! queryFiles dirinfo pattern Map.empty DateTime.Now
        }
        |> Async.StartAsTask
        :> Task