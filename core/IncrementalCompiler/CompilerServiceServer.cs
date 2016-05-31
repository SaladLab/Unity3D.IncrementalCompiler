using System;
using System.Diagnostics;
using System.ServiceModel;
using NLog;

namespace IncrementalCompiler
{
    public class CompilerServiceServer
    {
        public static int Run(Logger logger, int parentProcessId)
        {
            // get parent process which will be monitored

            Process parentProcess = null;
            if (parentProcessId != 0)
            {
                try
                {
                    parentProcess = Process.GetProcessById(parentProcessId);
                }
                catch (Exception e)
                {
                    logger.Error(e, "Cannot find parentProcess (Id={0})", parentProcessId);
                    return 1;
                }
            }

            // open service

            try
            {
                var address = "net.pipe://localhost/Unity3D.IncrementalCompiler/" + parentProcessId;
                var serviceHost = new ServiceHost(typeof(CompilerService));
                var binding = new NetNamedPipeBinding(NetNamedPipeSecurityMode.None)
                {
                    MaxBufferSize = int.MaxValue,
                    MaxReceivedMessageSize = int.MaxValue
                };
                serviceHost.AddServiceEndpoint(typeof(ICompilerService), binding, address);
                serviceHost.Open();
            }
            catch (Exception e)
            {
                logger.Error(e, "Service Host got an error");
                return 1;
            }

            if (parentProcess != null)
            {
                parentProcess.WaitForExit();
                logger.Info("Parent process just exited. (PID={0})", parentProcess.Id);
            }

            return 0;
        }
    }
}
