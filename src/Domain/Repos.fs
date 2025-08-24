namespace Domain

open System.Threading.Tasks
open Domain.Core

module Repos =
  type IRemoteStorage =
    abstract DownloadFile: string -> Task<File>
    abstract UploadFile: File -> Task<unit>
    abstract DeleteFile: string -> Task<unit>

  type QueueMessage =
    { Id: string
      PopReceipt: string
      Body: string }

  type IMessageClient =
    abstract Delete: unit -> Task<unit>

  type IQueue =
    abstract GetMessage: unit -> Task<QueueMessage option>
    abstract SendSuccessMessage: string * string * string -> Task<unit>
    abstract SendFailureMessage: string * string -> Task<unit>
    abstract GetMessageClient: string * string -> IMessageClient