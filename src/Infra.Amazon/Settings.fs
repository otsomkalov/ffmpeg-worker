namespace Infra.Amazon

[<CLIMutable>]
type StorageSettings' = { Queue: string; Container: string }

[<CLIMutable>]
type StorageSettings =
  { Input: StorageSettings'
    Output: StorageSettings' }

  static member SectionName = "Storage"