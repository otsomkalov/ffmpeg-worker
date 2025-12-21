namespace Infra.Settings

[<CLIMutable>]
type FFMpegSettings =
  { Path: string
    Arguments: string }

  static member SectionName = "FFMpeg"