namespace Worker

open System.Diagnostics
open System.Threading
open System.Threading.Tasks
open Domain.Core
open Domain.Repos
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
    remoteStorage: IRemoteStorage,
    convertFile: Converter.Convert,
    telemetryClient: TelemetryClient,
    queue: IQueue
  ) =
  inherit BackgroundService()

  let processQueueMessage (queueMessage: QueueMessage) =
    let inputMessage =
      JSON.deserialize<BaseMessage<Conversion.Request>> queueMessage.Body

    let data = inputMessage.Data

    let io: Conversion.RunIO =
      { Convert = convertFile
        DeleteLocalFile = LocalStorage.deleteFile }

    let msgClient = queue.GetMessageClient(queueMessage.Id, queueMessage.PopReceipt)

    let convert = Conversion.run remoteStorage queue msgClient io

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
        do! msgClient.Delete()
        do! queue.SendFailureMessage()
        operation.Telemetry.Success <- false
    }

  let runWorker () =
    queue.GetMessage()
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
