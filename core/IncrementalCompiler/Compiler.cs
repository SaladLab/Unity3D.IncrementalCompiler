using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using System.Text;
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

        public CompileResult Build(CompileOptions options)
        {
            if (_compilation == null ||
                _options.AssemblyName != options.AssemblyName ||
                _options.Output != options.Output ||
                Enumerable.SequenceEqual(_options.Defines, options.Defines) == false)
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

            _compilation = CSharpCompilation.Create(
                options.AssemblyName,
                syntaxTrees: _sourceMap.Values,
                references: _referenceMap.Values,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            Emit(result);

            return result;
        }

        private CompileResult BuildIncremental(CompileOptions options)
        {
            var result = new CompileResult();

            _options = options;

            // TODO: guard failure of compilation, ...

            // update reference files

            var referenceChanges = _referenceFileList.Update(options.References);
            foreach (var file in referenceChanges.Added)
            {
                var reference = CreateReference(file);
                _compilation = _compilation.AddReferences(reference);
                _referenceMap.Add(file, reference);
            }
            foreach (var file in referenceChanges.Changed)
            {
                var reference = CreateReference(file);
                _compilation = _compilation.RemoveReferences(_referenceMap[file])
                                           .AddReferences(reference);
                _referenceMap[file] = reference;
            }
            foreach (var file in referenceChanges.Removed)
            {
                _compilation = _compilation.RemoveReferences(_referenceMap[file]);
                _referenceMap.Remove(file);
            }

            // update source files

            var sourceChanges = _sourceFileList.Update(options.Files);
            var parseOption = new CSharpParseOptions(LanguageVersion.CSharp6, DocumentationMode.Parse, SourceCodeKind.Regular, options.Defines);
            foreach (var file in sourceChanges.Added)
            {
                var syntaxTree = ParseSource(file, parseOption);
                _compilation = _compilation.AddSyntaxTrees(syntaxTree);
                _sourceMap.Add(file, syntaxTree);
            }
            foreach (var file in sourceChanges.Changed)
            {
                var syntaxTree = ParseSource(file, parseOption);
                _compilation = _compilation.RemoveSyntaxTrees(_sourceMap[file])
                                           .AddSyntaxTrees(syntaxTree);
                _sourceMap[file] = syntaxTree;
            }
            foreach (var file in sourceChanges.Removed)
            {
                _compilation = _compilation.RemoveSyntaxTrees(_sourceMap[file]);
                _sourceMap.Remove(file);
            }

            Emit(result);

            return result;
        }

        private MetadataReference CreateReference(string file)
        {
            return MetadataReference.CreateFromFile(file);
        }

        private SyntaxTree ParseSource(string file, CSharpParseOptions parseOption)
        {
            return CSharpSyntaxTree.ParseText(File.ReadAllText(file),
                                              parseOption,
                                              file,
                                              Encoding.UTF8);
        }

        private void Emit(CompileResult result)
        {
            using (var peStream = new FileStream(_options.Output, FileMode.Create))
            using (var pdbStream = new FileStream(Path.ChangeExtension(_options.Output, ".pdb"), FileMode.Create))
            {
                var r = _compilation.Emit(peStream, pdbStream);

                foreach (var d in r.Diagnostics)
                {
                    if (d.Severity == DiagnosticSeverity.Warning && d.IsWarningAsError == false)
                        result.Warnings.Add(GetDiagnosticString(d));
                    else if (d.Severity == DiagnosticSeverity.Error || d.IsWarningAsError)
                        result.Errors.Add(GetDiagnosticString(d));
                }

                result.Succeeded = r.Success;
            }
        }

        private static string GetDiagnosticString(Diagnostic diagnostic)
        {
            var line = diagnostic.Location.GetLineSpan();
            return $"{line.Path}({line.StartLinePosition.Line + 1}): {diagnostic.Id} {diagnostic.GetMessage()}";
        }
    }
}
