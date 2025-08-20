module Conversion

open System.Threading.Tasks
open Domain.Core
open Domain.Repos
open Domain.Workflows
open Moq
open Xunit
open FsUnit.Xunit

type Conversion() =
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

  let remoteStorage = Mock<IRemoteStorage>()

  do
    remoteStorage.Setup(_.DownloadFile(request.Name)).ReturnsAsync(inputFile)
    |> ignore

  do remoteStorage.Setup(_.UploadFile(convertedFile)).ReturnsAsync(()) |> ignore
  do remoteStorage.Setup(_.DeleteFile(request.Name)).ReturnsAsync(()) |> ignore

  let io: Conversion.RunIO =
    { Convert =
        fun file ->
          file |> should equal inputFile

          convertedFile |> Result.Ok |> Task.FromResult
      DeleteLocalFile = fun file -> [ inputFile; convertedFile ] |> should contain file
      SendSuccessMessage =
        fun file ->
          file |> should equal convertedFile.FullName

          Task.FromResult()
      SendFailureMessage = fun _ -> failwith "todo"
      DeleteInputMessage = fun _ -> Task.FromResult() }

  [<Fact>]
  let ``run should send success message and cleanup local and remote files on success`` () =
    let sut = Conversion.run remoteStorage.Object io

    task {
      do! sut request

      remoteStorage.VerifyAll()
    }

  [<Fact>]
  let ``run should send failure message and cleanup downloaded files on failure`` () =
    let io =
      { io with
          Convert =
            fun file ->
              file |> should equal inputFile

              Converter.ConvertError |> Result.Error |> Task.FromResult
          SendFailureMessage = fun _ -> Task.FromResult()
          SendSuccessMessage = fun _ -> failwith "todo" }

    let sut = Conversion.run remoteStorage.Object io

    task {
      do! sut request

      remoteStorage.Verify(_.DownloadFile(request.Name))
      remoteStorage.VerifyNoOtherCalls()
    }