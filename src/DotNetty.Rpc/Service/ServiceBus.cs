
namespace DotNetty.Rpc.Service
{
    using System.Collections.Concurrent;
    using System;
    using System.Threading.Tasks;

    internal class ServiceBus
    {
        static readonly ServiceBus Instance0 = new ServiceBus();

        internal static ServiceBus Instance
        {
            get { return Instance0; }
        }

        internal ConcurrentDictionary<Type, Delegate> Handlers { get; private set; } = new ConcurrentDictionary<Type, Delegate>();

        internal void Subscribe<T>(Listener<T> subscriber) where T : IMessage
        {
            Delegate handler;
            Type type = typeof(T);
            if (this.Handlers.TryGetValue(type, out handler))
            {
                throw new Exception("Mutil Listener For this IMessage");
            }
            this.Handlers.TryAdd(typeof(T), subscriber);
        }

        internal async Task<object> Publish(object eventArgs)
        {
            Delegate handler;
            Type type = eventArgs.GetType();
            if (this.Handlers.TryGetValue(type, out handler))
            {
                await (Task)handler.DynamicInvoke(eventArgs);
                return ((IMessage)eventArgs).ReturnValue;
            }
            throw new NotImplementedException(type.Name);
        }
    }
}
