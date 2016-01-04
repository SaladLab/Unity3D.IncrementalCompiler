#define LOGGING_ENABLED

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

internal class Program
{
	private enum CompilerVersion
	{
		Version3Mono,
		Version5Mono,
		Version6Microsoft,
		Version6Mono,
		Version6IncrementalCompiler,
	}

	private enum Platform
	{
		Windows,
		Linux,
		Mac,
	}

	private enum ProcessRuntime
	{
		CLR40,
		CLR20,
	}

	private static readonly List<string> OutputLines = new List<string>();
	private static readonly List<string> ErrorLines = new List<string>();

	private static int Main(string[] args)
	{
		int exitCode;
		Logger logger = null;

#if LOGGING_ENABLED
		using (logger = new Logger())
#endif
		{
			try
			{
				exitCode = Compile(args, logger);
			}
			catch (Exception e)
			{
				exitCode = -1;
				Console.Error.Write($"Compiler redirection error: {e.GetType()}{Environment.NewLine}{e.Message} {e.StackTrace}");
			}
		}

		return exitCode;
	}

	private static int Compile(string[] args, Logger logger)
	{
		logger?.AppendHeader();

		var compilationOptions = GetCompilationOptions(args);
		var unityEditorDataDir = GetUnityEditorDataDir();
		var targetAssembly = compilationOptions.First(line => line.StartsWith("-out:")).Substring(10).Trim('\'');

		logger?.Append($"smcs.exe version: {Assembly.GetExecutingAssembly().GetName().Version}");
		logger?.Append($"Platform: {CurrentPlatform}");
		logger?.Append($"Target assembly: {targetAssembly}");
		logger?.Append($"Project directory: {Directory.GetCurrentDirectory()}");
		logger?.Append($"Unity directory: {unityEditorDataDir}");

		if (CurrentPlatform != Platform.Windows && CurrentPlatform != Platform.Mac)
		{
			logger?.Append("");
			logger?.Append("Platform is not supported");
			return -1;
		}

		CompilerVersion compilerVersion;
		var basePath = Path.Combine(Directory.GetCurrentDirectory(), "Compiler");
		var cscExists = File.Exists(Path.Combine(basePath, "csc.exe")); // Roslyn Compiler
		var icsExists = File.Exists(Path.Combine(basePath, "ics.exe")); // Incremental Compiler
		var mcsExists = File.Exists(Path.Combine(basePath, "mcs.exe")); // Mono Compiler

		logger?.Append($"Compiler Check: csc={cscExists} ics={icsExists} mcs={mcsExists}");

		// Roslyn compiler currently works with windows only.
		if (cscExists && CurrentPlatform != Platform.Windows)
		{
			logger?.Append("Microsoft C# 6.0 compiler is not supported on the current platform. Looking for another compiler...");
		}

		if (icsExists)
		{
			compilerVersion = CompilerVersion.Version6IncrementalCompiler;
		}
		else if (cscExists && CurrentPlatform == Platform.Windows)
		{
			compilerVersion = CompilerVersion.Version6Microsoft;
		}
		else if (mcsExists)
		{
			compilerVersion = CompilerVersion.Version6Mono;
		}
		else if (compilationOptions.Any(line => line.Contains("AsyncBridge.Net35.dll")))
		{
			compilerVersion = CompilerVersion.Version5Mono;
		}
		else
		{
			compilerVersion = CompilerVersion.Version3Mono;
		}

		logger?.Append($"Compiler: {compilerVersion}");
		logger?.Append("");
		logger?.Append("- Compilation -----------------------------------------------");
		logger?.Append("");

		var stopwatch = Stopwatch.StartNew();
		var process = CreateCompilerProcess(compilerVersion, unityEditorDataDir, args[0]);

		logger?.Append($"Process: {process.StartInfo.FileName}");
		logger?.Append($"Arguments: {process.StartInfo.Arguments}");

		process.Start();
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();
		process.WaitForExit();

		stopwatch.Stop();
		logger?.Append($"Exit code: {process.ExitCode}");
		logger?.Append($"Elapsed time: {stopwatch.ElapsedMilliseconds / 1000f:F2} sec");

		if (compilerVersion == CompilerVersion.Version6Microsoft)
		{
			// Microsoft's compiler writes all warnings and errors to the standard output channel,
			// so move them to the error channel skipping first 3 lines that are just part of the header.

			while (OutputLines.Count > 3)
			{
				var line = OutputLines[3];
				OutputLines.RemoveAt(3);
				ErrorLines.Add(line);
			}
		}

		logger?.Append("");
		logger?.Append("- Compiler output:");

		var lines = from line in OutputLines
					let trimmedLine = line?.Trim()
					where string.IsNullOrEmpty(trimmedLine) == false
					select trimmedLine;

		int lineIndex = 0;
		foreach (var line in lines)
		{
			Console.Out.WriteLine(line);
			logger?.Append($"{lineIndex++}: {line}");
		}

		logger?.Append("");
		logger?.Append("- Compiler errors:");

		lines = from line in ErrorLines
				let trimmedLine = line?.Trim()
				where string.IsNullOrEmpty(trimmedLine) == false
				select trimmedLine;

		lineIndex = 0;
		foreach (var line in lines)
		{
			Console.Error.WriteLine(line);
			logger?.Append($"{lineIndex++}: {line}");
		}

		if (process.ExitCode != 0 || (compilerVersion != CompilerVersion.Version6Microsoft &&
									  compilerVersion != CompilerVersion.Version6IncrementalCompiler))
		{
			return process.ExitCode;
		}

		logger?.Append("");
		logger?.Append("- PDB to MDB conversion --------------------------------------");
		logger?.Append("");

		OutputLines.Clear();
		ErrorLines.Clear();

		var pdb2mdbPath = Path.Combine(basePath, "pdb2mdb.exe");
		var libraryPath = Path.Combine("Temp", targetAssembly);
		var pdbPath = Path.Combine("Temp", Path.GetFileNameWithoutExtension(targetAssembly) + ".pdb");

		var startInfo = OSDependentStartInfo(ProcessRuntime.CLR40, pdb2mdbPath, libraryPath, unityEditorDataDir);
		startInfo.UseShellExecute = false;
		startInfo.RedirectStandardOutput = true;
		startInfo.CreateNoWindow = true;

		process = new Process
		{
			StartInfo = startInfo
		};

		process.OutputDataReceived += Process_OutputDataReceived;

		logger?.Append($"Process: {process.StartInfo.FileName}");
		logger?.Append($"Arguments: {process.StartInfo.Arguments}");

		stopwatch.Reset();
		stopwatch.Start();

		process.Start();
		process.BeginOutputReadLine();
		process.WaitForExit();

		stopwatch.Stop();
		logger?.Append($"Elapsed time: {stopwatch.ElapsedMilliseconds / 1000f:F2} sec");

		File.Delete(pdbPath);

		logger?.Append("");
		logger?.Append("- pdb2mdb.exe output:");
		foreach (var line in OutputLines)
		{
			logger?.Append(line);
		}

		return 0;
	}

	private static void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
	{
		OutputLines.Add(e.Data);
	}

	private static void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
	{
		ErrorLines.Add(e.Data);
	}

	private static Process CreateCompilerProcess(CompilerVersion version, string unityEditorDataDir, string responseFile)
	{
		string processPath;
		string processArguments;

		var systemDllPath = Path.Combine(unityEditorDataDir, @"Mono/lib/mono/2.0/System.dll");
		var systemCoreDllPath = Path.Combine(unityEditorDataDir, @"Mono/lib/mono/2.0/System.Core.dll");
		var systemXmlDllPath = Path.Combine(unityEditorDataDir, @"Mono/lib/mono/2.0/System.Xml.dll");
		var mscorlib = Path.Combine(unityEditorDataDir, @"Mono/lib/mono/2.0/mscorlib.dll");
		var basePath = Path.Combine(Directory.GetCurrentDirectory(), "Compiler");

		ProcessRuntime processRuntime;
		switch (version)
		{
			case CompilerVersion.Version3Mono:
				processRuntime = ProcessRuntime.CLR20;
				processPath = Path.Combine(unityEditorDataDir, @"Mono/lib/mono/2.0/gmcs.exe");
				processArguments = responseFile;
				break;

			case CompilerVersion.Version5Mono:
				processRuntime = ProcessRuntime.CLR40;
				processPath = Path.Combine(unityEditorDataDir, @"MonoBleedingEdge/lib/mono/4.5/mcs.exe");
				if (CurrentPlatform == Platform.Windows)
				{
					processArguments = $"-sdk:2 -langversion:Future -r:\"{systemCoreDllPath}\" {responseFile}";
				}
				else
				{
					processArguments = $"-sdk:2 -langversion:Future {responseFile}";
				}
				break;

			case CompilerVersion.Version6Mono:
				processRuntime = ProcessRuntime.CLR40;
				processPath = Path.Combine(basePath, "mcs.exe");
				if (CurrentPlatform == Platform.Windows)
				{
					processArguments = $"-sdk:2 -r:\"{systemCoreDllPath}\" {responseFile}";
				}
				else
				{
					processArguments = $"-sdk:2 {responseFile}";
				}
				break;

			case CompilerVersion.Version6Microsoft:
				processRuntime = ProcessRuntime.CLR40;
				processPath = Path.Combine(basePath, "csc.exe");
				processArguments = $"-nostdlib+ -noconfig -r:\"{mscorlib}\" -r:\"{systemDllPath}\" -r:\"{systemCoreDllPath}\" -r:\"{systemXmlDllPath}\" {responseFile}";

				if (CurrentPlatform == Platform.Mac) // Always false since Roslyn is not supported on Mac
				{
					// Temp file is different in mac build for some reason
					var fileName = responseFile.Substring(1);
					var data = File.ReadAllText(fileName);
					data = data.Replace('\'', '\"');
					// Debug build does not work with this compiler on a mac
					// Unity does not work without debug build
					//					data = data.Replace("-debug", "");
					File.WriteAllText(fileName, data);
				}
				break;

			case CompilerVersion.Version6IncrementalCompiler:
				processRuntime = ProcessRuntime.CLR40;
				processPath = Path.Combine(basePath, "ics.exe");
				processArguments = $"-nostdlib+ -noconfig -r:\"{mscorlib}\" -r:\"{systemDllPath}\" -r:\"{systemCoreDllPath}\" -r:\"{systemXmlDllPath}\" {responseFile}";

				if (CurrentPlatform == Platform.Mac) // Always false since Roslyn is not supported on Mac
				{
					// Temp file is different in mac build for some reason
					var fileName = responseFile.Substring(1);
					var data = File.ReadAllText(fileName);
					data = data.Replace('\'', '\"');
					// Debug build does not work with this compiler on a mac
					// Unity does not work without debug build
					//					data = data.Replace("-debug", "");
					File.WriteAllText(fileName, data);
				}
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(version), version, null);
		}

		var startInfo = OSDependentStartInfo(processRuntime, processPath, processArguments, unityEditorDataDir);
		startInfo.RedirectStandardError = true;
		startInfo.RedirectStandardOutput = true;
		startInfo.UseShellExecute = false;

		var process = new Process
		{
			StartInfo = startInfo
		};

		process.OutputDataReceived += Process_OutputDataReceived;
		process.ErrorDataReceived += Process_ErrorDataReceived;

		return process;
	}

	private static ProcessStartInfo OSDependentStartInfo(ProcessRuntime processRuntime, string processPath, string processArguments, string unityEditorDataDir)
	{
		ProcessStartInfo startInfo;

		if (CurrentPlatform == Platform.Windows)
		{
			switch (processRuntime)
			{
				case ProcessRuntime.CLR20:
					var runtimePath = Path.Combine(unityEditorDataDir, @"Mono/bin/mono.exe");
					startInfo = new ProcessStartInfo(runtimePath, $"\"{processPath}\" {processArguments}");
					break;

				case ProcessRuntime.CLR40:
					startInfo = new ProcessStartInfo(processPath, processArguments);
					break;

				default:
					throw new ArgumentOutOfRangeException(nameof(processRuntime), processRuntime, null);
			}
		}
		else
		{
			string runtimePath;
			switch (processRuntime)
			{
				case ProcessRuntime.CLR40:
					if (File.Exists("/usr/local/bin/mono"))
					{
						runtimePath = "/usr/local/bin/mono";
					}
					else
					{
						runtimePath = Path.Combine(unityEditorDataDir, "MonoBleedingEdge/bin/mono");
					}
					break;

				case ProcessRuntime.CLR20:
					runtimePath = Path.Combine(unityEditorDataDir, @"Mono/bin/mono");
					break;

				default:
					throw new ArgumentOutOfRangeException(nameof(processRuntime), processRuntime, null);
			}

			startInfo = new ProcessStartInfo(runtimePath, $"\"{processPath}\" {processArguments}");

			if (processRuntime != ProcessRuntime.CLR20)
			{
				// Since we already are running under old mono runtime, we need to remove
				// these variables before launching the different version of the runtime.
				var vars = startInfo.EnvironmentVariables;
				vars.Remove("MONO_PATH");
				vars.Remove("MONO_CFG_DIR");
			}
		}

		return startInfo;
	}

	private static Platform CurrentPlatform
	{
		get
		{
			switch (Environment.OSVersion.Platform)
			{
				case PlatformID.Unix:
					// Well, there are chances MacOSX is reported as Unix instead of MacOSX.
					// Instead of platform check, we'll do a feature checks (Mac specific root folders)
					if (Directory.Exists("/Applications")
						& Directory.Exists("/System")
						& Directory.Exists("/Users")
						& Directory.Exists("/Volumes"))
					{
						return Platform.Mac;
					}
					return Platform.Linux;

				case PlatformID.MacOSX:
					return Platform.Mac;

				default:
					return Platform.Windows;
			}
		}
	}

	/// <summary>
	/// Returns the directory that contains Mono and MonoBleedingEdge directories
	/// </summary>
	private static string GetUnityEditorDataDir()
	{
		// Windows:
		// MONO_PATH: C:\Program Files\Unity\Editor\Data\Mono\lib\mono\2.0
		//
		// Mac OS X:
		// MONO_PATH: /Applications/Unity/Unity.app/Contents/Frameworks/Mono/lib/mono/2.0

		var monoPath = Environment.GetEnvironmentVariable("MONO_PATH").Replace("\\", "/");
		var index = monoPath.IndexOf("/Mono/lib/", StringComparison.InvariantCultureIgnoreCase);
		var path = monoPath.Substring(0, index);
		return path;
	}

	private static string[] GetCompilationOptions(string[] args)
	{
		var compilationOptions = File.ReadAllLines(args[0].TrimStart('@'));
		return compilationOptions;
	}
}