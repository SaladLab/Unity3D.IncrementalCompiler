using System.Diagnostics;
using System.IO;
using System.Linq;

internal class Mono50Compiler : Compiler
{
	public Mono50Compiler(Logger logger, string compilerPath) : base(logger, compilerPath, null) { }
	public override string Name => "Mono C# 5.0";

	protected override Process CreateCompilerProcess(Platform platform, string monoProfileDir, string unityEditorDataDir, string responseFile)
	{
		var systemCoreDllPath = Path.Combine(monoProfileDir, "System.Core.dll");

		string processArguments;
		if (platform == Platform.Windows && GetSdkValue(responseFile) == "2.0")
		{
			// -sdk:2.0 requires System.Core.dll. but -sdk:unity doesn't.
			processArguments = $"-r:\"{systemCoreDllPath}\" {responseFile}";
		}
		else
		{
			processArguments = responseFile;
		}

		var process = new Process();
		process.StartInfo = CreateOSDependentStartInfo(platform, ProcessRuntime.CLR40, compilerPath, processArguments, unityEditorDataDir);
		return process;
	}

	private string GetSdkValue(string responseFile)
	{
		var lines = File.ReadAllLines(responseFile.Substring(1));
		var sdkArg = lines.FirstOrDefault(line => line.StartsWith("-sdk:"));
		return (sdkArg != null) ? sdkArg.Substring(5) : "";
	}
}
