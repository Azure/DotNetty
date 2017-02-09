namespace DotNetty.Rpc.Service
{

    public abstract class AbsMessage<T> : IMessage<T>
        where T : IResult
    {
        public T ReturnValue { get; set; }
    }

    public interface IMessage<T>: IMessage
        where T : IResult
    {
        T ReturnValue { get; set; }
    }

    public interface IMessage
    {
      
    }

    public interface IResult
    {

    }
}
