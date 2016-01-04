#I @"packages/FAKE/tools"
#r "FakeLib.dll"

open Fake
open Fake.FileHelper
open Fake.ProcessHelper
open Fake.ZipHelper

// ------------------------------------------------------------------------------ Project

let buildSolutionFile = "./IncrementalCompiler.sln"
let buildConfiguration = "Release"
  
// ---------------------------------------------------------------------------- Variables

let binDir = "bin"
let testDir = binDir @@ "test"

// ------------------------------------------------------------------------------ Targets

Target "Clean" (fun _ -> 
    CleanDirs [binDir]
)

Target "Build" (fun _ ->
    !! buildSolutionFile
    |> MSBuild "" "Rebuild" [ "Configuration", buildConfiguration ]
    |> Log "Build-Output: "
)

Target "Package" (fun _ ->
    // pack IncrementalCompiler.exe with dependent module dlls to packed one
    let errorCode = Shell.Exec("./packages/ILRepack/tools/ILRepack.exe",
                               "/wildcards /out:IncrementalCompiler.packed.exe IncrementalCompiler.exe *.dll",
                               "./core/IncrementalCompiler/bin/Release")
    // let's make package
    for target in ["Unity4"; "Unity5"] do
        let targetDir = binDir @@ target
        let editorDir = targetDir @@ "Assets" @@ "Editor"
        let compilerDir = targetDir @@ "Compiler"
        // create dirs
        CreateDir targetDir
        CreateDir editorDir
        CreateDir compilerDir
        // copy output files
        "./core/IncrementalCompiler/bin/Release/IncrementalCompiler.packed.exe" |> CopyFile (compilerDir @@ "IncrementalCompiler.exe")
        "./extra/CompilerPlugin." + target + "/bin/Release/Unity.PureCSharpTests.dll" |> CopyFile (editorDir @@ "CompilerPlugin.dll")
        "./extra/UniversalCompiler/bin/Release/UniversalCompiler.exe" |> CopyFile compilerDir
        "./tools/pdb2mdb/pdb2mdb.exe" |> CopyFile compilerDir
        // create zipped packages
        !! (targetDir @@ "**") |> Zip targetDir (binDir @@ "IncrementalCompiler." + target + ".zip")
)

Target "Help" (fun _ ->  
    List.iter printfn [
      "usage:"
      "build [target]"
      ""
      " Targets for building:"
      " * Build        Build"
      " * Package      Make packages"
      ""]
)

// --------------------------------------------------------------------------- Dependency

// Build order
"Clean"
  ==> "Build"
  ==> "Package"

RunTargetOrDefault "Help"
