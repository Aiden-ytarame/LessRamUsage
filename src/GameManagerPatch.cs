using System.Collections;
using System.Collections.Generic;
using CielaSpike;
using HarmonyLib;
using UnityEngine;

namespace LessRam;

[HarmonyPatch(typeof(GameManager))]
public static class GameManagerPatch
{
    [HarmonyPatch(nameof(GameManager.LoadAudio))]
    [HarmonyPrefix] 
    public static bool PreLoadAudio(ref IEnumerator __result, VGLevel _level)
    {
        if (!GameManager.Inst.IsArcade || _level.LevelMusic)
        {
            return true;
        }
        
        __result = LoadAudio(ArcadeManager.Inst.CurrentArcadeLevel);
        return false;
    }
    
    static IEnumerator LoadAudio(VGLevel _level)
    {
        VGLevelWrapper? levelRealm = LessRam.Levels!.GetValueOrDefault(_level.name, null);
        
        yield return DataManager.inst.StartCoroutineAsync(LevelLoaderHelper.LoadAudio(_level, levelRealm!.AudioPath));
        yield return DataManager.inst.StartCoroutineAsync(LevelLoaderHelper.LoadImage(_level, levelRealm.ImagePath));
		
    
        GameManager.Inst.LevelAudio = _level.LevelMusic;
        GameManager.Inst.CurLoadingState.Audio = true;
    }

    [HarmonyPatch(nameof(GameManager.PlayGame))]
    [HarmonyPostfix]
    static void OnPlayGame()
    {
        ArcadeMenuPatch.CleanLevels();
    }
}