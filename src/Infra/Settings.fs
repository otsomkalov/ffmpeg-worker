namespace Infra.Settings

[<CLIMutable>]
type FFMpegSettings =
  { Path: string
    Arguments: string
    TargetExtension: string }

  static member SectionName = "FFMpeg"