using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace IncrementalCompiler
{
    [DataContract]
    public class CompilerOptions
    {
        [DataMember] public string AssemblyName;
        [DataMember] public string Output;
        [DataMember] public List<string> Defines = new List<string>();
        [DataMember] public List<string> References = new List<string>();
        [DataMember] public List<string> Files = new List<string>();

        public void ParseArgument(string[] args, string currentPath)
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
                            References.Add(Path.Combine(currentPath, value.Trim('"')));
                            break;

                        case "define":
                            Defines.Add(value);
                            break;

                        case "out":
                            Output = Path.Combine(currentPath, value.Trim('"'));
                            AssemblyName = Path.GetFileNameWithoutExtension(value);
                            break;
                    }
                }
                else if (arg.StartsWith("@"))
                {
                    // more options in specified file
                    var argPath = Path.Combine(currentPath, arg.Substring(1));
                    var lines = File.ReadAllLines(argPath);
                    ParseArgument(lines, currentPath);
                }
                else
                {
                    var path = Path.Combine(currentPath, arg.Trim('"'));
                    Files.Add(path);
                }
            }
        }
    }
}
