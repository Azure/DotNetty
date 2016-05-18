// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;

    /// <summary>
    ///     Key which can be used to access <seealso cref="Attribute" /> out of the <see cref="IAttributeMap" />. Be aware that
    ///     it is not be possible to have multiple keys with the same name.
    /// </summary>
    /// <typeparam name="T">
    ///     the type of the <see cref="Attribute" /> which can be accessed via this <see cref="AttributeKey{T}" />.
    /// </typeparam>
    public sealed class AttributeKey<T> : AbstractConstant<AttributeKey<T>>
    {
        public static readonly ConstantPool Pool = new AttributeConstantPool();

        sealed class AttributeConstantPool : ConstantPool
        {
            protected override IConstant NewConstant<TValue>(int id, string name) => new AttributeKey<TValue>(id, name);
        };

        /// <summary>Returns the singleton instance of the {@link AttributeKey} which has the specified <c>name</c>.</summary>
        public static AttributeKey<T> ValueOf(string name) => (AttributeKey<T>)Pool.ValueOf<T>(name);

        /// <summary>Returns <c>true</c> if a <see cref="AttributeKey{T}" /> exists for the given <c>name</c>.</summary>
        public static bool Exists(string name) => Pool.Exists(name);

        /// <summary>
        ///     Creates a new <see cref="AttributeKey{T}" /> for the given <c>name</c> or fail with an
        ///     <see cref="ArgumentException" /> if a <see cref="AttributeKey{T}" /> for the given <c>name</c> exists.
        /// </summary>
        public static AttributeKey<T> NewInstance(string name) => (AttributeKey<T>)Pool.NewInstance<T>(name);

        public static AttributeKey<T> ValueOf(Type firstNameComponent, string secondNameComponent)
            => (AttributeKey<T>)Pool.ValueOf<T>(firstNameComponent, secondNameComponent);

        internal AttributeKey(int id, string name)
            : base(id, name)
        {
        }
    }
}