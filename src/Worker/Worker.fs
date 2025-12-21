namespace Worker

open System.Diagnostics
open System.Threading
open System.Threading.Tasks
open Domain
open Domain.Repos
open Domain.Settings
open Infra
open Infra.Helpers
open Infra.Queue
open Microsoft.ApplicationInsights
open Microsoft.ApplicationInsights.DataContracts
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Worker.Settings
open Domain.Workflows
open FsToolkit.ErrorHandling

type Worker
  (
    logger: ILogger<Worker>,
    workerSettings: WorkerSettings,
    telemetryClient: TelemetryClient,
    inputQueue: IInputQueue,
    getOutputQueue: GetOutputQueue,
    inputStorage: IInputStorage,
    outputStorage: IOutputStorage,
    converter: IConverter,
    appOptions: IOptions<AppSettings>
  ) =
  inherit BackgroundService()

  let processQueueMessage (queueMessage: QueueMessage) =
    let inputMessage =
      JSON.deserialize<BaseMessage<{| Id: string; Name: string |}>> queueMessage.Body

    let request: Conversion.Request =
      { Id = inputMessage.Data.Id
        Name = inputMessage.Data.Name }

    let inputMsgClient =
      inputQueue.GetInputMsgClient(queueMessage.Id, queueMessage.PopReceipt)

    let outputQueue = getOutputQueue (inputMessage.OperationId, inputMessage.Data.Id)

    let convert =
      Conversion.run inputStorage outputStorage inputMsgClient outputQueue appOptions converter LocalStorage.deleteFile

    task {
      use activity = (new Activity("Convert")).SetParentId(inputMessage.OperationId)

      use operation = telemetryClient.StartOperation<RequestTelemetry>(activity)

      operation.Telemetry.Context.Cloud.RoleName <- workerSettings.Name

      try
        do! convert request

        operation.Telemetry.Success <- true
      with e ->
        logger.LogError(e, "Error during processing queue message")
        do! inputMsgClient.Delete()
        do! outputQueue.SendFailureMessage()
        operation.Telemetry.Success <- false
    }

  let runWorker () =
    inputQueue.GetMessage()
    |> Task.bind (function
      | Some m -> processQueueMessage m
      | None -> Task.FromResult())

  override _.ExecuteAsync(ct: CancellationToken) = task {
    while not ct.IsCancellationRequested do
      try
        do! runWorker ()
      with e ->
        logger.LogError(e, "Worker error")

      do! Task.Delay(workerSettings.Delay)
  }