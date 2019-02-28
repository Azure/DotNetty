// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;

    public sealed class Signal : Exception, IConstant, IComparable, IComparable<Signal>
    {
        static readonly SignalConstantPool Pool = new SignalConstantPool();

        sealed class SignalConstantPool : ConstantPool
        {
            protected override IConstant NewConstant<T>(int id, string name) => new Signal(id, name);
        };

        public static Signal ValueOf(string name) => (Signal)Pool.ValueOf<Signal>(name);

        public static Signal ValueOf(Type firstNameComponent, string secondNameComponent) => (Signal)Pool.ValueOf<Signal>(firstNameComponent, secondNameComponent);

        readonly SignalConstant constant;

        Signal(int id, string name)
        {
            this.constant = new SignalConstant(id, name);
        }

        public void Expect(Signal signal)
        {
            if (!ReferenceEquals(this, signal))
            {
                throw new InvalidOperationException($"unexpected signal: {signal}");
            }
        }

        public int Id => this.constant.Id;

        public string Name => this.constant.Name;

        public override bool Equals(object obj) => ReferenceEquals(this, obj);

        public override int GetHashCode() => this.Id;

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return 0;
            }
            if (!ReferenceEquals(obj, null) && obj is Signal)
            {
                return this.CompareTo((Signal)obj);
            }

            throw new Exception("failed to compare two different signal constants");
        }

        public int CompareTo(Signal other)
        {
            if (ReferenceEquals(this, other))
            {
                return 0;
            }

            return this.constant.CompareTo(other.constant);
        }

        public override string ToString() => this.Name;

        sealed class SignalConstant : AbstractConstant<SignalConstant>
        {
            public SignalConstant(int id, string name) : base(id, name)
            {
            }
        }
    }
}
