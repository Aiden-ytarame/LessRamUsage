using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Realms;

namespace LessRam;

[BepInPlugin(Guid, Name, Version)]
[BepInProcess("Project Arrhythmia.exe")]
[BepInDependency("me.ytarame.Multiplayer")]
public class LessRam : BaseUnityPlugin
{
    public static LessRam Inst;
    public Realm Realm;

    Harmony _harmony;
    const string Guid = "me.ytarame.LessRam";
    const string Name = "LessRam";
    const string Version = "1.0.0";

    internal new static ManualLogSource Logger;
    private void Awake()
    {
        Inst = this;
        Logger = base.Logger;
        
        _harmony = new Harmony(Guid);
        _harmony.PatchAll();

        var config = RealmConfiguration.DefaultConfiguration;
        Realm.DeleteRealm(config);
        Realm = Realm.GetInstance(config);

        // Plugin startup logic
        Logger.LogError($"Plugin {Guid} is loaded!");
    }
}
