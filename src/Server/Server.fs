module PoQMon.Server

open System
open System.IO
open System.Threading.Tasks

open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging

open FSharp.Control.Tasks.V2
open Giraffe
open Shared


let tryGetEnv = System.Environment.GetEnvironmentVariable >> function null | "" -> None | x -> Some x

let publicPath = Path.GetFullPath "../Client/public"
let port =
    "SERVER_PORT"
    |> tryGetEnv |> Option.map uint16 |> Option.defaultValue 8085us

let buildConfiguration (args:string array) =
    (new ConfigurationBuilder())
        .AddCommandLine(args)
        .Build()

let webApp =
    route "/api/init" >=>
        fun next ctx ->
            task {
                let counter = { Value = 42 }
                return! json counter next ctx
            }

let configureApp (app : IApplicationBuilder) =
    app.UseDefaultFiles()
       .UseStaticFiles()
       .UseGiraffe webApp

let configureServices (configuration : IConfiguration) (services : IServiceCollection) =
    services.Configure<LogFileWatcherOptions>(configuration) |> ignore

    services.AddGiraffe() |> ignore
    services.AddSingleton<Giraffe.Serialization.Json.IJsonSerializer>(Thoth.Json.Giraffe.ThothSerializer()) |> ignore
    services.AddHostedService<LogFileWatcher>() |> ignore

let configureLogging (logging : ILoggingBuilder) =
    logging.SetMinimumLevel(LogLevel.Trace)
    |> ignore

[<EntryPoint>]
let main args =
    let config = buildConfiguration args
    WebHost
        .CreateDefaultBuilder(args)
        .UseConfiguration(config)
        .UseWebRoot(publicPath)
        .UseContentRoot(publicPath)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices config)
        .ConfigureLogging(configureLogging)
        .UseUrls(sprintf "http://0.0.0.0:%d/" port)
        .Build()
        .Run()
    0