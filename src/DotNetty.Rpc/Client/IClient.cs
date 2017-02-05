using System.Threading.Tasks;

namespace DotNetty.Rpc.Client
{
    using System.Net;
    using DotNetty.Rpc.Protocol;

    public interface IClient
    {
        Task Connect(EndPoint socketAddress);

        Task SendRequest<T>(AbsMessage<T> request, int timeout = 10000) where T : IResult;

        Task Close();
    }
}
