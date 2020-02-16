namespace Shared
open System

type Counter = { Value : int }


type LogfileUpdate =
    { timestamp: DateTimeOffset
      path: string
      data: byte[] }