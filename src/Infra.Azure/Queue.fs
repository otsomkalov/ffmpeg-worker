namespace Infra.Azure

open System
open Azure.Storage.Queues
open Domain.Repos
open Infra.Azure
open Infra.Helpers
open Infra.Queue
open Microsoft.Extensions.Logging
open otsom.fs.Extensions

module Queue =
  type internal QueueType =
    | Input
    | Output

  type InputMsgClient(queueClient: QueueClient, id: string, popReceipt: string) =
    interface IInputMsgClient with
      member this.Delete() =
        queueClient.DeleteMessageAsync(id, popReceipt) |> Task.map ignore

  type InputQueue(settings: StorageSettings, queueClient: QueueServiceClient) =
    let queue = queueClient.GetQueueClient settings.Input.Queue

    interface IInputQueue with
      member this.GetMessage() =
        queue.ReceiveMessageAsync()
        |> Task.map Option.ofObj
        |> Task.map (Option.bind (fun m -> m.Value |> Option.ofObj))
        |> Task.map (Option.filter (fun m -> not (String.IsNullOrEmpty(m.MessageText))))
        |> TaskOption.map (fun m ->
          { Id = m.MessageId
            PopReceipt = m.PopReceipt
            Body = m.MessageText })

      member this.GetInputMsgClient(id, popReceipt) =
        InputMsgClient(queue, id, popReceipt) :> IInputMsgClient

  type OutputQueue(logger: ILogger<OutputQueue>, queueClient: QueueServiceClient, settings: StorageSettings, operationId, conversionId) =
    let queue = queueClient.GetQueueClient settings.Output.Queue

    let sendOutputMessage = JSON.serialize >> queue.SendMessageAsync >> Task.map ignore

    interface IOutputQueue with
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

  let getOutputQueue logger queueClient settings : GetOutputQueue =
    fun (operationId, conversionId) -> OutputQueue(logger, queueClient, settings, operationId, conversionId)