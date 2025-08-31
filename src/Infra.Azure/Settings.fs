namespace Infra.Azure

[<CLIMutable>]
type StorageSettings' = { Queue: string; Container: string }

[<CLIMutable>]
type StorageSettings =
  { ConnectionString: string
    Input: StorageSettings'
    Output: StorageSettings' }

  static member SectionName = "Storage"