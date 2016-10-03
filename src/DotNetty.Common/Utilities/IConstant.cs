// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    /// <summary>
    ///     A singleton which is safe to compare via the <c>==</c> operator. Created and managed by
    ///     <see cref="ConstantPool" />.
    /// </summary>
    public interface IConstant
    {
        /// <summary>Returns the unique number assigned to this <see cref="IConstant" />.</summary>
        int Id { get; }

        /// <summary>Returns the name of this <see cref="IConstant" />.</summary>
        string Name { get; }
    }
}