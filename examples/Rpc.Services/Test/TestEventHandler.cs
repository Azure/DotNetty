namespace Rpc.Services.Test
{
    using System;
    using DotNetty.Rpc.Service;
    using Rpc.Models;
    using System.Threading.Tasks;
    using Rpc.Models.Test;

    public class TestEventHandler: EventHandlerImpl
    {
        protected override void InitializeComponents()
        {
            this.AddEventListener<TestCityQuery>(this.Handler);
            this.AddEventListener<TestAddressQuery>(this.Handler);
        }

        private Task<TestAddressQuery> Handler(TestAddressQuery eventData)
        {
            throw new NotImplementedException();
        }

        private Task<TestCityQuery> Handler(TestCityQuery eventData)
        {
            eventData.ReturnValue = new CityInfo()
            {
                Id = eventData.Id,
                Name = @" App的开发无外乎从网络端获取数据显示在屏幕上，数据做些缓存或者持久化，所以网络层极为重要。原来只是把AFNetwork二次封装了一下，使得调用变得很简单，并没有深层次的考虑一些问题。"
            };
            return Task.FromResult(eventData);
        }
    }
}
