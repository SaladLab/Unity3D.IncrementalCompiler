using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Mono.CompilerServices.SymbolWriter;
using NLog;

namespace IncrementalCompiler
{
    public class Compiler
    {
        private Logger _logger = LogManager.GetLogger("Compiler");
        private CSharpCompilation _compilation;
        private CompileOptions _options;
        private FileTimeList _referenceFileList;
        private FileTimeList _sourceFileList;
        private Dictionary<string, MetadataReference> _referenceMap;
        private Dictionary<string, SyntaxTree> _sourceMap;
        private MemoryStream _outputDllStream;
        private MemoryStream _outputDebugSymbolStream;

        public CompileResult Build(CompileOptions options)
        {
            if (_compilation == null ||
                _options.WorkDirectory != options.WorkDirectory ||
                _options.AssemblyName != options.AssemblyName ||
                _options.Output != options.Output ||
                _options.NoWarnings.SequenceEqual(options.NoWarnings) == false ||
                _options.Defines.SequenceEqual(options.Defines) == false)
            {
                return BuildFull(options);
            }
            else
            {
                return BuildIncremental(options);
            }
        }

        private CompileResult BuildFull(CompileOptions options)
        {
            var result = new CompileResult();

            _logger.Info("BuildFull");
            _options = options;

            _referenceFileList = new FileTimeList();
            _referenceFileList.Update(options.References);

            _sourceFileList = new FileTimeList();
            _sourceFileList.Update(options.Files);

            _referenceMap = options.References.ToDictionary(
               file => file,
               file => CreateReference(file));

            var parseOption = new CSharpParseOptions(LanguageVersion.CSharp6, DocumentationMode.Parse, SourceCodeKind.Regular, options.Defines);
            _sourceMap = options.Files.ToDictionary(
                file => file,
                file => ParseSource(file, parseOption));

            var specificDiagnosticOptions = options.NoWarnings.ToDictionary(x => x, _ => ReportDiagnostic.Suppress);
            _compilation = CSharpCompilation.Create(
                options.AssemblyName,
                _sourceMap.Values,
                _referenceMap.Values,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithSpecificDiagnosticOptions(specificDiagnosticOptions)
                    .WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default)
                    .WithAllowUnsafe(options.Options.Contains("-unsafe")));

            Emit(result);

            return result;
        }

        private CompileResult BuildIncremental(CompileOptions options)
        {
            var result = new CompileResult();

            _logger.Info("BuildIncremental");
            _options = options;

            // update reference files

            var referenceChanges = _referenceFileList.Update(options.References);
            foreach (var file in referenceChanges.Added)
            {
                _logger.Info("+ {0}", file);
                var reference = CreateReference(file);
                _compilation = _compilation.AddReferences(reference);
                _referenceMap.Add(file, reference);
            }
            foreach (var file in referenceChanges.Changed)
            {
                _logger.Info("* {0}", file);
                var reference = CreateReference(file);
                _compilation = _compilation.RemoveReferences(_referenceMap[file])
                                           .AddReferences(reference);
                _referenceMap[file] = reference;
            }
            foreach (var file in referenceChanges.Removed)
            {
                _logger.Info("- {0}", file);
                _compilation = _compilation.RemoveReferences(_referenceMap[file]);
                _referenceMap.Remove(file);
            }

            // update source files

            var sourceChanges = _sourceFileList.Update(options.Files);
            var parseOption = new CSharpParseOptions(LanguageVersion.CSharp6, DocumentationMode.Parse, SourceCodeKind.Regular, options.Defines);
            foreach (var file in sourceChanges.Added)
            {
                _logger.Info("+ {0}", file);
                var syntaxTree = ParseSource(file, parseOption);
                _compilation = _compilation.AddSyntaxTrees(syntaxTree);
                _sourceMap.Add(file, syntaxTree);
            }
            foreach (var file in sourceChanges.Changed)
            {
                _logger.Info("* {0}", file);
                var syntaxTree = ParseSource(file, parseOption);
                _compilation = _compilation.RemoveSyntaxTrees(_sourceMap[file])
                                           .AddSyntaxTrees(syntaxTree);
                _sourceMap[file] = syntaxTree;
            }
            foreach (var file in sourceChanges.Removed)
            {
                _logger.Info("- {0}", file);
                _compilation = _compilation.RemoveSyntaxTrees(_sourceMap[file]);
                _sourceMap.Remove(file);
            }

            // emit or reuse prebuilt output

            var reusePrebuilt = _outputDllStream != null && (
                (_options.PrebuiltOutputReuse == PrebuiltOutputReuseType.WhenNoChange &&
                 sourceChanges.Empty && referenceChanges.Empty) ||
                (_options.PrebuiltOutputReuse == PrebuiltOutputReuseType.WhenNoSourceChange &&
                 sourceChanges.Empty && referenceChanges.Added.Count == 0 && referenceChanges.Removed.Count == 0));

            if (reusePrebuilt)
            {
                _logger.Info("Reuse prebuilt output");

                // write dll

                var dllFile = Path.Combine(_options.WorkDirectory, _options.Output);
                using (var dllStream = new FileStream(dllFile, FileMode.Create))
                {
                    _outputDllStream.Seek(0L, SeekOrigin.Begin);
                    _outputDllStream.CopyTo(dllStream);
                }

                // write pdb or mdb

                switch (_options.DebugSymbolFile)
                {
                    case DebugSymbolFileType.Pdb:
                        var pdbFile = Path.Combine(_options.WorkDirectory, Path.ChangeExtension(_options.Output, ".pdb"));
                        using (var debugSymbolStream = new FileStream(pdbFile, FileMode.Create))
                        {
                            _outputDebugSymbolStream.Seek(0L, SeekOrigin.Begin);
                            _outputDebugSymbolStream.CopyTo(debugSymbolStream);
                        }
                        break;

                    case DebugSymbolFileType.PdbToMdb:
                    case DebugSymbolFileType.Mdb:
                        var mdbFile = Path.Combine(_options.WorkDirectory, _options.Output + ".mdb");
                        using (var debugSymbolStream = new FileStream(mdbFile, FileMode.Create))
                        {
                            _outputDebugSymbolStream.Seek(0L, SeekOrigin.Begin);
                            _outputDebugSymbolStream.CopyTo(debugSymbolStream);
                        }
                        break;
                }

                result.Succeeded = true;
            }
            else
            {
                _logger.Info("Emit");

                Emit(result);
            }

            return result;
        }

        private MetadataReference CreateReference(string file)
        {
            return MetadataReference.CreateFromFile(Path.Combine(_options.WorkDirectory, file));
        }

        private SyntaxTree ParseSource(string file, CSharpParseOptions parseOption)
        {
            var fileFullPath = Path.Combine(_options.WorkDirectory, file);
            var text = File.ReadAllText(fileFullPath);
            return CSharpSyntaxTree.ParseText(text, parseOption, fileFullPath, Encoding.UTF8);
        }

        private void Emit(CompileResult result)
        {
            _outputDllStream = new MemoryStream();
            _outputDebugSymbolStream = _options.DebugSymbolFile != DebugSymbolFileType.None ? new MemoryStream() : null;

            // emit to memory

            var r = _options.DebugSymbolFile == DebugSymbolFileType.Mdb
                ? _compilation.EmitWithMdb(_outputDllStream, _outputDebugSymbolStream)
                : _compilation.Emit(_outputDllStream, _outputDebugSymbolStream);

            // memory to file

            var dllFile = Path.Combine(_options.WorkDirectory, _options.Output);
            var mdbFile = Path.Combine(_options.WorkDirectory, _options.Output + ".mdb");
            var pdbFile = Path.Combine(_options.WorkDirectory, Path.ChangeExtension(_options.Output, ".pdb"));

            var emitDebugSymbolFile = _options.DebugSymbolFile == DebugSymbolFileType.Mdb ? mdbFile : pdbFile;

            using (var dllStream = new FileStream(dllFile, FileMode.Create))
            {
                _outputDllStream.Seek(0L, SeekOrigin.Begin);
                _outputDllStream.CopyTo(dllStream);
            }

            if (_outputDebugSymbolStream != null)
            {
                using (var debugSymbolStream = new FileStream(emitDebugSymbolFile, FileMode.Create))
                {
                    _outputDebugSymbolStream.Seek(0L, SeekOrigin.Begin);
                    _outputDebugSymbolStream.CopyTo(debugSymbolStream);
                }
            }

            // gather result

            foreach (var d in r.Diagnostics)
            {
                if (d.Severity == DiagnosticSeverity.Warning && d.IsWarningAsError == false)
                    result.Warnings.Add(GetDiagnosticString(d, "warning"));
                else if (d.Severity == DiagnosticSeverity.Error || d.IsWarningAsError)
                    result.Errors.Add(GetDiagnosticString(d, "error"));
            }

            result.Succeeded = r.Success;

            // pdb to mdb when required

            if (_options.DebugSymbolFile == DebugSymbolFileType.PdbToMdb)
            {
                var code = ConvertPdb2Mdb(dllFile);
                _logger.Info("pdb2mdb exited with {0}", code);
                File.Delete(pdbFile);

                // read converted mdb file to cache contents
                _outputDebugSymbolStream = new MemoryStream(File.ReadAllBytes(mdbFile));
            }
        }

        private string GetDiagnosticString(Diagnostic diagnostic, string type)
        {
            var line = diagnostic.Location.GetLineSpan();

            // Path could be null
            if (string.IsNullOrEmpty(line.Path))
                return $"None: " + $"{type} {diagnostic.Id}: {diagnostic.GetMessage()}";

            // Unity3d must have a relative path starting with "Assets/".
            var path = (line.Path.StartsWith(_options.WorkDirectory + "/") || line.Path.StartsWith(_options.WorkDirectory + "\\"))
                ? line.Path.Substring(_options.WorkDirectory.Length + 1)
                : line.Path;

            return $"{path}({line.StartLinePosition.Line + 1},{line.StartLinePosition.Character + 1}): " + $"{type} {diagnostic.Id}: {diagnostic.GetMessage()}";
        }

        public static int ConvertPdb2Mdb(string dllFile)
        {
            var toolPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "pdb2mdb.exe");
            var process = new Process();
            process.StartInfo = new ProcessStartInfo(toolPath, '"' + dllFile + '"');
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.Start();
            process.WaitForExit();
            return process.ExitCode;
        }
    }
}
