namespace Infrastructure

open System
open System.Text.Json.Serialization
open Azure.Storage.Queues
open Domain.Repos
open Infrastructure.Helpers
open Microsoft.Extensions.Logging
open otsom.fs.Extensions

type internal QueueType =
  | Input
  | Output

type BaseMessage<'a> = { OperationId: string; Data: 'a }

type internal SuccessfulConversion = { Name: string }

type internal ConversionError = { Error: string }

type internal ConversionResult =
  | Success of SuccessfulConversion
  | Error of ConversionError

type internal ConversionResultMessage =
  { Id: string; Result: ConversionResult }

[<JsonFSharpConverter>]
type internal OutputMessage = { Id: string; Name: string }

type MsgClient(queueClient: QueueClient, id: string, popReceipt: string) =
  interface IMessageClient with
    member this.Delete() =
      queueClient.DeleteMessageAsync(id, popReceipt) |> Task.map ignore

type AzureStorageQueue(logger: ILogger<AzureStorageQueue>, operationId: string, conversionId: string, settings: Settings.StorageSettings) =
  let getQueueClient =
    let queueServiceClient = QueueServiceClient(settings.ConnectionString)

    function
    | Input -> settings.Input.Container
    | Output -> settings.Output.Container
    >> queueServiceClient.GetQueueClient

  let sendOutputMessage =
    let outputQueueClient = getQueueClient Output

    JSON.serialize >> outputQueueClient.SendMessageAsync >> Task.map ignore

  interface IQueue with
    member this.GetMessage() =
      let inputQueueClient = getQueueClient Input

      inputQueueClient.ReceiveMessageAsync()
      |> Task.map Option.ofObj
      |> Task.map (Option.bind (fun m -> m.Value |> Option.ofObj))
      |> Task.map (Option.filter (fun m -> not (String.IsNullOrEmpty(m.MessageText))))
      |> TaskOption.map (fun m ->
        { Id = m.MessageId
          PopReceipt = m.PopReceipt
          Body = m.MessageText })

    member this.SendSuccessMessage(name: string) =
      let message: BaseMessage<ConversionResultMessage> =
        { OperationId = operationId
          Data =
            { Id = conversionId
              Result = ConversionResult.Success { Name = name } } }

      logger.LogInformation "Sending successful conversion result message"

      sendOutputMessage message

    member this.SendFailureMessage() =
      let message: BaseMessage<ConversionResultMessage> =
        { OperationId = operationId
          Data =
            { Id = conversionId
              Result = ConversionResult.Error { Error = "Error during conversion!" } } }

      logger.LogInformation "Sending conversion result error message"

      sendOutputMessage message

    member this.GetMessageClient(id, popReceipt) =
      let inputQueueClient = getQueueClient Input

      MsgClient(inputQueueClient, id, popReceipt) :> IMessageClient