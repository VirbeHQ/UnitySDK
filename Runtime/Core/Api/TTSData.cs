namespace Virbe.Core
{
    public class TTSData
    {
        public TtsConnectionProtocol TtsConnectionProtocol { get; }
        public int AudioChannels { get; }
        public int AudioFrequency { get; }
        public int AudioSampleBits { get; }
        public string Path { get; }

        public TTSData(TtsConnectionProtocol ttsConnectionProtocol, int audioChannels, int audioFrequency, int audioSampleBits, string path)
        {
            TtsConnectionProtocol = ttsConnectionProtocol;
            AudioChannels = audioChannels;
            AudioFrequency = audioFrequency;
            AudioSampleBits = audioSampleBits;
            Path = path;    
        }
    }
}