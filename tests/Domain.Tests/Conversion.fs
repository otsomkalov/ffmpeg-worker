module Conversion

open System
open System.Threading.Tasks
open Domain.Core
open Domain.Repos
open Domain.Workflows
open Moq
open Xunit
open FsUnit.Xunit

type Conversion() =
  let operationId = Guid.NewGuid().ToString()

  let conversionId = "test-id"

  let request: Conversion.Request =
    { Id = conversionId
      Name = "test.webm" }

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

  let inputStorage = Mock<IInputStorage>()

  do
    inputStorage.Setup(_.DownloadFile(request.Name)).ReturnsAsync(inputFile)
    |> ignore

  do inputStorage.Setup(_.DeleteFile(request.Name)).ReturnsAsync(()) |> ignore

  let outputStorage = Mock<IOutputStorage>()

  do outputStorage.Setup(_.UploadFile(convertedFile)).ReturnsAsync(()) |> ignore

  let queue = Mock<IInputQueue>()

  let outputQueue = Mock<IOutputQueue>()

  do
    outputQueue.Setup(_.SendSuccessMessage(convertedFile.FullName)).ReturnsAsync(())
    |> ignore

  do outputQueue.Setup(_.SendFailureMessage()).ReturnsAsync(()) |> ignore

  let inputMsgClient = Mock<IInputMsgClient>()

  do inputMsgClient.Setup(_.Delete()).ReturnsAsync(()) |> ignore

  let io: Conversion.RunIO =
    { Convert =
        fun file ->
          file |> should equal inputFile

          convertedFile |> Result.Ok |> Task.FromResult
      DeleteLocalFile = fun file -> [ inputFile; convertedFile ] |> should contain file }

  [<Fact>]
  let ``run should send success message and cleanup local and remote files on success`` () =
    let sut =
      Conversion.run inputStorage.Object outputStorage.Object inputMsgClient.Object outputQueue.Object io

    task {
      do! sut request

      inputStorage.VerifyAll()
    }

  [<Fact>]
  let ``run should send failure message and cleanup downloaded files on failure`` () =
    let io =
      { io with
          Convert =
            fun file ->
              file |> should equal inputFile

              Converter.ConvertError |> Result.Error |> Task.FromResult }

    let sut =
      Conversion.run inputStorage.Object outputStorage.Object inputMsgClient.Object outputQueue.Object io

    task {
      do! sut request

      inputStorage.Verify(_.DownloadFile(request.Name))
      inputStorage.VerifyNoOtherCalls()
    }