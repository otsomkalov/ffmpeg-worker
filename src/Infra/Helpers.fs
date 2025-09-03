namespace Infra.Helpers

open System.Text.Json
open System.Text.Json.Serialization

module JSON =
  let private options =
    JsonFSharpOptions.Default().WithUnionExternalTag().WithUnionUnwrapRecordCases().ToJsonSerializerOptions()

  let serialize value =
    JsonSerializer.Serialize(value, options)

  let deserialize<'a> (str: string) =
    JsonSerializer.Deserialize<'a>(str, options)