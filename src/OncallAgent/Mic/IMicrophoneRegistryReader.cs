namespace OncallAgent.Mic;

public interface IMicrophoneRegistryReader
{
    IReadOnlyList<MicrophoneAppUsage> ReadAppUsages();
}
