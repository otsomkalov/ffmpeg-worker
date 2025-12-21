namespace Domain

open System.IO
open System.Threading.Tasks
open shortid
open shortid.Configuration

type File =
  { Name: string
    FullName: string
    Extension: string
    Path: string }

[<RequireQualifiedAccess>]
module Conversion =
  type Request = { Id: string; Name: string }

  type Run = Request -> Task<unit>

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