namespace Worker

open System

module Settings =
  [<CLIMutable>]
  type WorkerSettings = { Name: string; Delay: TimeSpan }