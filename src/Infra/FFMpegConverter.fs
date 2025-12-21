namespace Infra

open System
open System.Diagnostics
open Domain
open Infra.Settings
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options

type FFMpegConverter(options: IOptions<FFMpegSettings>, logger: ILogger<FFMpegConverter>) =
  let settings = options.Value

  interface IConverter with
    member this.Convert(inputFile, targetExtension) = task {
      let outputFile = File.create targetExtension

      let arguments = [ $"-i {inputFile.Path}"; settings.Arguments; outputFile.Path ]

      let processStartInfo =
        ProcessStartInfo(
          RedirectStandardError = true,
          UseShellExecute = false,
          FileName = settings.Path,
          Arguments = String.Join(" ", arguments)
        )

      try
        logger.LogInformation("Starting conversion of {InputFileName} to {OutputFileName}", inputFile.FullName, outputFile.FullName)

        use pcs = Process.Start(processStartInfo)

        let! ffmpegOutput = pcs.StandardError.ReadToEndAsync()

        do! pcs.WaitForExitAsync()

        if pcs.ExitCode = 0 then
          logger.LogInformation(
            "Conversion of {InputFileName} to {OutputFileName} done! FFMpeg output: {FFMpegOutput}",
            inputFile.FullName,
            outputFile.FullName,
            ffmpegOutput
          )

          return outputFile |> Ok
        else
          logger.LogError("FFMpeg error: {FFMpegError}", ffmpegOutput)
          return ConvertError |> Error
      with e ->
        logger.LogError(e, "Error during file conversion:")
        return ConvertError |> Error
    }