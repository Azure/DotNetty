// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;

    /// <summary>
    ///     A pool of <see cref="IConstant" />s.
    /// </summary>
    public abstract class ConstantPool
    {
        readonly Dictionary<string, IConstant> constants = new Dictionary<string, IConstant>();

        int nextId = 1;

        /// <summary>Shortcut of <c>this.ValueOf(firstNameComponent.Name + "#" + secondNameComponent)</c>.</summary>
        public IConstant ValueOf<T>(Type firstNameComponent, string secondNameComponent)
        {
            Contract.Requires(firstNameComponent != null);
            Contract.Requires(secondNameComponent != null);

            return this.ValueOf<T>(firstNameComponent.Name + '#' + secondNameComponent);
        }

        /// <summary>
        ///     Returns the <see cref="IConstant" /> which is assigned to the specified <c>name</c>.
        ///     If there's no such <see cref="IConstant" />, a new one will be created and returned.
        ///     Once created, the subsequent calls with the same <c>name</c> will always return the previously created one
        ///     (i.e. singleton.)
        /// </summary>
        /// <param name="name">the name of the <see cref="IConstant" /></param>
        public IConstant ValueOf<T>(string name)
        {
            IConstant c;

            lock (this.constants)
            {
                if (this.constants.TryGetValue(name, out c))
                {
                    return c;
                }
                else
                {
                    c = this.NewInstance0<T>(name);
                }
            }

            return c;
        }

        /// <summary>Returns <c>true</c> if a <see cref="AttributeKey{T}" /> exists for the given <c>name</c>.</summary>
        public bool Exists(string name)
        {
            CheckNotNullAndNotEmpty(name);
            lock (this.constants)
            {
                return this.constants.ContainsKey(name);
            }
        }

        /// <summary>
        ///     Creates a new <see cref="IConstant" /> for the given <c>name</c> or fail with an
        ///     <see cref="ArgumentException" /> if a <see cref="IConstant" /> for the given <c>name</c> exists.
        /// </summary>
        public IConstant NewInstance<T>(string name)
        {
            if (this.Exists(name))
            {
                throw new ArgumentException($"'{name}' is already in use");
            }

            IConstant c = this.NewInstance0<T>(name);

            return c;
        }

        // Be careful that this dose not check whether the argument is null or empty.
        IConstant NewInstance0<T>(string name)
        {
            lock (this.constants)
            {
                IConstant c = this.NewConstant<T>(this.nextId, name);
                this.constants[name] = c;
                this.nextId++;
                return c;
            }
        }

        static void CheckNotNullAndNotEmpty(string name) => Contract.Requires(!string.IsNullOrEmpty(name));

        protected abstract IConstant NewConstant<T>(int id, string name);

        [Obsolete]
        public int NextId()
        {
            lock (this.constants)
            {
                int id = this.nextId;
                this.nextId++;
                return id;
            }
        }
    }
}