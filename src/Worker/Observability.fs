[<RequireQualifiedAccess>]
module Worker.Observability

open System.Diagnostics
open OpenTelemetry

let ActivitySource = new ActivitySource("Worker")

type AzureStorageQueueReceiveMessageTracesProcessor() =
  inherit BaseProcessor<Activity>()

  override this.OnEnd(activity: Activity) =
    if activity.OperationName <> "QueueClient.ReceiveMessage" then
      base.OnEnd activity