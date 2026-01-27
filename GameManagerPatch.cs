using System.Collections;
using System.Collections.Generic;
using CielaSpike;
using HarmonyLib;
using UnityEngine;

namespace LessRam;

[HarmonyPatch(typeof(GameManager))]
public static class GameManagerPatch
{
    private static int audioLoading = 0;
    
      [HarmonyPrefix]
      [HarmonyPatch(nameof(GameManager.LoadAudio), MethodType.Enumerator)]
    public static bool PreLoadAudio(ref bool __result)
    {
        if (!GameManager.Inst.IsArcade || ArcadeManager.Inst.CurrentArcadeLevel.LevelMusic)
        {
            return true;
        }
      
        if(audioLoading == 0)
            DataManager.inst.StartCoroutine(LoadAudio(ArcadeManager.Inst.CurrentArcadeLevel));
        
        if (audioLoading == 2)
            audioLoading = 0;
      
        __result = audioLoading == 1;
        return false;
    }
    
    static IEnumerator LoadAudio(VGLevel _level)
    {
        audioLoading = 1;

        VGLevelWrapper? levelRealm = LessRam.Levels!.GetValueOrDefault(_level.name, null);
        
        yield return DataManager.inst.StartCoroutineAsync(LevelLoaderHelper.LoadAudio(_level, levelRealm!.AudioPath));
        yield return DataManager.inst.StartCoroutineAsync(LevelLoaderHelper.LoadImage(_level, levelRealm.ImagePath));
		
    
        GameManager.Inst.LevelAudio = _level.LevelMusic;
        yield return new WaitForEndOfFrame();
        GameManager.Inst.CurLoadingState.Audio = true;
        audioLoading = 2;
    }

    [HarmonyPatch(nameof(GameManager.PlayGame))]
    [HarmonyPostfix]
    static void OnPlayGame()
    {
        ArcadeMenuPatch.CleanLevels();
    }
}