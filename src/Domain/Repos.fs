namespace Domain

open System.Threading.Tasks
open Domain.Core

module Repos =
  type QueueMessage =
    { Id: string
      PopReceipt: string
      Body: string }

  type IInputMsgClient =
    abstract Delete: unit -> Task<unit>

  type IInputStorage =
    abstract DownloadFile: string -> Task<File>
    abstract DeleteFile: string -> Task<unit>

  type IOutputStorage =
    abstract UploadFile: File -> Task<unit>

  type IOutputQueue =
    abstract SendSuccessMessage: string -> Task<unit>
    abstract SendFailureMessage: unit -> Task<unit>

  type IInputQueue =
    abstract GetMessage: unit -> Task<QueueMessage option>
    abstract GetInputMsgClient: string * string -> IInputMsgClient

  type GetOutputQueue = string * string -> IOutputQueue