// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    using System.Diagnostics.Contracts;
    using System.Threading;

    sealed class SpscLinkedQueue<T> : BaseLinkedQueue<T>, ILinkedQueue<T>
        where T : class
    {
        public SpscLinkedQueue()
        {
            this.SpProducerNode(new LinkedQueueNode<T>());
            this.SpConsumerNode(this.ProducerNode);
            this.ConsumerNode.SoNext(null); // this ensures correct construction: StoreStore
        }

        public override bool Offer(T e)
        {
            Contract.Requires(e != null);

            var nextNode = new LinkedQueueNode<T>(e);
            LinkedQueueNode<T> producerNode = this.LpProducerNode();
            producerNode.SoNext(nextNode);
            this.SpProducerNode(nextNode);

            return true;
        }

        public override T Poll() => this.RelaxedPoll();

        public override T Peek() => this.RelaxedPeek();

        public override void Clear()
        {
            T value;
            do
            {
                value = this.Poll();
            }
            while (null != value);
        }
    }

    abstract class BaseLinkedQueue<T> : BaseLinkedQueueConsumerNodeRef<T>
        where T : class
    {
        protected T RelaxedPoll()
        {
            LinkedQueueNode<T> currConsumerNode = this.LpConsumerNode(); // don't load twice, it's alright
            LinkedQueueNode<T> nextNode = currConsumerNode.LvNext();
            return nextNode != null
                ? this.GetSingleConsumerNodeValue(currConsumerNode, nextNode)
                : null;
        }

        protected T RelaxedPeek()
        {
            LinkedQueueNode<T> currConsumerNode = this.ConsumerNode; // don't load twice, it's alright
            LinkedQueueNode<T> nextNode = currConsumerNode.LvNext();

            return nextNode?.LpValue();
        }

        protected T GetSingleConsumerNodeValue(LinkedQueueNode<T> currConsumerNode, LinkedQueueNode<T> nextNode)
        {
            // we have to null out the value because we are going to hang on to the node
            T nextValue = nextNode.GetAndNullValue();

            // Fix up the next ref of currConsumerNode to prevent promoted nodes from keep new ones alive.
            // We use a reference to self instead of null because null is already a meaningful value (the next of
            // producer node is null).
            currConsumerNode.SoNext(currConsumerNode);
            this.SpConsumerNode(nextNode);

            // currConsumerNode is now no longer referenced and can be collected
            return nextValue;
        }

        public sealed override int Count
        {
            get
            {
                // Read consumer first, this is important because if the producer is node is 'older' than the consumer
                // the consumer may overtake it (consume past it) invalidating the 'snapshot' notion of size.
                LinkedQueueNode<T> chaserNode = this.LvConsumerNode();
                LinkedQueueNode<T> producerNode = this.LvProducerNode();
                int size = 0;
                // must chase the nodes all the way to the producer node, but there's no need to count beyond expected head.
                while (chaserNode != producerNode && // don't go passed producer node
                    chaserNode != null && // stop at last node
                    size < int.MaxValue) // stop at max int
                {
                    LinkedQueueNode<T> next = chaserNode.LvNext();
                    // check if this node has been consumed, if so return what we have
                    if (next == chaserNode)
                    {
                        return size;
                    }
                    chaserNode = next;
                    size++;
                }
                return size;
            }
        }

        public sealed override bool IsEmpty => this.LvConsumerNode() == this.LvProducerNode();
    }

    abstract class BaseLinkedQueueConsumerNodeRef<T> : BaseLinkedQueuePad1<T>
        where T : class
    {
        protected LinkedQueueNode<T> ConsumerNode;

        protected void SpConsumerNode(LinkedQueueNode<T> node) => this.ConsumerNode = node;

        protected LinkedQueueNode<T> LvConsumerNode() => Volatile.Read(ref this.ConsumerNode);

        protected LinkedQueueNode<T> LpConsumerNode() => this.ConsumerNode;
    }

    abstract class BaseLinkedQueuePad1<T> : BaseLinkedQueueProducerNodeRef<T>
        where T : class
    {
#pragma warning disable 169 // padded reference
        long p01, p02, p03, p04, p05, p06, p07;
        long p10, p11, p12, p13, p14, p15, p16, p17;
#pragma warning restore 169
    }

    abstract class BaseLinkedQueueProducerNodeRef<T> : BaseLinkedQueuePad0<T>
        where T : class
    {
        protected LinkedQueueNode<T> ProducerNode;

        protected void SpProducerNode(LinkedQueueNode<T> node) => this.ProducerNode = node;

        protected LinkedQueueNode<T> LvProducerNode() => Volatile.Read(ref this.ProducerNode);

        protected LinkedQueueNode<T> LpProducerNode() => this.ProducerNode;
    }

    abstract class BaseLinkedQueuePad0<T>
        where T : class
    {
#pragma warning disable 169 // padded reference
        long p00, p01, p02, p03, p04, p05, p06, p07;
        long p10, p11, p12, p13, p14, p15, p16;
#pragma warning restore 169

        /// <summary>
        /// Called from a producer thread subject to the restrictions appropriate to the implementation and
        /// according to the <see cref="ILinkedQueue{T}.Offer"/> interface.
        /// </summary>
        /// <param name="e">The element to enqueue.</param>
        /// <returns><c>true</c> if the element was inserted, <c>false</c> iff the queue is full.</returns>
        public abstract bool Offer(T e);

        /// <summary>
        /// Called from the consumer thread subject to the restrictions appropriate to the implementation and
        /// according to the <see cref="ILinkedQueue{T}.Poll"/> interface.
        /// </summary>
        /// <returns>A message from the queue if one is available, <c>null</c> iff the queue is empty.</returns>
        public abstract T Poll();

        /// <summary>
        /// Called from the consumer thread subject to the restrictions appropriate to the implementation and
        /// according to the <see cref="ILinkedQueue{T}.Peek"/> interface.
        /// </summary>
        /// <returns>A message from the queue if one is available, <c>null</c> iff the queue is empty.</returns>
        public abstract T Peek();

        public abstract int Count { get; }

        public abstract bool IsEmpty { get; }

        public abstract void Clear();
    }

    sealed class LinkedQueueNode<T>
        where T : class
    {
        T value;
        LinkedQueueNode<T> next;

        public LinkedQueueNode() : this(null)
        { }

        public LinkedQueueNode(T value)
        {
            this.value = value;
        }

        public T GetAndNullValue()
        {
            T temp = this.LpValue();
            this.SpValue(null);
            return temp;
        }

        public T LpValue() => this.value;

        public void SpValue(T newValue) => this.value = newValue;

        public void SoNext(LinkedQueueNode<T> n) => Volatile.Write(ref this.next, n);

        public LinkedQueueNode<T> LvNext() => Volatile.Read(ref this.next);
    }

    public interface ILinkedQueue<T>
    {
        bool Offer(T e);

        T Poll();

        T Peek();
    }
}
