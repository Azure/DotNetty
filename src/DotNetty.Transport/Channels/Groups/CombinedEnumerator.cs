using System.Collections.Generic;

namespace DotNetty.Transport.Channels.Groups
{
    using System.Diagnostics.Contracts;

    public sealed class CombinedEnumerator<E> : IEnumerator<E>
    {
        readonly IEnumerator<E> e1;
        readonly IEnumerator<E> e2;
        IEnumerator<E> currentEnumerator;

        public CombinedEnumerator(IEnumerator<E> e1, IEnumerator<E> e2)
        {
            Contract.Requires(e1 != null);
            Contract.Requires(e2 != null);
            this.e1 = e1;
            this.e2 = e2;
            this.currentEnumerator = e1;
        }

        public E Current
        {
            get { return this.currentEnumerator.Current; }
        }

        public void Dispose()
        {
            this.currentEnumerator.Dispose();
        }

        object System.Collections.IEnumerator.Current
        {
            get { return this.Current; }
        }

        public bool MoveNext()
        {
            for (;;)
            {
                if (this.currentEnumerator.MoveNext())
                {
                    return true;
                }
                if (this.currentEnumerator == this.e1)
                {
                    this.currentEnumerator = this.e2;
                }
                else
                {
                    return false;
                }
            }
        }

        public void Reset()
        {
            this.currentEnumerator.Reset();
        }
    }
}