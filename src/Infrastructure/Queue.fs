namespace Infrastructure

open System
open System.Text.Json.Serialization
open System.Threading.Tasks
open Azure.Storage.Queues
open Azure.Storage.Queues.Models
open FSharp
open Infrastructure.Helpers
open Microsoft.Extensions.Logging
open otsom.fs.Extensions
open Domain.Workflows

[<RequireQualifiedAccess>]
module Queue =
  type QueueType =
    | Input
    | Output

  let getQueueClient (settings: Settings.StorageSettings) =
    let queueServiceClient = QueueServiceClient(settings.ConnectionString)

    function
    | Input -> settings.Input.Container
    | Output -> settings.Output.Container
    >> queueServiceClient.GetQueueClient

  type GetMessage = unit -> Task<QueueMessage option>

  let getMessage settings : GetMessage =
    let getQueueClient = getQueueClient settings
    let inputQueueClient = getQueueClient Input

    inputQueueClient.ReceiveMessageAsync
    >> Task.map Option.ofObj
    >> Task.map (Option.bind (fun m -> m.Value |> Option.ofObj))
    >> Task.map (Option.filter (fun m -> not (String.IsNullOrEmpty(m.MessageText))))

  type DeleteMessageFactory = string * string -> Queue.DeleteMessage

  let deleteMessageFactory settings (loggerFactory: ILoggerFactory) : DeleteMessageFactory =
    let logger = loggerFactory.CreateLogger(nameof Queue.DeleteMessage)
    let getQueueClient = getQueueClient settings
    let inputQueueClient = getQueueClient Input

    fun (id, popReceipt) ->
      fun () ->
        Logf.logfi logger "Deleting message from queue"

        inputQueueClient.DeleteMessageAsync(id, popReceipt) |> Task.map ignore

  type SuccessfulConversion = { Name: string }

  type ConversionError = { Error: string }

  type ConversionResult =
    | Success of SuccessfulConversion
    | Error of ConversionError

  type ConversionResultMessage =
    { Id: string; Result: ConversionResult }

  let private sendOutputMessage settings =
    let getQueueClient = getQueueClient settings
    let outputQueueClient = getQueueClient Output

    JSON.serialize >> outputQueueClient.SendMessageAsync >> Task.map ignore

  [<JsonFSharpConverter>]
  type OutputMessage = { Id: string; Name: string }

  type SendSuccessMessageFactory = string -> Queue.SendSuccessMessage

  let sendSuccessMessageFactory settings (loggerFactory: ILoggerFactory) : SendSuccessMessageFactory =
    let logger = loggerFactory.CreateLogger(nameof Queue.SendSuccessMessage)

    fun id ->
      fun name ->
        let message: ConversionResultMessage =
          { Id = id
            Result = ConversionResult.Success {Name = name } }

        Logf.logfi logger "Sending successful conversion result message"

        sendOutputMessage settings message

  type SendFailureMessageFactory = string -> Queue.SendFailureMessage

  let sendFailureMessageFactory settings (loggerFactory: ILoggerFactory) : SendFailureMessageFactory =
    let logger = loggerFactory.CreateLogger(nameof Queue.SendFailureMessage)

    fun id ->
      fun () ->
        let message: ConversionResultMessage =
          { Id = id
            Result = ConversionResult.Error {Error = "Error during conversion!" } }

        Logf.logfi logger "Sending conversion result error message"

        sendOutputMessage settings message
