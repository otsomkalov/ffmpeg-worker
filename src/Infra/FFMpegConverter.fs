namespace Infra

open System
open System.Diagnostics
open System.Threading.Tasks
open Domain.Workflows
open Infra.Settings
open Microsoft.Extensions.Logging

module FFMpegConverter =
  let convert (settings: FFMpegSettings) (loggerFactory: ILoggerFactory) : Converter.Convert =
    let logger = loggerFactory.CreateLogger(nameof Converter.Convert)

    fun file ->
      let targetExtension =
        settings.TargetExtension
        |> Option.ofObj
        |> Option.filter (String.IsNullOrEmpty >> not)
        |> Option.defaultValue file.Extension

      let outputFile = File.create targetExtension

      let arguments = [ $"-i {file.Path}"; settings.Arguments; outputFile.Path ]

      let processStartInfo =
        ProcessStartInfo(
          RedirectStandardError = true,
          UseShellExecute = false,
          FileName = settings.Path,
          Arguments = String.Join(" ", arguments)
        )

      try
        task {
          logger.LogInformation("Starting conversion of {InputFileName} to {OutputFileName}", file.FullName, outputFile.FullName)

          use pcs = Process.Start(processStartInfo)

          let! ffmpegOutput = pcs.StandardError.ReadToEndAsync()

          do! pcs.WaitForExitAsync()

          return
            if pcs.ExitCode = 0 then
              logger.LogInformation(
                "Conversion of {InputFileName} to {OutputFileName} done! FFMpeg output: {FFMpegOutput}",
                file.FullName,
                outputFile.FullName,
                ffmpegOutput
              )

              outputFile |> Ok
            else
              logger.LogError("FFMpeg error: {FFMpegError}", ffmpegOutput)
              Converter.ConvertError |> Error
        }
      with e ->
        logger.LogError(e, "Error during file conversion:")
        Converter.ConvertError |> Error |> Task.FromResult