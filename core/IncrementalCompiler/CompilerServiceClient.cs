using System.ServiceModel;

namespace IncrementalCompiler
{
    public class CompilerServiceClient
    {
        public static CompileResult Request(int parentProcessId, string currentPath, CompileOptions options)
        {
            var address = "net.pipe://localhost/Unity3D.IncrementalCompiler/" + parentProcessId;

            var binding = new NetNamedPipeBinding(NetNamedPipeSecurityMode.None)
            {
                MaxBufferSize = int.MaxValue,
                MaxReceivedMessageSize = int.MaxValue
            };
            var ep = new EndpointAddress(address);
            var channel = ChannelFactory<ICompilerService>.CreateChannel(binding, ep);
            return channel.Build(currentPath, options);
        }
    }
}
