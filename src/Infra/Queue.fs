namespace Infra.Queue

open System.Collections.Generic
open System.Text.Json.Serialization

type BaseMessage<'a> = { Context: Dictionary<string, string>; Data: 'a }

type SuccessfulConversion = { Name: string }

type ConversionError = { Error: string }

type ConversionResult =
  | Success of SuccessfulConversion
  | Error of ConversionError

type ConversionResultMessage =
  { Id: string; Result: ConversionResult }

[<JsonFSharpConverter>]
type OutputMessage = { Id: string; Name: string }