namespace Rpc.Models
{
    using DotNetty.Rpc.Service;

    public class TestAddressQuery : AbsMessage<AddressInfo>
    {

    }

    public class AddressInfo : IResult
    {

    }
}
