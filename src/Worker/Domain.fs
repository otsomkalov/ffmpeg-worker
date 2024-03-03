namespace Worker.Domain

open System.IO
open shortid

type File =
  { Name: string
    Extension: string
    Path: string }

[<RequireQualifiedAccess>]
module File =
  let create extension =
    let name = ShortId.Generate()
    let fileNameWithExtension = sprintf "%s%s" name extension
    let filePath = Path.Combine(Path.GetTempPath(), fileNameWithExtension)

    { Name = name
      Extension = extension
      Path = filePath }
