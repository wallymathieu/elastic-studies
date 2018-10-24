// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------


#r "paket: groupref FakeBuild //"

#load "./.fake/build.fsx/intellisense.fsx"

open System.IO
open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.DotNet.Testing
open Fake.Tools
open Fake.Api
open Fake.DotNet

// File system information 
let solutionFile  = "elastic-studies.sln"

// Pattern specifying assemblies to be tested using NUnit
let testProjects = ["./tests/Tests"]

let configuration = "Release"

// --------------------------------------------------------------------------------------
// END TODO: The rest of the file includes standard build steps
// --------------------------------------------------------------------------------------

Target.create "clean" (fun _ ->
    Shell.cleanDirs ["bin"; "temp"]
)


Target.create "restore" (fun _ ->
    solutionFile
    |> DotNet.restore id
)

Target.create "build" (fun _ ->
    let buildMode = Environment.environVarOrDefault "buildMode" configuration
    let setParams (defaults:MSBuildParams) =
        { defaults with
            Verbosity = Some(Quiet)
            Targets = ["Build"]
            Properties =
                [
                    "Optimize", "True"
                    "DebugSymbols", "True"
                    "Configuration", buildMode
                ]
         }
    MSBuild.build setParams solutionFile
)

Target.create "test_only" (fun _ ->
    testProjects
    |> Seq.iter (fun proj -> DotNet.test (fun p ->
        { p with ResultsDirectory = Some __SOURCE_DIRECTORY__ }) proj)
)

Target.create "test" ignore


// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target.create "all" ignore

"test_only" ==> "test"

"build" ?=> "test_only"

"clean"
  ==> "restore"
  ==> "build"
  ==> "test"
  ==> "all"

Target.runOrDefault "test"
