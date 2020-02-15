namespace PoQMon
open System
open System.IO
open System.Threading.Tasks
open Microsoft.Extensions.Options
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

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

    // FileStatus -> {| status; content; |}
    let getChanges (fstatus:FileStatus) =
        async {
            let fs = fstatus.fileInfo.OpenRead()
            fs.Seek(fstatus.offset, SeekOrigin.Begin) |> ignore
        }


    let rec queryFiles (dinfo:DirectoryInfo) pattern fsMap since =
        let entries =
            query {
                for f in dinfo.EnumerateFiles(pattern) do
                where (f.CreationTime > since
                       || f.LastWriteTime > since)
                select f
            }

        ()
        // open the files, get changes since last status


        // fire events?  publish to a bus?

        // update fsMap with new FileStatus records

        // wait for FrequencyMs

        // call self





    override __.ExecuteAsync(stopToken) =
        async {
            logger.LogTrace "monitoring log files"
            let dirinfo = DirectoryInfo(Path.Combine(_options.DataDir, "log"))
            if not dirinfo.Exists then
                failwithf "%s does not exist" logPath

            while not stopToken.IsCancellationRequested do

                do! Async.Sleep _options.FrequencyMs

            logger.LogTrace "cancelling..."

        }
        |> Async.StartAsTask
        :> Task