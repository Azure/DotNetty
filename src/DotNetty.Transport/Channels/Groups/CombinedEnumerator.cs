using System;
using System.Collections.Generic;

namespace DotNetty.Transport.Channels.Groups
{
    public sealed class CombinedEnumerator<E> : IEnumerator<E> 
    {

        private readonly IEnumerator<E> e1;
        private readonly IEnumerator<E> e2;
        private IEnumerator<E> currentEnumerator;

        public CombinedEnumerator(IEnumerator<E> e1, IEnumerator<E> e2)
        {
            if (e1 == null)
                throw new ArgumentNullException("e1");
            if (e2 == null)
                throw new ArgumentNullException("e2");
            this.e1 = e1;
            this.e2 = e2;
            this.currentEnumerator = e1;
        }




        public E Current
        {
            get
            {
                for(;;)
                {
                    try
                    {
                        return currentEnumerator.Current;
                    }
                    catch(InvalidOperationException)
                    {
                        if (currentEnumerator == e1)
                            currentEnumerator = e2;
                        else
                            throw;
                    }
                }
            }
        }

        public void Dispose()
        {
            currentEnumerator.Dispose();
            e1.Dispose();
            e2.Dispose();
        }

        object System.Collections.IEnumerator.Current
        {
            get { throw new NotImplementedException(); }
        }

        public bool MoveNext()
        {
            for(;;)
            {
                if (currentEnumerator.MoveNext())
                    return true;
                if (currentEnumerator == e1)
                    currentEnumerator = e2;
                else
                    return false;
            }
        }

        public void Reset()
        {
            currentEnumerator.Reset();
        }
    }
}
