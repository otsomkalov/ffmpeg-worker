namespace Infrastructure.Settings

[<CLIMutable>]
type StorageSettings' = { Queue: string; Container: string }

[<CLIMutable>]
type StorageSettings =
  { ConnectionString: string
    Input: StorageSettings'
    Output: StorageSettings' }

  static member SectionName = "Storage"

[<CLIMutable>]
type FFMpegSettings =
  { Path: string
    Arguments: string
    TargetExtension: string }

  static member SectionName = "FFMpeg"