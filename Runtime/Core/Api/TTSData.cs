namespace Virbe.Core
{
    public class TTSData
    {
        public ConnectionProtocol ConnectionProtocol { get; }
        public int AudioChannels { get; }
        public int AudioFrequency { get; }
        public int AudioSampleBits { get; }
        public string Path { get; }

        public TTSData(ConnectionProtocol ttsConnectionProtocol, int audioChannels, int audioFrequency, int audioSampleBits, string path)
        {
            ConnectionProtocol = ttsConnectionProtocol;
            AudioChannels = audioChannels;
            AudioFrequency = audioFrequency;
            AudioSampleBits = audioSampleBits;
            Path = path;    
        }
    }
}