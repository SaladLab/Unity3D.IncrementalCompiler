using System.Diagnostics;

internal class Mono30Compiler : Compiler
{
	public Mono30Compiler(Logger logger, string compilerPath) : base(logger, compilerPath, null) { }
	public override string Name => "Mono C# 3.0";

	protected override Process CreateCompilerProcess(Platform platform, string monoProfile, string unityEditorDataDir, string responseFile)
	{
		var process = new Process();
		process.StartInfo = CreateOSDependentStartInfo(platform, ProcessRuntime.CLR20, compilerPath, responseFile, unityEditorDataDir);
		return process;
	}
}
