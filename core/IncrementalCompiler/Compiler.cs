using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using System.Text;

namespace IncrementalCompiler
{
    public class Compiler
    {
        private CSharpCompilation _compilation;
        private CompilerOptions _options;
        private FileTimeList _referenceFileList;
        private FileTimeList _sourceFileList;
        private Dictionary<string, MetadataReference> _referenceMap;
        private Dictionary<string, SyntaxTree> _sourceMap;

        public void Build(CompilerOptions options)
        {
            if (_compilation == null ||
                _options.AssemblyName != options.AssemblyName ||
                _options.Output != options.Output ||
                Enumerable.SequenceEqual(_options.Defines, options.Defines) == false)
            {
                BuildFull(options);
            }
            else
            {
                BuildIncremental(options);
            }
        }

        private void BuildFull(CompilerOptions options)
        {
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

            Emit();
        }

        private void BuildIncremental(CompilerOptions options)
        {
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

            Emit();
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

        private void Emit()
        {
            using (var peStream = new FileStream(_options.Output, FileMode.Create))
            using (var pdbStream = new FileStream(Path.ChangeExtension(_options.Output, ".pdb"), FileMode.Create))
            {
                var result = _compilation.Emit(peStream, pdbStream);

                if (!result.Success)
                {
                    var failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (var diagnostic in failures)
                    {
                        var line = diagnostic.Location.GetLineSpan();
                        Console.Error.WriteLine("{0}({1}): {2} {3}",
                            line.Path,
                            line.StartLinePosition.Line + 1,
                            diagnostic.Id,
                            diagnostic.GetMessage());
                    }
                }
            }
        }
    }
}
