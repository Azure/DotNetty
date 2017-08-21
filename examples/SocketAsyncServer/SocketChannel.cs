using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;

namespace SocketAsyncServer
{

    public sealed class StateActionWithContextTaskQueueNode 
    {
        readonly Action<object, object> action;
        readonly object context;
        readonly object state;

        public StateActionWithContextTaskQueueNode(Action<object, object> action, object context, object state)
        {
            this.action = action;
            this.context = context;
            this.state = state;
        }

        public void Run() => this.action(this.context, this.state);
    }

    public class EventLoop
    {
        private static readonly BlockingCollection<StateActionWithContextTaskQueueNode> Queue =
            new BlockingCollection<StateActionWithContextTaskQueueNode>();

        public EventLoop()
        {
            var thread = new Thread(this.Start) {IsBackground = true};


            thread.Start();

        }

        private void Start(object o)
        {
            try
            {
                StateActionWithContextTaskQueueNode action;
                while (Queue.TryTake(out action, -1))
                {
                    try
                    {
                        action.Run();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);           
            }
        }

        public void Append(StateActionWithContextTaskQueueNode task) => Queue.TryAdd(task);
    }

    /// <summary>
    /// SocketChannel
    /// </summary>
    public sealed class SocketChannel : IDisposable
    {
        private readonly Socket socket;

        private const int MaxSize = 99999;
        private const int PreReadSize = 1024;
        private byte[] array = new byte[MaxSize];

        private int writerIndex = 0;

        private readonly EventLoop _eventLoop = new EventLoop();

        /// <summary>
        /// Class constructor.
        /// </summary>
        /// <param name="socket">Socket to accept incoming data.</param>
        internal SocketChannel(Socket socket)
        {
            this.socket = socket;
        }

        /// <summary>
        /// Callback called whenever a receive or send operation is completed on a socket.
        /// </summary>
        /// <param name="sender">Object who raised the event.</param>
        /// <param name="e">SocketAsyncEventArg associated with the completed send/receive operation.</param>
        public static void IoCompletedCallback(object sender, SocketAsyncEventArgs e)
        {
            var operation = (SocketChannelAsyncOperation)e;
            // Determine which type of operation just completed and call the associated handler.
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    operation.Channel._eventLoop.Append(GetTaskQueueNode(operation));
                    break;
                default:
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send");
            }
        }

        /// <summary>
        /// GetTaskQueueNode
        /// </summary>
        /// <param name="operation"></param>
        /// <returns></returns>
        private static StateActionWithContextTaskQueueNode GetTaskQueueNode(SocketChannelAsyncOperation operation)
        {
            return new StateActionWithContextTaskQueueNode(OnReadCompletedSync, operation.Channel, operation);
        }

        static void OnReadCompletedSync(object u, object e) => ((SocketChannel)u).ProcessReceive((SocketChannelAsyncOperation)e);

        /// <summary>
        /// This method is invoked when an asynchronous receive operation completes. 
        /// If the remote host closed the connection, then the socket is closed.  
        /// If data was received then the data is echoed back to the client.
        /// </summary>
        /// <param name="operation"></param>
        public void ProcessReceive(SocketChannelAsyncOperation operation)
        {
            operation.Validate();
            int flag = 0;
            try
            {         
                while (true)
                {
                    SocketError errorCode;
                    if (this.writerIndex + PreReadSize > MaxSize)
                    {
                        this.array = new byte[MaxSize];
                        this.writerIndex = 0;
                    }

                    int received = this.socket.Receive(this.array, this.writerIndex, PreReadSize, SocketFlags.None, out errorCode);
                    if (errorCode == SocketError.Success)
                    {
                        if (received == 0)
                        {
                            flag = -1;
                            break;
                        }
                    }
                    else if (errorCode == SocketError.WouldBlock)
                    {
                        if (received == 0)
                        {
                            break;
                        }
                    }
                    else
                    {
                        throw new SocketException((int)errorCode);
                    }

                    this.writerIndex += received;
                }
            }
            finally
            {
                if (flag == -1)
                {
                    operation.Channel.socket.Close();
                }
                else
                {
                    this.ScheduleSocketRead();
                }
            }
        }

        private void ScheduleSocketRead()
        {
            var operation = new SocketChannelAsyncOperation(this, true);
            bool pending = operation.Channel.socket.ReceiveAsync(operation);
            if (!pending)
            {
                operation.Channel.ProcessReceive(operation);
            }
        }

        /// <summary>
        /// Release instance.
        /// </summary>
        public void Dispose()
        {
            try
            {
                this.socket.Shutdown(SocketShutdown.Send);
            }
            catch (Exception)
            {
                // Throw if client has closed, so it is not necessary to catch.
            }
            finally
            {
                this.socket.Close();
            }
        }
    }
}
