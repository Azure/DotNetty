namespace Rpc.Models.Test
{
    using DotNetty.Rpc.Service;

    public class TestCustomerPointQuery : AbsMessage<CustomerPointInfo>
    {
        public int Id { get; set; }
    }

    public class CustomerPointInfo : IResult
    {
        public int Id { get; set; }

        public string Name { get; set; }
    }
}
