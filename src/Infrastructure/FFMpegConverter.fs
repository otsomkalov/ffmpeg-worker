namespace Infrastructure

open System
open System.Diagnostics
open System.Threading.Tasks
open FSharp
open Infrastructure.Core
open Infrastructure.Settings
open Microsoft.Extensions.Logging
open Domain.Workflows

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
          Logf.logfi logger "Starting conversion of %s{InputFileName} to %s{OutputFileName}" file.FullName outputFile.FullName

          use pcs = Process.Start(processStartInfo)

          let! ffmpegOutput = pcs.StandardError.ReadToEndAsync()

          do! pcs.WaitForExitAsync()

          return
            if pcs.ExitCode = 0 then
              Logf.logfi
                logger
                "Conversion of %s{InputFileName} to %s{OutputFileName} done! FFMpeg output: %s{FFMpegOutput}"
                file.FullName
                outputFile.FullName
                ffmpegOutput

              outputFile |> Ok
            else
              Logf.logfe logger "FFMpeg error: %s{FFMpegError}" ffmpegOutput
              Converter.ConvertError |> Error
        }
      with e ->
        Logf.elogfe logger e "Error during file conversion:"
        Converter.ConvertError |> Error |> Task.FromResult
