using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor.Scripting;
using UnityEditor.Scripting.Compilers;
using UnityEditor.Utils;
using UnityEngine;

internal class CustomCSharpCompiler : MonoCSharpCompiler
{
#if UNITY4
	public CustomCSharpCompiler(MonoIsland island, bool runUpdater) : base(island)
	{
	}
#else
	public CustomCSharpCompiler(MonoIsland island, bool runUpdater) : base(island, runUpdater)
	{
	}
#endif

	private string[] GetAdditionalReferences()
	{
		// calling base method via reflection
		var bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
		var methodInfo = GetType().BaseType.GetMethod(nameof(GetAdditionalReferences), bindingFlags);
		var result = (string[])methodInfo.Invoke(this, null);
		return result;
	}

	private string GetCompilerPath(List<string> arguments)
	{
		// calling base method via reflection
		var bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
		var methodInfo = GetType().BaseType.GetMethod(nameof(GetCompilerPath), bindingFlags);
		var result = (string)methodInfo.Invoke(this, new object[] {arguments});
		return result;
	}

	private string GetUniversalCompilerPath()
	{
		var basePath = Path.Combine(Directory.GetCurrentDirectory(), "Compiler");
		var compilerPath = Path.Combine(basePath, "UniversalCompiler.exe");
		return File.Exists(compilerPath) ? compilerPath : null;
	}

	// Copy of MonoCSharpCompiler.StartCompiler()
	// The only reason it exists is to call the new implementation
	// of GetCompilerPath(...) which is non-virtual unfortunately.
	protected override Program StartCompiler()
	{
		var arguments = new List<string>
		{
			"-debug",
			"-target:library",
			"-nowarn:0169",
			"-out:" + PrepareFileName(_island._output),
		};
		foreach (var reference in _island._references)
		{
			arguments.Add("-r:" + PrepareFileName(reference));
		}

		foreach (var define in _island._defines.Distinct())
		{
			arguments.Add("-define:" + define);
		}

		foreach (var file in _island._files)
		{
			arguments.Add(PrepareFileName(file));
		}

		var additionalReferences = GetAdditionalReferences();
		foreach (string path in additionalReferences)
		{
			var text = Path.Combine(GetProfileDirectory(), path);
			if (File.Exists(text))
			{
				arguments.Add("-r:" + PrepareFileName(text));
			}
		}

		var universalCompilerPath = GetUniversalCompilerPath();
		if (universalCompilerPath != null)
		{
			// use universal compiler.
			var defaultCompilerName = Path.GetFileNameWithoutExtension(GetCompilerPath(arguments));
			arguments.Add("-define:__UNITY_PROCESSID__" + System.Diagnostics.Process.GetCurrentProcess().Id);
			arguments.Add("-define:__UNITY_PROFILE__" + Path.GetFileName(base.GetProfileDirectory()).Replace(".", "_"));
			var rspFileName = "Assets/" + defaultCompilerName + ".rsp";
			if (File.Exists(rspFileName))
				arguments.Add("@" + rspFileName);
			return StartCompiler(_island._target, universalCompilerPath, arguments);
		}
		else
		{
			// fallback to the default compiler.
			Debug.LogWarning($"Universal C# compiler not found in project directory. Use the default compiler");
			return StartCompiler(_island._target, GetCompilerPath(arguments), arguments);
		}
	}
}
