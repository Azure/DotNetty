namespace DotNetty.Rpc.Service
{

    public abstract class AbsMessage<T> : IMessage<T>
        where T : IResult
    {
        public object ReturnValue { get; set; }
    }

    public interface IMessage<T>: IMessage
        where T : IResult
    {
   
    }

    public interface IMessage
    {
        object ReturnValue { get; set; }
    }

    public interface IResult
    {

    }
}
