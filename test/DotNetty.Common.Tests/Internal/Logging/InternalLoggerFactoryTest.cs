// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Tests.Internal.Logging
{
    using System;
    using System.Reactive.Disposables;
    using DotNetty.Common.Internal.Logging;
    using Moq;
    using Xunit;

    public class InternalLoggerFactoryTest
    {
        // todo: CodeContracts on CI
        //[Fact]
        //public void ShouldNotAllowNullDefaultFactory()
        //{
        //    Assert.ThrowsAny<Exception>(() => InternalLoggerFactory.DefaultFactory = null);
        //}

        [Fact]
        public void ShouldGetInstance()
        {
            IInternalLogger one = InternalLoggerFactory.GetInstance("helloWorld");
            IInternalLogger two = InternalLoggerFactory.GetInstance<string>();

            Assert.NotNull(one);
            Assert.NotNull(two);
            Assert.NotSame(one, two);
        }

        [Fact]
        public void TestMockReturned()
        {
            Mock<IInternalLogger> mock;
            using (SetupMockLogger(out mock))
            {
                mock.SetupGet(x => x.TraceEnabled).Returns(true).Verifiable();

                IInternalLogger logger = InternalLoggerFactory.GetInstance("mock");

                Assert.Equal(logger, mock.Object);
                Assert.True(logger.TraceEnabled);
                mock.Verify(x => x.TraceEnabled, Times.Once);
            }
        }

        static IDisposable SetupMockLogger(out Mock<IInternalLogger> loggerMock)
        {
            InternalLoggerFactory oldLoggerFactory = InternalLoggerFactory.DefaultFactory;
            var factoryMock = new Mock<InternalLoggerFactory>(MockBehavior.Strict);
            InternalLoggerFactory mockFactory = factoryMock.Object;
            loggerMock = new Mock<IInternalLogger>(MockBehavior.Strict);

            factoryMock.Setup(x => x.NewInstance("mock")).Returns(loggerMock.Object);
            InternalLoggerFactory.DefaultFactory = mockFactory;
            return Disposable.Create(() => InternalLoggerFactory.DefaultFactory = oldLoggerFactory);
        }
    }
}