namespace BusyBarOncallAgent.Mic;

public interface IMicrophoneRegistryReader
{
    IReadOnlyList<MicrophoneAppUsage> ReadAppUsages();
}
