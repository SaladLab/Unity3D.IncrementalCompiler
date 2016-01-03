using NLog;
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
        private Logger _logger = LogManager.GetLogger("CompilerService");
        private string _projectPath;
        private Dictionary<string, Compiler> _compilerMap;

        public bool Build(string projectPath, CompilerOptions options)
        {
            _logger.Info("Build(projectPath={0}, output={1})", projectPath, options.Output);

            if (string.IsNullOrEmpty(_projectPath) || _projectPath != projectPath)
            {
                // Flush existing

                _compilerMap = new Dictionary<string, Compiler>();
                _logger.Info("Flush old project. (Project={0})", _projectPath);
            }

            _projectPath = projectPath;

            Compiler compiler;
            if (_compilerMap.TryGetValue(options.Output, out compiler) == false)
            {
                compiler = new Compiler();
                _compilerMap.Add(options.Output, compiler);
                _logger.Info("Add new project. (Project={0})", _projectPath);
            }

            try
            {
                compiler.Build(options);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error in build.");
                throw;
            }

            return true;
        }
    }
}
