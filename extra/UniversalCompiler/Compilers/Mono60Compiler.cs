using System.Diagnostics;
using System.IO;

internal class Mono60Compiler : Compiler
{
	public Mono60Compiler(Logger logger, string directory)
		: base(logger, Path.Combine(directory, "mcs.exe"), null) { }

	public override string Name => "Mono C# 6.0";

	protected override Process CreateCompilerProcess(Platform platform, string unityEditorDataDir, string responseFile)
	{
		var systemCoreDllPath = Path.Combine(unityEditorDataDir, @"Mono/lib/mono/2.0/System.Core.dll");

		string processArguments;
		if (platform == Platform.Windows)
		{
			processArguments = $"-sdk:2 -debug+ -langversion:Default -r:\"{systemCoreDllPath}\" {responseFile}";
		}
		else
		{
			processArguments = $"-sdk:2 -debug+ -langversion:Default {responseFile}";
		}

		var process = new Process();
		process.StartInfo = CreateOSDependentStartInfo(platform, ProcessRuntime.CLR40, compilerPath, processArguments, unityEditorDataDir);
		return process;
	}

	public static bool IsAvailable(string directory) => File.Exists(Path.Combine(directory, "mcs.exe"));
}