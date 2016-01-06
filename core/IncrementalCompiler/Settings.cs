using System.IO;
using System.Reflection;
using System.Xml.Serialization;

namespace IncrementalCompiler
{
    public class Settings
    {
        public DebugSymbolFileType DebugSymbolFile;
        public PrebuiltOutputReuseType PrebuiltOutputReuse;

        public static Settings Default = new Settings
        {
            DebugSymbolFile = DebugSymbolFileType.Mdb,
            PrebuiltOutputReuse = PrebuiltOutputReuseType.WhenNoChange,
        };

        public static Settings Load()
        {
            var fileName = Path.ChangeExtension(Assembly.GetEntryAssembly().Location, ".xml");
            if (File.Exists(fileName) == false)
                return null;

            using (var stream = new FileStream(fileName, FileMode.Open))
            {
                return Load(stream);
            }
        }

        public static Settings Load(Stream stream)
        {
            var deserializer = new XmlSerializer(typeof(Settings));
            return (Settings)deserializer.Deserialize(stream);
        }

        public static void Save(Stream stream, Settings settings)
        {
            var serializer = new XmlSerializer(typeof(Settings));
            serializer.Serialize(stream, settings);
        }
    }
}
