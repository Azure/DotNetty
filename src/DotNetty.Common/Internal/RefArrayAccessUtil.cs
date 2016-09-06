namespace DotNetty.Common.Internal
{
    using System;
    using System.Threading;

    static class RefArrayAccessUtil
    {
        public static readonly int RefBufferPad = 64 * 2 / IntPtr.Size;

        /// A plain store (no ordering/fences) of an element to a given offset
        /// @param buffer this.buffer
        /// @param offset computed via {@link ConcurrentCircularArrayQueue#calcElementOffset(long)}
        /// @param e an orderly kitty
        public static void SpElement<T>(T[] buffer, long offset, T e) => buffer[offset] = e;

        /// An ordered store(store + StoreStore barrier) of an element to a given offset
        /// @param buffer this.buffer
        /// @param offset computed via {@link ConcurrentCircularArrayQueue#calcElementOffset(long)}
        /// @param e an orderly kitty
        public static void SoElement<T>(T[] buffer, long offset, T e) where T : class => Volatile.Write(ref buffer[offset], e);

        /// A plain load (no ordering/fences) of an element from a given offset.
        /// @param buffer this.buffer
        /// @param offset computed via {@link ConcurrentCircularArrayQueue#calcElementOffset(long)}
        /// @return the element at the offset
        public static T LpElement<T>(T[] buffer, long offset) => buffer[offset];

        /// A volatile load (load + LoadLoad barrier) of an element from a given offset.
        /// @param buffer this.buffer
        /// @param offset computed via {@link ConcurrentCircularArrayQueue#calcElementOffset(long)}
        /// @return the element at the offset
        public static T LvElement<T>(T[] buffer, long offset) where T : class => Volatile.Read(ref buffer[offset]);

        /// @param index desirable element index
        /// @param mask
        /// @return the offset in bytes within the array for a given index.
        public static long CalcElementOffset(long index, long mask) => RefBufferPad + (index & mask);
    }
}