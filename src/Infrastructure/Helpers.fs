namespace Infrastructure.Helpers

open System.Text.Json
open System.Text.Json.Serialization
open System.Threading.Tasks
open otsom.fs.Extensions

module Option =
  let taskMap mapping =
    function
    | Some v -> mapping v |> Task.map Some
    | None -> None |> Task.FromResult

module JSON =
  let private options =
    JsonFSharpOptions.Default().WithUnionUntagged().ToJsonSerializerOptions()

  let serialize value =
    JsonSerializer.Serialize(value, options)

  let deserialize<'a> (str: string) =
    JsonSerializer.Deserialize<'a>(str, options)