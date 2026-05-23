using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace LessRam;

[BepInPlugin(Guid, Name, Version)]
[BepInDependency("me.ytarame.Multiplayer")]
public class LessRam : BaseUnityPlugin
{
    Harmony _harmony;
    const string Guid = "me.ytarame.LessRam";
    const string Name = "LessRam";
    const string Version = "1.0.2";

    internal static readonly Dictionary<string, VGLevelWrapper> Levels = new();
    internal new static ManualLogSource Logger;
    internal static int SemaphoreCount = 5; 
    private void Awake()
    {
        Logger = base.Logger;
        
        _harmony = new Harmony(Guid);
        _harmony.PatchAll();
        SemaphoreCount = Config.Bind(new ConfigDefinition("LessRam", "Semaphore count"), 5).Value;
        // Plugin startup logic
        Logger.LogInfo($"Plugin {Guid} is loaded!");
    }
}
