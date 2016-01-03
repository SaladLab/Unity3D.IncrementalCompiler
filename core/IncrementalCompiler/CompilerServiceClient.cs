using NLog;
using System;
using System.ServiceModel;

namespace IncrementalCompiler
{
    public class CompilerServiceClient
    {
        public static void Request(int parentProcessId, string currentPath, CompilerOptions options)
        {
            var address = "net.pipe://localhost/Unity3D.IncrementalCompiler/" + parentProcessId;

            var binding = new NetNamedPipeBinding(NetNamedPipeSecurityMode.None)
            {
                MaxBufferSize = 1048576,
                MaxReceivedMessageSize = 1048576
            };
            var ep = new EndpointAddress(address);
            var channel = ChannelFactory<ICompilerService>.CreateChannel(binding, ep);
            channel.Build(currentPath, options);
        }
    }
}
