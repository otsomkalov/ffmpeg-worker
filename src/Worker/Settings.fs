namespace Worker

open System

module Settings =
  [<CLIMutable>]
  type AppSettings = { Name: string; Delay: TimeSpan }
