module Conversion

open Domain
open Domain.Repos
open Domain.Settings
open Domain.Workflows
open Microsoft.Extensions.Options
open Moq
open Xunit

type Conversion() =
  let conversionId = "test-id"

  let targetExtension = ".mp4"

  let request: Conversion.Request =
    { Id = conversionId
      Name = "input.webm" }

  let inputFile: File =
    { Name = "input"
      FullName = "input.webm"
      Extension = ".webm"
      Path = "C:/input.webm" }

  let convertedFile: File =
    { Name = "output"
      FullName = "output.mp4"
      Extension = ".mp4"
      Path = "C:/output.mp4" }

  let inputStorage = Mock<IInputStorage>()

  do
    inputStorage.Setup(_.DownloadFile(request.Name)).ReturnsAsync(inputFile)
    |> ignore

  do inputStorage.Setup(_.DeleteFile(request.Name)).ReturnsAsync(()) |> ignore

  let outputStorage = Mock<IOutputStorage>()

  do outputStorage.Setup(_.UploadFile(convertedFile)).ReturnsAsync(()) |> ignore

  let outputQueue = Mock<IOutputQueue>()

  do
    outputQueue.Setup(_.SendSuccessMessage(convertedFile.FullName)).ReturnsAsync(())
    |> ignore

  do outputQueue.Setup(_.SendFailureMessage()).ReturnsAsync(()) |> ignore

  let inputMsgClient = Mock<IInputMsgClient>()

  do inputMsgClient.Setup(_.Delete()).ReturnsAsync(()) |> ignore

  let converter = Mock<IConverter>()

  let options = Mock<IOptions<AppSettings>>()

  let deleteLocalFile = fun _ -> ()

  [<Fact>]
  let ``run converts to target extension from the settings`` () =
    options.Setup(_.Value).Returns({ TargetExtension = targetExtension }) |> ignore

    converter.Setup(_.Convert(inputFile, targetExtension)).ReturnsAsync(Ok(convertedFile))

    let sut =
      Conversion.run
        inputStorage.Object
        outputStorage.Object
        inputMsgClient.Object
        outputQueue.Object
        options.Object
        converter.Object
        deleteLocalFile

    task {
      do! sut request

      inputStorage.VerifyAll()
      inputMsgClient.VerifyAll()
      converter.VerifyAll()
      outputStorage.VerifyAll()
    }

  [<Fact>]
  let ``run converts to target extension from input file`` () =
    options.Setup(_.Value).Returns({ TargetExtension = null }) |> ignore

    converter.Setup(_.Convert(inputFile, inputFile.Extension)).ReturnsAsync(Ok(convertedFile))

    let sut =
      Conversion.run
        inputStorage.Object
        outputStorage.Object
        inputMsgClient.Object
        outputQueue.Object
        options.Object
        converter.Object
        deleteLocalFile

    task {
      do! sut request

      inputStorage.VerifyAll()
      inputMsgClient.VerifyAll()
      converter.VerifyAll()
      outputStorage.VerifyAll()
    }

  [<Fact>]
  let ``run should send failure message and cleanup downloaded files on failure`` () =
    options.Setup(_.Value).Returns({ TargetExtension = targetExtension }) |> ignore
    converter.Setup(_.Convert(inputFile, targetExtension)).ReturnsAsync(Result.Error ConvertError)

    let sut =
      Conversion.run
        inputStorage.Object
        outputStorage.Object
        inputMsgClient.Object
        outputQueue.Object
        options.Object
        converter.Object
        deleteLocalFile

    task {
      do! sut request

      inputStorage.Verify(_.DownloadFile(request.Name))
      inputMsgClient.VerifyAll()
      converter.VerifyAll()
      outputStorage.VerifyNoOtherCalls()
      outputQueue.Verify(_.SendSuccessMessage(It.IsAny<string>()), Times.Never)
      outputQueue.Verify(_.SendFailureMessage())
    }