
namespace DotNetty.Common.Utilities
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Threading;

    public abstract class AbstractReferenceCounted : IReferenceCounted
    {
        int referenceCount = 1;

        public int ReferenceCount => this.referenceCount;

        public IReferenceCounted Retain() => this.RetainCore(1);

        public IReferenceCounted Retain(int increment)
        {
            Contract.Requires(increment > 0);

            return this.RetainCore(increment);
        }

        protected virtual IReferenceCounted RetainCore(int increment)
        {
            while (true)
            {
                int count = this.referenceCount;
                int nextCount = count + increment;

                // Ensure we not resurrect (which means the refCnt was 0) and also that we encountered an overflow.
                if (nextCount <= increment)
                {
                    throw new InvalidOperationException($"refCnt: {count}" + (increment > 0 ? $"increment: {increment}" : $"decrement: -{increment}"));
                }

                if (Interlocked.CompareExchange(ref this.referenceCount, nextCount, count) == count)
                {
                    break;
                }
            }

            return this;
        }

        public IReferenceCounted Touch() => this.Touch(null);

        public abstract IReferenceCounted Touch(object hint);

        public bool Release() => this.ReleaseCore(1);

        public bool Release(int decrement)
        {
            Contract.Requires(decrement > 0);

            return this.ReleaseCore(decrement);
        }

        bool ReleaseCore(int decrement)
        {
            while (true)
            {
                int count = this.referenceCount;
                if (count < decrement)
                {
                    throw new InvalidOperationException($"refCnt: {count}" + (decrement > 0 ? $"increment: {decrement}" : $"decrement: -{decrement}"));
                }

                if (Interlocked.CompareExchange(ref this.referenceCount, count - decrement, count) == decrement)
                {
                    this.Deallocate();
                    return  true;
                }

                return false;
            }
        }

        protected abstract void Deallocate();
    }
}
