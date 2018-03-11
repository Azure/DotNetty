// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Tests.Internal.Logging
{
    using System;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Tests.Common;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    [CollectionDefinition(nameof(InternalLoggerFactoryTest), DisableParallelization = true)]
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
            Mock<ILogger> mock;
            using (SetupMockLogger(out mock))
            {
                mock.Setup(x => x.IsEnabled(LogLevel.Trace)).Returns(true).Verifiable();

                IInternalLogger logger = InternalLoggerFactory.GetInstance("mock");

                Assert.True(logger.TraceEnabled);
                mock.Verify(x => x.IsEnabled(LogLevel.Trace), Times.Once);
            }
        }

        static IDisposable SetupMockLogger(out Mock<ILogger> loggerMock)
        {
            ILoggerFactory oldLoggerFactory = InternalLoggerFactory.DefaultFactory;
            var loggerFactory = new LoggerFactory();
            var factoryMock = new Mock<ILoggerProvider>(MockBehavior.Strict);
            ILoggerProvider mockFactory = factoryMock.Object;
            loggerMock = new Mock<ILogger>(MockBehavior.Strict);
            loggerFactory.AddProvider(mockFactory);
            factoryMock.Setup(x => x.CreateLogger("mock")).Returns(loggerMock.Object);
            InternalLoggerFactory.DefaultFactory = loggerFactory;
            return new Disposable(() => InternalLoggerFactory.DefaultFactory = oldLoggerFactory);
        }
    }
}