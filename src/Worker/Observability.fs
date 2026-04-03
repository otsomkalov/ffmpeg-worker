[<RequireQualifiedAccess>]
module Worker.Observability

open System.Diagnostics
open OpenTelemetry.Trace

let ActivitySource = new ActivitySource("Worker")

type DropByNameSampler(blockedNames: string Set) =
  inherit Sampler()

  override this.ShouldSample(samplingContext) =
    if blockedNames |> Set.contains samplingContext.Name then
      SamplingResult SamplingDecision.Drop
    else
      SamplingResult SamplingDecision.RecordAndSample