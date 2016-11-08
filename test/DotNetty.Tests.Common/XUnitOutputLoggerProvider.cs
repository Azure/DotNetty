namespace DotNetty.Tests.Common
{
    using Microsoft.Extensions.Logging;
    using Xunit.Abstractions;

    sealed class XUnitOutputLoggerProvider : ILoggerProvider
    {
        readonly ITestOutputHelper output;

        public XUnitOutputLoggerProvider(ITestOutputHelper output)
        {
            this.output = output;
        }

        public void Dispose()
        {
        }

        public ILogger CreateLogger(string categoryName) => new XUnitOutputLogger(categoryName, this.output);
    }
}