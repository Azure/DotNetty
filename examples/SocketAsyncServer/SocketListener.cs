using System;
using System.Net.Sockets;
using System.Threading;
using System.Net;

namespace SocketAsyncServer
{
    /// <summary>
    /// Implements the connection logic for the socket server.  
    /// After accepting a connection, all data read from the client is sent back. 
    /// The read and echo back to the client pattern is continued until the client disconnects.
    /// </summary>
    internal sealed class SocketListener
    {
        /// <summary>
        /// The socket used to listen for incoming connection requests.
        /// </summary>
        private Socket listenSocket;

        /// <summary>
        /// The total number of clients connected to the server.
        /// </summary>
        private int numConnectedSockets;

        /// <summary>
        /// Create an uninitialized server instance.  
        /// To start the server listening for connection requests
        /// call the Init method followed by Start method.
        /// </summary>
        internal SocketListener()
        {
            this.numConnectedSockets = 0;
        }

        /// <summary>
        /// Callback method associated with Socket.AcceptAsync 
        /// operations and is invoked when an accept operation is complete.
        /// </summary>
        /// <param name="sender">Object who raised the event.</param>
        /// <param name="e">SocketAsyncEventArg associated with the completed accept operation.</param>
        private void OnAcceptCompleted(object sender, SocketAsyncEventArgs e) => this.ProcessAccept(e);

        /// <summary>
        /// Process the accept for the socket listener.
        /// </summary>
        /// <param name="e">SocketAsyncEventArg associated with the completed accept operation.</param>
        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            Socket s = e.AcceptSocket;
            if (s.Connected)
            {
                try
                {
                    Interlocked.Increment(ref this.numConnectedSockets);
                    Console.WriteLine("Client connection accepted. There are {0} clients connected to the server", this.numConnectedSockets);

                    var socketChannel = new SocketChannel(s);
                    var readEventArgs = new SocketChannelAsyncOperation(socketChannel, true);
                    socketChannel.ProcessReceive(readEventArgs);
                }
                catch (SocketException ex)
                {
                    Console.WriteLine("Error when processing data received from {0}:\r\n{1}", s.RemoteEndPoint, ex.ToString());
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }

                // Accept the next connection request.
                this.StartAccept(e);
            }
        }

        /// <summary>
        /// Starts the server listening for incoming connection requests.
        /// </summary>
        /// <param name="port">Port where the server will listen for connection requests.</param>
        internal void Start(int port)
        {
            // Get endpoint for the listener.
            var localEndPoint = new IPEndPoint(IPAddress.Any, port);

            // Create the socket which listens for incoming connections.
            this.listenSocket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            this.listenSocket.Blocking = false;
            this.listenSocket.NoDelay = true;

            this.listenSocket.Bind(localEndPoint);

            // Start the server.
            this.listenSocket.Listen(1024);

            // Post accepts on the listening socket.
            this.StartAccept(null);
        }

        /// <summary>
        /// Begins an operation to accept a connection request from the client.
        /// </summary>
        /// <param name="acceptEventArg">The context object to use when issuing 
        /// the accept operation on the server's listening socket.</param>
        private void StartAccept(SocketAsyncEventArgs acceptEventArg)
        {
            if (acceptEventArg == null)
            {
                acceptEventArg = new SocketAsyncEventArgs();
                acceptEventArg.Completed += this.OnAcceptCompleted;
            }
            else
            {
                // Socket must be cleared since the context object is being reused.
                acceptEventArg.AcceptSocket = null;
            }

            if (!this.listenSocket.AcceptAsync(acceptEventArg))
            {
                this.ProcessAccept(acceptEventArg);
            }
        }

        /// <summary>
        /// Stop the server.
        /// </summary>
        internal void Stop() => this.listenSocket.Close();
    }
}
