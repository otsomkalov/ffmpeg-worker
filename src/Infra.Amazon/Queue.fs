namespace Infra.Amazon

open Amazon.SQS
open Amazon.SQS.Model
open Domain.Repos
open Infra.Helpers
open Infra.Queue
open Microsoft.Extensions.Logging
open otsom.fs.Extensions

module Queue =
  type MsgClient(sqsClient: IAmazonSQS, settings: StorageSettings, popReceipt: string) =
    interface IInputMsgClient with
      member this.Delete() =
        sqsClient.DeleteMessageAsync(settings.Input.Queue, popReceipt) |> Task.ignore

  type InputQueue(sqsClient: IAmazonSQS, settings: StorageSettings, logger: ILogger<InputQueue>) =
    interface IInputQueue with
      member this.GetMessage() = task {
        let request = ReceiveMessageRequest(settings.Input.Queue, MaxNumberOfMessages = 1)

        let! response = sqsClient.ReceiveMessageAsync request

        return
          response.Messages
          |> Option.ofObj
          |> Option.bind (
            Seq.tryHead
            >> Option.map (fun m ->
              { Id = m.MessageId
                PopReceipt = m.ReceiptHandle
                Body = m.Body })
          )
      }

      member this.GetInputMsgClient(_, popReceipt) =
        MsgClient(sqsClient, settings, popReceipt) :> IInputMsgClient

  type OutputQueue(sqsClient: IAmazonSQS, settings: StorageSettings, logger: ILogger<OutputQueue>, operationId, conversionId) =
    interface IOutputQueue with
      member this.SendSuccessMessage(name) = task {
        let message: BaseMessage<ConversionResultMessage> =
          { OperationId = operationId
            Data =
              { Id = conversionId
                Result = ConversionResult.Success { Name = name } } }

        logger.LogInformation "Sending successful conversion result message"

        let json = JSON.serialize message

        do! sqsClient.SendMessageAsync(settings.Output.Queue, json) |> Task.ignore
      }

      member this.SendFailureMessage() = task {
        let message: BaseMessage<ConversionResultMessage> =
          { OperationId = operationId
            Data =
              { Id = conversionId
                Result = ConversionResult.Error { Error = "Error during conversion!" } } }

        logger.LogInformation "Sending conversion result error message"

        let json = JSON.serialize message

        do! sqsClient.SendMessageAsync(settings.Output.Queue, json) |> Task.ignore
      }

  let getOutputQueue sqsClient logger settings : GetOutputQueue =
    fun (operationId, conversionId) -> OutputQueue(sqsClient, settings, logger, operationId, conversionId)