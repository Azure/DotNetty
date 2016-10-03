// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    /// <summary>Holds <see cref="IAttribute{T}" />s which can be accessed via <see cref="AttributeKey{T}" />.</summary>
    /// <remarks>Implementations must be Thread-safe.</remarks>
    public interface IAttributeMap
    {
        /// <summary>
        ///     Get the <see cref="IAttribute{T}" /> for the given <see cref="AttributeKey{T}" />. This method will never return
        ///     null, but may return an <see cref="IAttribute{T}" /> which does not have a value set yet.
        /// </summary>
        IAttribute<T> GetAttribute<T>(AttributeKey<T> key)
            where T : class;

        /// <summary>
        ///     Returns <c>true</c> if and only if the given <see cref="IAttribute{T}" /> exists in this
        ///     <see cref="IAttributeMap" />.
        /// </summary>
        bool HasAttribute<T>(AttributeKey<T> key)
            where T : class;
    }
}