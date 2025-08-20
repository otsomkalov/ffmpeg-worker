namespace Worker

open System.Diagnostics
open System.Threading
open System.Threading.Tasks
open Azure.Storage.Queues.Models
open Domain
open Domain.Core
open FSharp
open Infrastructure
open Microsoft.ApplicationInsights
open Microsoft.ApplicationInsights.DataContracts
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Infrastructure.Helpers
open Worker.Settings
open otsom.fs.Extensions
open Domain.Workflows

type Worker
  (
    logger: ILogger<Worker>,
    appSettings: AppSettings,
    remoteStorage: Repos.IRemoteStorage,
    convertFile: Converter.Convert,
    getMessage: Queue.GetMessage,
    deleteMessageFactory: Queue.DeleteMessageFactory,
    sendSuccessMessageFactory: Queue.SendSuccessMessageFactory,
    sendFailureMessageFactory: Queue.SendFailureMessageFactory,
    telemetryClient: TelemetryClient
  ) =
  inherit BackgroundService()

  let processQueueMessage (queueMessage: QueueMessage) =
    let inputMessage =
      JSON.deserialize<Queue.BaseMessage<Conversion.Request>> queueMessage.MessageText

    let data = inputMessage.Data
    let sendSuccessMessage = sendSuccessMessageFactory inputMessage.OperationId data.Id
    let sendFailureMessage = sendFailureMessageFactory inputMessage.OperationId data.Id

    let deleteMessage =
      deleteMessageFactory (queueMessage.MessageId, queueMessage.PopReceipt)

    let io: Conversion.RunIO =
      { Convert = convertFile
        DeleteLocalFile = LocalStorage.deleteFile
        SendFailureMessage = sendFailureMessage
        SendSuccessMessage = sendSuccessMessage
        DeleteInputMessage = deleteMessage }

    let convert = Conversion.run remoteStorage io

    task {
      use activity =
        (new Activity("Convert")).SetParentId(inputMessage.OperationId)

      use operation = telemetryClient.StartOperation<RequestTelemetry>(activity)

      operation.Telemetry.Context.Cloud.RoleName <- appSettings.Name

      try
        do! convert data

        operation.Telemetry.Success <- true
      with e ->
        Logf.elogfe logger e "Error during processing queue message:"
        do! deleteMessage ()
        do! sendFailureMessage ()
        operation.Telemetry.Success <- false
    }

  let runWorker () =
    getMessage ()
    |> Task.bind (function
      | Some m -> processQueueMessage m
      | None -> Task.FromResult())

  override _.ExecuteAsync(ct: CancellationToken) =
    task {
      while not ct.IsCancellationRequested do
        try
          do! runWorker ()
        with e ->
          Logf.elogfe logger e "Worker error:"

        do! Task.Delay(appSettings.Delay)
    }
