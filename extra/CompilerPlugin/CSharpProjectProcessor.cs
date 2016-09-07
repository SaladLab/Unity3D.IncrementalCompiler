using System.IO;
using System.Linq;
using UnityEditor;

public class CSharpProjectProcessor : AssetPostprocessor
{
	private static bool OnPreGeneratingCSProjectFiles()
	{
		var currentDirectory = Directory.GetCurrentDirectory();
		var projectFiles = Directory.GetFiles(currentDirectory, "*.csproj");

		foreach (var file in projectFiles)
		{
			UpdateProjectFile(file);
		}

		return false;
	}

	private static void UpdateProjectFile(string file)
	{
		// strip out `LangVersion` elements
		var lines = File.ReadAllLines(file);
		var newLines = lines.Where(line => line.Contains("<LangVersion") == false && line.Contains("LangVersion>") == false).ToArray();
		if (lines.Count() != newLines.Count())
			File.WriteAllLines(file, newLines);
	}
}
