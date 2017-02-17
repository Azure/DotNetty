namespace DotNetty.Codecs.DNS.Records
{
    public interface IDnsOptEcsRecord : IDnsOptPseudoRecord
    {
        int SourcePrefixLength { get; }
        int ScopePrefixLength { get; }
        byte[] Address { get; }
    }
}
