namespace Virbe.Core
{
    public class STTData
    {
        public ConnectionProtocol ConnectionProtocol { get; }
        public string Path { get; }

        public STTData(ConnectionProtocol connectionProtocol, string path)
        {
            ConnectionProtocol = connectionProtocol;
            Path = path;
        }
    }
}