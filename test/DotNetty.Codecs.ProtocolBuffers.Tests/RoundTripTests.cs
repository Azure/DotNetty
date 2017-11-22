// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.ProtocolBuffers.Tests
{
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Protobuf;
    using Xunit;
    using DotNetty.Codecs.ProtocolBuffers;
    using DotNetty.Transport.Channels.Embedded;
    using Google.ProtocolBuffers;

    public class RoundTripTests
    {
        static IEnumerable<object[]> GetAddressBookCases()
        {
            Person.Builder personBuilder = Person.CreateBuilder();
            personBuilder.SetId(1);
            personBuilder.SetName("Foo");
            personBuilder.SetEmail("foo@bar");

            Person.Types.PhoneNumber.Builder phoneBuilder = Person.Types.PhoneNumber.CreateBuilder();
            phoneBuilder.SetType(Person.Types.PhoneType.HOME);
            phoneBuilder.SetNumber("555-1212");
            Person.Types.PhoneNumber phone1 = phoneBuilder.Build();

            personBuilder.AddPhone(phone1);

            Person person1 = personBuilder.Build();

            AddressBook.Builder addressBuilder = AddressBook.CreateBuilder();
            addressBuilder.AddPerson(person1);

            yield return new object[]
            {
                addressBuilder.Build(),
                false
            };

            yield return new object[]
            {
                addressBuilder.Build(),
                true
            };

            phoneBuilder = Person.Types.PhoneNumber.CreateBuilder();
            phoneBuilder.SetType(Person.Types.PhoneType.MOBILE);
            phoneBuilder.SetNumber("+61 123456789");

            Person.Types.PhoneNumber phone2 = phoneBuilder.Build();
            personBuilder.AddPhone(phone1);
            personBuilder.AddPhone(phone2);

            addressBuilder = AddressBook.CreateBuilder();
            addressBuilder.AddPerson(person1);

            yield return new object[]
            {
                addressBuilder.Build(),
                false
            };

            yield return new object[]
            {
                addressBuilder.Build(),
                true
            };

            personBuilder = Person.CreateBuilder();
            personBuilder.SetId(2);
            personBuilder.SetName("姓名");
            personBuilder.SetEmail("foo.bar@net.com");

            personBuilder.AddPhone(phone2);
            personBuilder.AddPhone(phone1);

            Person person2 = personBuilder.Build();

            addressBuilder = AddressBook.CreateBuilder();
            addressBuilder.AddPerson(person1);
            addressBuilder.AddPerson(person2);

            yield return new object[]
            {
                addressBuilder.Build(),
                false
            };

            yield return new object[]
            {
                addressBuilder.Build(),
                true
            };
        }

        [Theory]
        [MemberData(nameof(GetAddressBookCases))]
        public void Run1(AddressBook addressBook, bool isCompositeBuffer)
        {
            AddressBook.Builder builder = AddressBook.CreateBuilder();
            IMessageLite protoType = builder.DefaultInstanceForType;

            var channel = new EmbeddedChannel(
                new ProtobufVarint32FrameDecoder(),
                new ProtobufDecoder(protoType, null),
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
                inputBuffer = Unpooled.WrappedBuffer(
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

            Assert.Equal(addressBook.PersonList.Count, roundTripped.PersonList.Count);
            for (int i = 0; i < addressBook.PersonList.Count; i++)
            {
                Assert.Equal(addressBook.PersonList[i].Id, roundTripped.PersonList[i].Id);
                Assert.Equal(addressBook.PersonList[i].Email, roundTripped.PersonList[i].Email);
                Assert.Equal(addressBook.PersonList[i].Name, roundTripped.PersonList[i].Name);

                Assert.Equal(addressBook.PersonList[i].PhoneList.Count, roundTripped.PersonList[i].PhoneList.Count);
                for (int j = 0; j < addressBook.PersonList[i].PhoneList.Count; j++)
                {
                    Assert.Equal(addressBook.PersonList[i].PhoneList[j].Type, 
                        roundTripped.PersonList[i].PhoneList[j].Type);
                    Assert.Equal(addressBook.PersonList[i].PhoneList[j].Number, 
                        roundTripped.PersonList[i].PhoneList[j].Number);
                }
            }

            Assert.False(channel.Finish());
        }

        [Theory]
        [InlineData(Person.Types.PhoneType.MOBILE, "+123 456 789", false)]
        [InlineData(Person.Types.PhoneType.MOBILE, "+123 456 789", true)]
        [InlineData(Person.Types.PhoneType.HOME, "", false)]
        [InlineData(Person.Types.PhoneType.HOME, "", true)]
        [InlineData(Person.Types.PhoneType.WORK, "+123-456+789", false)]
        [InlineData(Person.Types.PhoneType.WORK, "+123-456+789", true)]
        public void Run2(Person.Types.PhoneType phoneType, string number, bool isCompositeBuffer)
        {
            var builder = new Person.Types.PhoneNumber.Builder();
            builder.SetType(phoneType);
            builder.SetNumber(number);
            Person.Types.PhoneNumber phoneNumber = builder.Build();
            
            IMessageLite protoType = builder.DefaultInstanceForType;
            var channel = new EmbeddedChannel(
                new ProtobufVarint32FrameDecoder(),
                new ProtobufDecoder(protoType, null),
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
                inputBuffer = Unpooled.WrappedBuffer(
                    Unpooled.CopiedBuffer(data, 0, 2),
                    Unpooled.CopiedBuffer(data, 2, data.Length - 2));
            }
            else
            {
                inputBuffer = Unpooled.WrappedBuffer(data);
            }

            Assert.True(channel.WriteInbound(inputBuffer));

            var message = channel.ReadInbound<IMessageLite>();
            Assert.NotNull(message);
            Assert.IsType<Person.Types.PhoneNumber>(message);
            var roundTripped = (Person.Types.PhoneNumber)message;

            Assert.Equal(phoneNumber.Type, roundTripped.Type);
            Assert.Equal(phoneNumber.Number, roundTripped.Number);
        }
    }
}
