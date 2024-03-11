namespace Infrastructure

open System.IO
open Domain.Workflows

module LocalStorage =
  let deleteFile: LocalStorage.DeleteFile = fun file -> File.Delete(file.Path)
