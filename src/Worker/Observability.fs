[<RequireQualifiedAccess>]
module Worker.Observability

open System.Diagnostics

let ActivitySource = new ActivitySource("Worker")