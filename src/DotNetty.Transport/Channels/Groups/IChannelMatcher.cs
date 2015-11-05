namespace DotNetty.Transport.Channels.Groups
{
    public interface IChannelMatcher
    {
        bool Matches(IChannel channel);
    }
}