namespace DotNetty.Rpc.Service
{
    using System.Threading.Tasks;

    public delegate Task<TEvent> Listener<TEvent>(TEvent eventData) where TEvent : IMessage;

    public interface IModule
    {
        void Initialize();
    }

    public abstract class AbstractModule : IModule
    {
        private readonly object mutex = new object();
        private string name;

        public bool Initialized { get; protected set; }

        public string Name
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this.name))
                    this.name = this.GetType().FullName;

                return this.name;
            }
            set { this.name = value; }
        }

        public virtual void Initialize()
        {
            if (this.Initialized)
                return;

            lock (this.mutex)
            {
                if (this.Initialized)
                    return;

                this.InitializeComponents();

                this.Initialized = true;
            }
        }

        protected abstract void InitializeComponents();

    }

    public abstract class EventHandlerImpl : AbstractModule
    {
        protected override void InitializeComponents()
        {

        }

        public void AddEventListener<TEvent>(Listener<TEvent> handler) where TEvent : IMessage => ServiceBus.Instance.Subscribe(handler);
    }
}
