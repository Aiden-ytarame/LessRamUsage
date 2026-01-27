using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace LessRam;

[BepInPlugin(Guid, Name, Version)]
[BepInProcess("Project Arrhythmia.exe")]
public class LessRam : BaseUnityPlugin
{
    Harmony _harmony;
    const string Guid = "me.ytarame.LessRam";
    const string Name = "LessRam";
    const string Version = "1.0.0";

    internal static readonly Dictionary<string, VGLevelWrapper> Levels = new();
    internal new static ManualLogSource Logger;
    private void Awake()
    {
        Logger = base.Logger;
        
        _harmony = new Harmony(Guid);
        _harmony.PatchAll();
        
        // Plugin startup logic
        Logger.LogError($"Plugin {Guid} is loaded!");
    }
}
