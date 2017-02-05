namespace Rpc.Services.Test
{
    using System;
    using DotNetty.Rpc.Service;
    using Rpc.Models;
    using System.Threading.Tasks;

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
                Name = "Test"
            };
            return Task.FromResult(eventData);
        }
    }
}
