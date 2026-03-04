namespace Worker

open System

module Settings =
  [<CLIMutable>]
  type WorkerSettings = { Delay: TimeSpan }