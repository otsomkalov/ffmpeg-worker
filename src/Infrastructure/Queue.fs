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

  type BaseMessage<'a> = { OperationId: string; Data: 'a }

  let getQueueClient (queueServiceClient: QueueServiceClient) (settings: Settings.StorageSettings) =
    function
    | Input -> settings.Input.Container
    | Output -> settings.Output.Container
    >> queueServiceClient.GetQueueClient

  type GetMessage = unit -> Task<QueueMessage option>

  let getMessage client settings : GetMessage =
    let inputQueueClient = getQueueClient client settings Input

    inputQueueClient.ReceiveMessageAsync
    >> Task.map Option.ofObj
    >> Task.map (Option.bind (fun m -> m.Value |> Option.ofObj))
    >> Task.map (Option.filter (fun m -> not (String.IsNullOrEmpty(m.MessageText))))

  type DeleteMessageFactory = string * string -> Queue.DeleteMessage

  let deleteMessageFactory client settings (loggerFactory: ILoggerFactory) : DeleteMessageFactory =
    let logger = loggerFactory.CreateLogger(nameof Queue.DeleteMessage)
    let inputQueueClient = getQueueClient client settings Input

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

  let private sendOutputMessage client settings =
    let outputQueueClient = getQueueClient client settings Output

    JSON.serialize >> outputQueueClient.SendMessageAsync >> Task.map ignore

  [<JsonFSharpConverter>]
  type OutputMessage = { Id: string; Name: string }

  type SendSuccessMessageFactory = string -> string -> Queue.SendSuccessMessage

  let sendSuccessMessageFactory client settings (loggerFactory: ILoggerFactory) : SendSuccessMessageFactory =
    let logger = loggerFactory.CreateLogger(nameof Queue.SendSuccessMessage)

    fun operationId conversionId ->
      fun name ->
        let message: BaseMessage<ConversionResultMessage> =
          { OperationId = operationId
            Data =
              { Id = conversionId
                Result = ConversionResult.Success { Name = name } } }

        Logf.logfi logger "Sending successful conversion result message"

        sendOutputMessage client settings message

  type SendFailureMessageFactory = string -> string -> Queue.SendFailureMessage

  let sendFailureMessageFactory client settings (loggerFactory: ILoggerFactory) : SendFailureMessageFactory =
    let logger = loggerFactory.CreateLogger(nameof Queue.SendFailureMessage)

    fun operationId conversionId ->
      fun () ->
        let message: BaseMessage<ConversionResultMessage> =
          { OperationId = operationId
            Data =
              { Id = conversionId
                Result = ConversionResult.Error { Error = "Error during conversion!" } } }

        Logf.logfi logger "Sending conversion result error message"

        sendOutputMessage client settings message
