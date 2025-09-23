namespace Infra

open System.IO
open shortid
open Domain.Core
open shortid.Configuration

[<RequireQualifiedAccess>]
module File =
  let create =
    let idGenerationOptions =
      GenerationOptions(useSpecialCharacters = false, length = 12)

    fun extension ->
      let name = ShortId.Generate(idGenerationOptions)
      let fileNameWithExtension = sprintf "%s%s" name extension
      let filePath = Path.Combine(Path.GetTempPath(), fileNameWithExtension)

      { Name = name
        FullName = fileNameWithExtension
        Extension = extension
        Path = filePath }