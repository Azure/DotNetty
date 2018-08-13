namespace DotNetty.Common.Internal
{
    using System;
    using System.Threading;

    static class RefArrayAccessUtil
    {
        public static readonly int RefBufferPad = 64 * 2 / IntPtr.Size;

        /// <summary>
        /// A plain store (no ordering/fences) of an element to a given offset.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="buffer">The source buffer.</param>
        /// <param name="offset">Computed via <see cref="ConcurrentCircularArrayQueue{T}.CalcElementOffset"/></param>
        /// <param name="e">An orderly kitty.</param>
        public static void SpElement<T>(T[] buffer, long offset, T e) => buffer[offset] = e;

        /// <summary>
        /// An ordered store(store + StoreStore barrier) of an element to a given offset.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="buffer">The source buffer.</param>
        /// <param name="offset">Computed via <see cref="ConcurrentCircularArrayQueue{T}.CalcElementOffset"/></param>
        /// <param name="e"></param>
        public static void SoElement<T>(T[] buffer, long offset, T e) where T : class => Volatile.Write(ref buffer[offset], e);

        /// <summary>
        /// A plain load (no ordering/fences) of an element from a given offset.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="buffer">The source buffer.</param>
        /// <param name="offset">Computed via <see cref="ConcurrentCircularArrayQueue{T}.CalcElementOffset"/></param>
        /// <returns>The element at the given <paramref name="offset"/> in the given <paramref name="buffer"/>.</returns>
        public static T LpElement<T>(T[] buffer, long offset) => buffer[offset];

        /// <summary>
        /// A volatile load (load + LoadLoad barrier) of an element from a given offset.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="buffer">The source buffer.</param>
        /// <param name="offset">Computed via <see cref="ConcurrentCircularArrayQueue{T}.CalcElementOffset"/></param>
        /// <returns>The element at the given <paramref name="offset"/> in the given <paramref name="buffer"/>.</returns>
        public static T LvElement<T>(T[] buffer, long offset) where T : class => Volatile.Read(ref buffer[offset]);

        /// <summary>
        /// Gets the offset in bytes within the array for a given index.
        /// </summary>
        /// <param name="index">The desired element index.</param>
        /// <param name="mask">Mask for the index.</param>
        /// <returns>The offset (in bytes) within the array for a given index.</returns>
        public static long CalcElementOffset(long index, long mask) => RefBufferPad + (index & mask);
    }
}