namespace Worker

open System

module Settings =
  [<CLIMutable>]
  type AppSettings = { Delay: TimeSpan }
