namespace DotNetty.Handlers.Logging
{
    using DotNetty.Common.Internal.Logging;

    public sealed class LogLevel
    {
        public static readonly LogLevel TRACE = new LogLevel(InternalLogLevel.TRACE);
        public static readonly LogLevel DEBUG = new LogLevel(InternalLogLevel.DEBUG);
        public static readonly LogLevel INFO = new LogLevel(InternalLogLevel.INFO);
        public static readonly LogLevel WARN = new LogLevel(InternalLogLevel.WARN);
        public static readonly LogLevel ERROR = new LogLevel(InternalLogLevel.ERROR);

        private readonly InternalLogLevel internalLevel;

        private LogLevel()
        {
        }

        private LogLevel(InternalLogLevel internalLevel)
        {
            this.internalLevel = internalLevel;
        }

        public InternalLogLevel ToInternalLevel()
        {
            return internalLevel;
        }
    }
}
