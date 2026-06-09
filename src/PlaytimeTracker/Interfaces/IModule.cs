namespace InsanityGaming.PlaytimeTracker.Interfaces;

internal interface IModule
{
    bool Init();

    void PostInit() { }

    void Shutdown() { }

    void OnLibraryConnected(string name) { }

    void OnLibraryDisconnect(string name) { }

    void OnAllModulesLoaded() { }
}
