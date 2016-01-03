using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;

namespace IncrementalCompiler
{
    [ServiceContract(Namespace = "http://github.com/Unity3D.RoslynCompiler")]
    public interface ICompilerService
    {
        [OperationContract]
        bool Build(string projectPath, CompilerOptions options);
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, IncludeExceptionDetailInFaults = true)]
    public class CompilerService : ICompilerService
    {
        private string _projectPath;
        private Dictionary<string, Compiler> _compilerMap;

        public bool Build(string projectPath, CompilerOptions options)
        {
            if (string.IsNullOrEmpty(_projectPath) || _projectPath != projectPath)
            {
                // Flush existing

                _compilerMap = new Dictionary<string, Compiler>();
            }

            _projectPath = projectPath;

            Compiler compiler;
            if (_compilerMap.TryGetValue(options.Output, out compiler) == false)
            {
                compiler = new Compiler();
                _compilerMap.Add(options.Output, compiler);
            }

            try
            {
                compiler.Build(options);
            }
            catch (Exception e)
            {
                // log
                throw;
            }

            return true;
        }
    }
}
