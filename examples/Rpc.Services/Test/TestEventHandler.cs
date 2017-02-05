namespace Rpc.Services.Test
{
    using DotNetty.Rpc.Service;
    using Rpc.Models;
    using System.Threading.Tasks;

    public class TestEventHandler: EventHandlerImpl
    {
        protected override void InitializeComponents() => this.AddEventListener<TestCityQuery>(this.Handler);

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
