using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using NLog;
using NLog.Config;
using NLog.Targets;
using System.ServiceModel;

namespace IncrementalCompiler
{
    partial class Program
    {
        static int RunAsDev(string[] args)
        {
            SetupLogger("IncrementalCompiler.log");

            var logger = LogManager.GetLogger("Dev");
            logger.Info("Started");

            Directory.SetCurrentDirectory(@"..\..\..\..\samples\Basic");
            var curPath = Directory.GetCurrentDirectory();

            var options = new CompileOptions();
            options.ParseArgument(new string[]
            {
                "-nostdlib+",
                "-noconfig",
                "-r:" + @"C:/Program Files/Unity/Editor/Data\Mono/lib/mono/2.0/mscorlib.dll",
                "-r:" + @"C:/Program Files/Unity/Editor/Data\Mono/lib/mono/2.0/System.dll",
                "-r:" + @"C:/Program Files/Unity/Editor/Data\Mono/lib/mono/2.0/System.Core.dll",
                "-r:" + @"C:/Program Files/Unity/Editor/Data\Mono/lib/mono/2.0/System.Xml.dll",
                @"@Temp/UnityTempFile-abde314464ae1bc4cae691cbf128cb26",
            },
            curPath);
            options.References = options.References.Distinct().ToList();
            options.Files = options.Files.Distinct().ToList();

            var parentProcessId = Process.GetCurrentProcess().Id;

            Process serverProcess = null;

            while (true)
            {
                try
                {
                    var w = new Stopwatch();
                    w.Start();
                    Console.WriteLine("Run");

                    var result = CompilerServiceClient.Request(parentProcessId, curPath, options);

                    w.Stop();

                    Console.WriteLine("Done: Succeeded={0}. Duration={1}sec. ", result.Succeeded, w.Elapsed.TotalSeconds);
                    foreach (var warning in result.Warnings)
                        Console.WriteLine(warning);
                    foreach (var error in result.Errors)
                        Console.WriteLine(error);

                    Console.ReadLine();
                }
                catch (EndpointNotFoundException)
                {
                    if (serverProcess == null)
                    {
                        var a = new Thread(() => CompilerServiceServer.Run(logger, parentProcessId));
                        a.Start();
                        serverProcess = Process.GetCurrentProcess();
                        /*
                        serverProcess = Process.Start(
                            new ProcessStartInfo
                            {
                                FileName = Assembly.GetEntryAssembly().Location,
                                Arguments = "-server " + parentProcessId,
                                WindowStyle = ProcessWindowStyle.Hidden
                            });
                        */
                        Thread.Sleep(100);
                    }
                    else
                    {
                        if (serverProcess.HasExited == false)
                            Thread.Sleep(100);
                        else
                            serverProcess = null;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return 1;
                }
            }
            return 0;
        }
    }
}
