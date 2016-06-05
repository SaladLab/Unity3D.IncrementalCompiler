using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using Microsoft.CodeAnalysis;

namespace IncrementalCompiler
{
    public enum DebugSymbolFileType
    {
        None,
        Pdb,
        PdbToMdb,
        Mdb
    }

    public enum PrebuiltOutputReuseType
    {
        None,
        WhenNoChange,
        WhenNoSourceChange
    }

    [DataContract]
    public class CompileOptions
    {
        [DataMember] public string WorkDirectory;
        [DataMember] public string AssemblyName;
        [DataMember] public string Output;
        [DataMember] public List<string> Defines = new List<string>();
        [DataMember] public List<string> References = new List<string>();
        [DataMember] public List<string> Files = new List<string>();
        [DataMember] public List<string> NoWarnings = new List<string>();
        [DataMember] public List<string> Options = new List<string>();
        [DataMember] public DebugSymbolFileType DebugSymbolFile;
        [DataMember] public PrebuiltOutputReuseType PrebuiltOutputReuse;

        public void ParseArgument(string[] args)
        {
            foreach (var arg in args)
            {
                if (arg.StartsWith("-"))
                {
                    string command;
                    string value;

                    var valueIdx = arg.IndexOf(':');
                    if (valueIdx != -1)
                    {
                        command = arg.Substring(1, valueIdx - 1).ToLower();
                        value = arg.Substring(valueIdx + 1);
                    }
                    else
                    {
                        command = arg.Substring(1).ToLower();
                        value = "";
                    }

                    switch (command)
                    {
                        case "r":
                        case "reference":
                            References.Add(value.Trim('"'));
                            break;

                        case "define":
                            Defines.Add(value.Trim());
                            break;

                        case "out":
                            Output = value.Trim('"');
                            AssemblyName = Path.GetFileNameWithoutExtension(value);
                            break;

                        case "nowarn":
                            foreach (var id in value.Split(new char[] { ',', ';', ' ' },
                                                           StringSplitOptions.RemoveEmptyEntries))
                            {
                                int num;
                                NoWarnings.Add(int.TryParse(id, out num)
                                    ? string.Format("CS{0:0000}", num)
                                    : id);
                            }
                            break;

                        default:
                            Options.Add(arg);
                            break;
                    }
                }
                else if (arg.StartsWith("@"))
                {
                    var subArgs = new List<string>();
                    foreach (var line in File.ReadAllLines(arg.Substring(1)))
                    {
                        subArgs.AddRange(CommandLineParser.SplitCommandLineIntoArguments(line, removeHashComments: true));
                    }
                    ParseArgument(subArgs.ToArray());
                }
                else
                {
                    var path = arg.Trim('"');
                    if (path.Length > 0)
                        Files.Add(path);
                }
            }
        }
    }
}
