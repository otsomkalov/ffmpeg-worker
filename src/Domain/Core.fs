namespace Domain

open System.Threading.Tasks

module Core =
  type File =
    { Name: string
      FullName: string
      Extension: string
      Path: string }

  [<RequireQualifiedAccess>]
  module Conversion =
    type Request = { Id: string; Name: string }

    type Run = Request -> Task<unit>
