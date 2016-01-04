using System.Diagnostics;
using System.IO;

internal class Mono50Compiler : Compiler
{
	public Mono50Compiler(Logger logger, string compilerPath) : base(logger, compilerPath, null) { }
	public override string Name => "Mono C# 5.0";

	protected override Process CreateCompilerProcess(Platform platform, string unityEditorDataDir, string responseFile)
	{
		var systemCoreDllPath = Path.Combine(unityEditorDataDir, @"Mono/lib/mono/2.0/System.Core.dll");

		string processArguments;
		if (platform == Platform.Windows)
		{
			processArguments = $"-sdk:2 -debug+ -langversion:Future -r:\"{systemCoreDllPath}\" {responseFile}";
		}
		else
		{
			processArguments = $"-sdk:2 -debug+ -langversion:Future {responseFile}";
		}

		var process = new Process();
		process.StartInfo = CreateOSDependentStartInfo(platform, ProcessRuntime.CLR40, compilerPath, processArguments, unityEditorDataDir);
		return process;
	}
}