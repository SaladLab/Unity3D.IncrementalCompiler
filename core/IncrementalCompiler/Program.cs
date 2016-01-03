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
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "-server")
            {
                return RunAsServer(args);
            }
            else
            {
                return RunAsClient(args);
            }
        }

        static int RunAsClient(string[] args)
        {
            SetupLogger("RoslynCompiler.log");

            var logger = LogManager.GetLogger("Client");
            logger.Info("Started");

            var currentPath = Directory.GetCurrentDirectory();
            var options = new CompilerOptions();
            options.ParseArgument(args, currentPath);
            options.References = options.References.Distinct().ToList();
            options.Files = options.Files.Distinct().ToList();

            logger.Info("CurrentDir: {0}", Directory.GetCurrentDirectory());
            logger.Info("Output: {0}", options.Output);

            if (string.IsNullOrEmpty(options.Output))
            {
                Console.WriteLine("No output");
                return 1;
            }

            // TODO: GET PARENT PROCESS ID

            var parentProcessId = Process.GetProcessesByName("Unity").FirstOrDefault().Id;
            Console.WriteLine("" + parentProcessId);

            // RUN

            var done = false;
            Process serverProcess = null;
            while (true)
            {
                try
                {
                    var w = new Stopwatch();
                    w.Start();
                    Console.WriteLine("Start to Request");
                    CompilerServiceClient.Request(parentProcessId, currentPath, options);
                    w.Stop();
                    Console.WriteLine("Done: " + w.Elapsed.TotalSeconds + "sec");
                    return 0;
                }
                catch (EndpointNotFoundException)
                {
                    if (serverProcess == null)
                    {
                        serverProcess = Process.Start(
                            new ProcessStartInfo
                            {
                                FileName = Assembly.GetEntryAssembly().Location,
                                Arguments = "-server " + parentProcessId,
                                WindowStyle = ProcessWindowStyle.Hidden
                            });
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        if (serverProcess.HasExited == false)
                            Thread.Sleep(100);
                        else
                            return 1;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return 1;
                }
            }
        }

        static int RunAsServer(string[] args)
        {
            SetupLogger("RoslynCompiler-Server.log");

            var logger = LogManager.GetLogger("Server");
            logger.Info("Started");

            var parentProcessId = 0;
            if (args.Length >= 2 && int.TryParse(args[1], out parentProcessId) == false)
            {
                logger.Error("Error in parsing parentProcessId (arg={0})", args[1]);
                return 1;
            }

            return CompilerServiceServer.Run(logger, parentProcessId);
        }

        static void SetupLogger(string fileName)
        {
            var config = new LoggingConfiguration();

            var consoleTarget = new ColoredConsoleTarget
            {
                Layout = @"${date:format=HH\:mm\:ss} ${logger} ${message}"
            };
            config.AddTarget("console", consoleTarget);
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, consoleTarget));

            var logDirectory = Directory.Exists(".\\Temp") ? ".\\Temp\\" : ".\\";
            var fileTarget = new FileTarget
            {
                FileName = logDirectory + fileName,
                Layout = @"${date:format=HH\:mm\:ss} ${logger} ${message}"
            };
            config.AddTarget("file", fileTarget);
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, fileTarget));

            LogManager.Configuration = config;
        }
    }
}
