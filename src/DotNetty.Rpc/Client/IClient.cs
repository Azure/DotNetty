using System.Threading.Tasks;

namespace DotNetty.Rpc.Client
{
    using System.Net;
    using DotNetty.Rpc.Protocol;

    public interface IClient
    {
        Task Connect(EndPoint socketAddress);

        Task<RpcResponse> SendRequest(RpcRequest request, int timeout = 10000);

        Task Close();
    }
}
