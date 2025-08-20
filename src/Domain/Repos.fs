namespace Domain

open System.Threading.Tasks
open Domain.Core

module Repos =
  type IRemoteStorage =
    abstract DownloadFile: string -> Task<File>
    abstract UploadFile: File -> Task<unit>
    abstract DeleteFile: string -> Task<unit>
