namespace PoQMon
open System
open System.IO
open System.Threading.Tasks
open Microsoft.Extensions.Options
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.FSharpLu

type LogFileWatcherOptions() =
    member val DataDir = "" with get,set
    member val LogFormat = "csv" with get,set
    member val FrequencyMs = 1000 with get,set

type private FileStatus =
    { fileInfo : FileInfo
      offset : int64 }

type LogFileWatcher(logger:ILogger<LogFileWatcher>, optionsMonitor:IOptionsMonitor<LogFileWatcherOptions>) =
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

            return (fstatus,buf)
        }

    let publishChanges (fstatus:FileStatus,newdata:byte[]) =
        async {
            // publish here
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

            // add new statuses to the map
            let combinedMap =
                entries
                |> Seq.filter (fun fi -> not (fsMap.ContainsKey fi.FullName))
                |> Seq.map (fun fi -> { fileInfo = fi; offset = 0L })
                |> Seq.fold (fun map fs -> Map.add fs.fileInfo.FullName fs map) fsMap

            // snapshot the time *before* reading to make sure there aren't any little gaps
            let newSince = DateTime.UtcNow

            // open the files, get changes since last status
            let! statuses =
                Map.toSeq combinedMap
                |> Seq.map (fun (_,fs) -> getChanges fs |> Async.bind publishChanges)
                |> Async.Parallel

            // create updated map
            let newMap =
                statuses
                |> Array.fold (fun map fs -> Map.add fs.fileInfo.FullName fs map) Map.empty

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