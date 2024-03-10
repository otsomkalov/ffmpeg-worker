namespace Worker

open System.Threading
open System.Threading.Tasks
open Azure.Storage.Queues.Models
open Domain.Core
open FSharp
open Infrastructure
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
    downloadFile: RemoteStorage.DownloadFile,
    uploadFile: RemoteStorage.UploadFile,
    deleteRemoteFile: RemoteStorage.DeleteFile,
    convertFile: Converter.Convert,
    getMessage: Queue.GetMessage,
    deleteMessageFactory: Queue.DeleteMessageFactory,
    sendSuccessMessageFactory: Queue.SendSuccessMessageFactory,
    sendFailureMessageFactory: Queue.SendFailureMessageFactory
  ) =
  inherit BackgroundService()

  let processQueueMessage (queueMessage: QueueMessage) =
    let inputMessage = JSON.deserialize<Conversion.Request> queueMessage.MessageText
    let sendSuccessMessage = sendSuccessMessageFactory  inputMessage.Id
    let sendFailureMessage = sendFailureMessageFactory inputMessage.Id
    let deleteMessage = deleteMessageFactory (queueMessage.MessageId, queueMessage.PopReceipt)

    let io: Conversion.RunIO =
      { DownloadFile = downloadFile
        Convert = convertFile
        UploadFile = uploadFile
        DeleteLocalFile = LocalStorage.deleteFile
        DeleteRemoteFile = deleteRemoteFile
        SendFailureMessage = sendFailureMessage
        SendSuccessMessage = sendSuccessMessage
        DeleteInputMessage = deleteMessage }

    let convert = Conversion.run io

    task {
      try
        do! convert inputMessage
      with e ->
        Logf.elogfe logger e "Error during processing queue message:"
        do! deleteMessage ()
        do! sendFailureMessage ()
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
