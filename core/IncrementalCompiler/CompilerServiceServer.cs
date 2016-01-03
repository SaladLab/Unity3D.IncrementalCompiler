using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.Threading;
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

            var address = "net.pipe://localhost/Unity3D.IncrementalCompiler/" + parentProcessId;
            var serviceHost = new ServiceHost(typeof(CompilerService));
            var binding = new NetNamedPipeBinding(NetNamedPipeSecurityMode.None)
            {
                MaxBufferSize = 1048576,
                MaxReceivedMessageSize = 1048576
            };
            serviceHost.AddServiceEndpoint(typeof(ICompilerService), binding, address);
            serviceHost.Open();

            if (parentProcess != null)
                parentProcess.WaitForExit();

            return 0;
        }
    }
}
