namespace DotNetty.Codecs.DNS.Records
{
    public interface IDnsOptPseudoRecord : IDnsRecord
    {
        int ExtendedRcode { get; }
        int Version { get; }
        int Flags { get; }
    }
}
