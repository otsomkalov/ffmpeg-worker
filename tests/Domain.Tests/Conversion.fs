module Conversion

open System.Threading.Tasks
open Domain.Core
open Domain.Workflows
open Xunit
open FsUnit.Xunit

let request: Conversion.Request = { Id = "test-id"; Name = "test.webm" }

let inputFile: File =
  { Name = "input"
    FullName = "input.webm"
    Extension = "webm"
    Path = "C:/input.webm" }

let convertedFile: File =
  { Name = "output"
    FullName = "output.mp4"
    Extension = "mp4"
    Path = "C:/output.mp4" }

let io: Conversion.RunIO =
  { DownloadFile =
      fun name ->
        name |> should equal request.Name

        inputFile |> Task.FromResult
    Convert =
      fun file ->
        file |> should equal inputFile

        convertedFile |> Result.Ok |> Task.FromResult
    UploadFile =
      fun file ->
        file |> should equal convertedFile

        Task.FromResult()
    DeleteRemoteFile =
      fun file ->
        file |> should equal request.Name

        Task.FromResult()
    DeleteLocalFile = fun file -> [ inputFile; convertedFile ] |> should contain file
    SendSuccessMessage =
      fun file ->
        file |> should equal convertedFile.FullName

        Task.FromResult()
    SendFailureMessage = fun _ -> failwith "todo"
    DeleteInputMessage = fun _ -> Task.FromResult() }

[<Fact>]
let ``run should send success message and cleanup local and remote files on success`` () =
  let sut = Conversion.run io

  sut request

[<Fact>]
let ``run should send failure message and cleanup downloaded files on failure`` () =
  let io =
    { io with
        Convert =
          fun file ->
            file |> should equal inputFile

            Converter.ConvertError |> Result.Error |> Task.FromResult
        SendFailureMessage = fun _ -> Task.FromResult()
        SendSuccessMessage = fun _ -> failwith "todo"
        UploadFile = fun _ -> failwith "todo"
        DeleteRemoteFile = fun _ -> failwith "todo" }

  let sut = Conversion.run io

  sut request
