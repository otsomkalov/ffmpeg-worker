namespace Infra

open System.IO
open shortid
open Domain.Core

[<RequireQualifiedAccess>]
module File =
  let create extension =
    let name = ShortId.Generate()
    let fileNameWithExtension = sprintf "%s%s" name extension
    let filePath = Path.Combine(Path.GetTempPath(), fileNameWithExtension)

    { Name = name
      FullName = fileNameWithExtension
      Extension = extension
      Path = filePath }