// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common
{
    /// <summary>
    ///     Reference counting interface for reusable objects
    /// </summary>
    public interface IReferenceCounted
    {
        /// <summary>
        ///     Returns the reference count of this object
        /// </summary>
        int ReferenceCount { get; }

        /// <summary>
        ///     Increases the reference count by 1
        /// </summary>
        IReferenceCounted Retain();

        /// <summary>
        ///     Increases the reference count by <paramref name="increment" />
        /// </summary>
        IReferenceCounted Retain(int increment);

        /// <summary>
        ///     Records the current access location of this object for debugging purposes.
        ///     If this object is determined to be leaked, the information recorded by this operation will be provided to you
        ///     via <see cref="ResourceLeakDetector" />. This method is a shortcut to <see cref="Touch(object)" /> with null as
        ///     an argument.
        /// </summary>
        /// <returns></returns>
        IReferenceCounted Touch();

        /// <summary>
        ///     Records the current access location of this object with an additonal arbitrary information for debugging
        ///     purposes. If this object is determined to be leaked, the information recorded by this operation will be
        ///     provided to you via <see cref="ResourceLeakDetector" />.
        /// </summary>
        IReferenceCounted Touch(object hint);

        /// <summary>
        ///     Decreases the reference count by 1 and deallocates this object if the reference count reaches 0.
        /// </summary>
        /// <returns>true if and only if the reference count is 0 and this object has been deallocated</returns>
        bool Release();

        /// <summary>
        ///     Decreases the reference count by <paramref name="decrement" /> and deallocates this object if the reference count
        ///     reaches 0.
        /// </summary>
        /// <returns>true if and only if the reference count is 0 and this object has been deallocated</returns>
        bool Release(int decrement);
    }
}