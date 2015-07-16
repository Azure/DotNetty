// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common
{
    /// <summary>
    /// Reference counting interface for reusable objects
    /// </summary>
    public interface IReferenceCounted
    {
        /// <summary>
        /// Returns the reference count of this object
        /// </summary>
        int ReferenceCount { get; }

        /// <summary>
        /// Increases the reference count by 1
        /// </summary>
        IReferenceCounted Retain();

        /// <summary>
        /// Increases the reference count by <see cref="increment"/>.
        /// </summary>
        IReferenceCounted Retain(int increment);

        /// <summary>
        /// Decreases the reference count by 1 and deallocates this object if the reference count reaches 0.
        /// </summary>
        /// <returns>true if and only if the reference count is 0 and this object has been deallocated</returns>
        bool Release();

        /// <summary>
        /// Decreases the reference count by <see cref="decrement"/> and deallocates this object if the reference count reaches 0.
        /// </summary>
        /// <returns>true if and only if the reference count is 0 and this object has been deallocated</returns>
        bool Release(int decrement);
    }
}