using System;
using System.IO;
using System.Reflection;
using System.Xml.Linq;

public enum CompilerType
{
	Auto,
	Mono3,
	Mono5,
	Mono6,
	Microsoft6,
	Incremental6,
}

public class Settings
{
	public CompilerType Compiler;

	public static Settings Default = new Settings
	{
		Compiler = CompilerType.Auto,
	};

	public static Settings Load()
	{
		var fileName = Path.ChangeExtension(Assembly.GetEntryAssembly().Location, ".xml");
		if (File.Exists(fileName) == false)
			return null;

		using (var reader = File.OpenText(fileName))
		{
			return Load(reader);
		}
	}

	public static Settings Load(TextReader reader)
	{
		// To reduce start-up time, do manual parsing instead of using XmlSerializer
		var xdoc = XDocument.Load(reader).Element("Settings");
		return new Settings
		{
			Compiler = (CompilerType)Enum.Parse(typeof(CompilerType), xdoc.Element("Compiler").Value),
		};
	}
}
