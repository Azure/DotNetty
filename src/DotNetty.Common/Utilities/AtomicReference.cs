// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System.Threading;

    /// <summary>
    /// Implementation of the java.concurrent.util AtomicReference type.
    /// </summary>
    public sealed class AtomicReference<T>
        where T : class
    {
        // ReSharper disable once InconsistentNaming
        T atomicValue;

        /// <summary>
        /// Sets the initial value of this <see cref="AtomicReference{T}"/> to <see cref="originalValue"/>.
        /// </summary>
        public AtomicReference(T originalValue)
        {
            this.atomicValue = originalValue;
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public AtomicReference()
        {
            this.atomicValue = default(T);
        }

        /// <summary>
        /// The current value of this <see cref="AtomicReference{T}"/>
        /// </summary>
        public T Value
        {
            get { return Volatile.Read(ref this.atomicValue); }
            set { Volatile.Write(ref this.atomicValue, value); }
        }

        /// <summary>
        /// If <see cref="Value"/> equals <see cref="expected"/>, then set the Value to
        /// <see cref="newValue"/>.
        /// 
        /// Returns true if <see cref="newValue"/> was set, false otherwise.
        /// </summary>
        public T CompareAndSet(T expected, T newValue)
        {
            return Interlocked.CompareExchange(ref this.atomicValue, expected, newValue);
        }

        #region Conversion operators

        /// <summary>
        /// Implicit conversion operator = automatically casts the <see cref="AtomicReference{T}"/> to an instance of <typeparam name="T"></typeparam>
        /// </summary>
        public static implicit operator T(AtomicReference<T> aRef)
        {
            return aRef.Value;
        }

        /// <summary>
        /// Implicit conversion operator = allows us to cast any type directly into a <see cref="AtomicReference{T}"/> instance.
        /// </summary>
        /// <param name="newValue"></param>
        /// <returns></returns>
        public static implicit operator AtomicReference<T>(T newValue)
        {
            return new AtomicReference<T>(newValue);
        }

        #endregion
    }
}