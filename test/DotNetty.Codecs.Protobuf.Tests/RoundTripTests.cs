// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Protobuf.Tests
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels.Embedded;
    using Google.Protobuf;
    using Google.Protobuf.Examples.AddressBook;
    using Xunit;

    public class RoundTripTests
    {
        static IEnumerable<object[]> GetAddressBookCases()
        {
            var person = new Person
            {
                Id = 1,
                Name = "Foo",
                Email = "foo@bar",
                Phones =
                {
                    new Person.Types.PhoneNumber
                    {
                        Type = Person.Types.PhoneType.Home,
                        Number = "555-1212"
                    }
                }
            };

            yield return new object[]
            {
                new AddressBook
                {
                    People =
                    {
                        person
                    }
                },
                false
            };

            yield return new object[]
            {
                new AddressBook
                {
                    People =
                    {
                        person
                    }
                },
                true
            };

            person = new Person
            {
                Id = 1,
                Name = "Foo",
                Email = "foo@bar",
                Phones =
                {
                    new Person.Types.PhoneNumber
                    {
                        Type = Person.Types.PhoneType.Home,
                        Number = "555-1212"
                    },
                    new Person.Types.PhoneNumber
                    {
                        Type = Person.Types.PhoneType.Mobile,
                        Number = "+61 123456789"
                    }
                }
            };

            yield return new object[]
            {
                new AddressBook
                {
                    People =
                    {
                        person
                    }
                },
                false
            };

            yield return new object[]
            {
                new AddressBook
                {
                    People =
                    {
                        person
                    }
                },
                true
            };

            var person1 = new Person
            {
                Id = 2,
                Name = "姓名",
                Email = "foo.bar@net.com",
                Phones =
                {
                    new Person.Types.PhoneNumber
                    {
                        Type = Person.Types.PhoneType.Mobile,
                        Number = "+61 123456789"
                    }
                }
            };

            yield return new object[]
            {
                new AddressBook
                {
                    People =
                    {
                        person,
                        person1
                    }
                },
                false
            };

            yield return new object[]
            {
                new AddressBook
                {
                    People =
                    {
                        person,
                        person1
                    }
                },
                true
            };


            person1 = new Person
            {
                Id = 2,
                Name = "姓名",
                Email = "foo.bar@net.com",
                Phones =
                {
                    new Person.Types.PhoneNumber
                    {
                        Type = Person.Types.PhoneType.Mobile,
                        Number = "+61 123456789"
                    },
                    new Person.Types.PhoneNumber
                    {
                        Type = Person.Types.PhoneType.Home,
                        Number = "555-1212"
                    },
                }
            };

            yield return new object[]
            {
                new AddressBook
                {
                    People =
                    {
                        person,
                        person1
                    }
                },
                false
            };

            yield return new object[]
            {
                new AddressBook
                {
                    People =
                    {
                        person,
                        person1
                    }
                },
                true
            };
        }

        [Theory]
        [MemberData(nameof(GetAddressBookCases))]
        public void Run1(AddressBook addressBook, bool isCompositeBuffer)
        {
            var channel = new EmbeddedChannel(
                new ProtobufVarint32FrameDecoder(),
                new ProtobufDecoder(AddressBook.Parser),
                new ProtobufVarint32LengthFieldPrepender(),
                new ProtobufEncoder());

            Assert.True(channel.WriteOutbound(addressBook));
            var buffer = channel.ReadOutbound<IByteBuffer>();
            Assert.NotNull(buffer);
            Assert.True(buffer.ReadableBytes > 0);

            var data = new byte[buffer.ReadableBytes];
            buffer.ReadBytes(data);

            IByteBuffer inputBuffer;
            if (isCompositeBuffer)
            {
                inputBuffer = new CompositeByteBuffer(UnpooledByteBufferAllocator.Default, 2,
                    Unpooled.CopiedBuffer(data, 0, 2),
                    Unpooled.CopiedBuffer(data, 2, data.Length - 2));
            }
            else
            {
                inputBuffer = Unpooled.WrappedBuffer(data);
            }

            Assert.True(channel.WriteInbound(inputBuffer));

            var message = channel.ReadInbound<IMessage>();
            Assert.NotNull(message);
            Assert.IsType<AddressBook>(message);
            var roundTripped = (AddressBook)message;

            Assert.Equal(addressBook.People.Count, roundTripped.People.Count);
            for (int i = 0; i < addressBook.People.Count; i++)
            {
                Assert.Equal(addressBook.People[i].Id, roundTripped.People[i].Id);
                Assert.Equal(addressBook.People[i].Email, roundTripped.People[i].Email);
                Assert.Equal(addressBook.People[i].Name, roundTripped.People[i].Name);

                Assert.Equal(addressBook.People[i].Phones.Count, roundTripped.People[i].Phones.Count);
                for (int j = 0; j < addressBook.People[i].Phones.Count; j++)
                {
                    Assert.Equal(addressBook.People[i].Phones[j].Type , roundTripped.People[i].Phones[j].Type);
                    Assert.Equal(addressBook.People[i].Phones[j].Number, roundTripped.People[i].Phones[j].Number);
                }
            }

            Assert.False(channel.Finish());
        }

        [Theory]
        [InlineData(Person.Types.PhoneType.Mobile, "+123 456 789", false)]
        [InlineData(Person.Types.PhoneType.Mobile, "+123 456 789", true)]
        [InlineData(Person.Types.PhoneType.Home, "", false)]
        [InlineData(Person.Types.PhoneType.Home, "", true)]
        [InlineData(Person.Types.PhoneType.Work, "+123-456+789", false)]
        [InlineData(Person.Types.PhoneType.Work, "+123-456+789", true)]
        public void Run2(Person.Types.PhoneType phoneType, string number, bool isCompositeBuffer)
        {
            var phoneNumber = new Person.Types.PhoneNumber
            {
                Type = phoneType,
                Number = number
            };

            var channel = new EmbeddedChannel(
                new ProtobufVarint32FrameDecoder(),
                new ProtobufDecoder(Person.Types.PhoneNumber.Parser),
                new ProtobufVarint32LengthFieldPrepender(),
                new ProtobufEncoder());

            Assert.True(channel.WriteOutbound(phoneNumber));
            var buffer = channel.ReadOutbound<IByteBuffer>();
            Assert.NotNull(buffer);
            Assert.True(buffer.ReadableBytes > 0);

            var data = new byte[buffer.ReadableBytes];
            buffer.ReadBytes(data);

            IByteBuffer inputBuffer;
            if (isCompositeBuffer)
            {
                inputBuffer = new CompositeByteBuffer(UnpooledByteBufferAllocator.Default, 2,
                    Unpooled.CopiedBuffer(data, 0, 2),
                    Unpooled.CopiedBuffer(data, 2, data.Length - 2));
            }
            else
            {
                inputBuffer = Unpooled.WrappedBuffer(data);
            }

            Assert.True(channel.WriteInbound(inputBuffer));

            var message = channel.ReadInbound<IMessage>();
            Assert.NotNull(message);
            Assert.IsType<Person.Types.PhoneNumber>(message);
            var roundTripped = (Person.Types.PhoneNumber)message;

            Assert.Equal(phoneNumber.Type, roundTripped.Type);
            Assert.Equal(phoneNumber.Number, roundTripped.Number);

            Assert.False(channel.Finish());
        }
    }
}
