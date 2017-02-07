namespace Rpc.Models.Test
{
    using DotNetty.Rpc.Service;

    public class TestCustomerQuery : AbsMessage<CustomerInfo>
    {
        public int Id { get; set; }
    }

    public class CustomerInfo : IResult
    {
        public int Id { get; set; }

        public string Name { get; set; }
    }
}
