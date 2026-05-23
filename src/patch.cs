using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using IEVO.UI.uGUIDirectedNavigation;
using CielaSpike;
using Steamworks.Ugc;
using UnityEngine;
using VGFunctions;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;
using Task = System.Threading.Tasks.Task;

namespace LessRam;

[HarmonyPatch(typeof(SteamWorkshopFacepunch))]
public static class SteamWorkshopPatch
{
	private static object _lockObject = new();
	
    [HarmonyPatch(nameof(SteamWorkshopFacepunch.DownloadLevels))]
    [HarmonyPrefix]
    public static bool PreStart(SteamWorkshopFacepunch __instance, ref Task __result)
    {
	    __result = LoadLevels(__instance);
        return false;
    }

    static async Task LoadLevels(SteamWorkshopFacepunch facepunch)
    {
	    Stopwatch stopWatch = new Stopwatch();
	    stopWatch.Start();
	    
	    facepunch.TotalSteamWorkshopSubscriptions = 0;
	    facepunch.TotalSteamWorkshopSubscriptionsDone = 0;
	    SteamWorkshopFacepunch.inst.isLoadingLevels = true;
	    
	    Query q = Query.ItemsReadyToUse.WhereUserSubscribed().SortByCreationDate();
	    
	
	    ResultPage? resultPage = await q.GetPageAsync(1);
	    if (!resultPage.HasValue)
	    {
		    stopWatch.Stop();
		    LessRam.Logger.LogError($"Level loading failure [{stopWatch.ElapsedMilliseconds}]");
		    return;
	    }
	    
	    facepunch.TotalSteamWorkshopSubscriptions = resultPage.Value.TotalCount;

	    foreach (Item entry in resultPage.Value.Entries)
	    {
		    if (SingletonBase<ArcadeManager>.Inst.skippedLoad)
		    {
			    return;
		    }

		    if (entry.IsInstalled)
		    {
			    var level = CreateEntry(facepunch, entry);
			    if (level.HasValue)
			    {
				    LessRam.Levels.Add(level.Value.Item1.name, level.Value.Item2);
				    ArcadeLevelDataManager.Inst.ArcadeLevels.Add(level.Value.Item1);
				    facepunch.TotalSteamWorkshopSubscriptionsDone++;
			    }
		    }
	    }
        
	    int totalPages = Mathf.CeilToInt((float)resultPage.Value.TotalCount / resultPage.Value.ResultCount);

	    using SemaphoreSlim semaphore = new SemaphoreSlim(LessRam.SemaphoreCount);
	    
	    List<Task> tasks = new();
	   
	    for (int i = 2; i < totalPages; i++)
	    {
		    await semaphore.WaitAsync();
		    LessRam.Logger.LogInfo($"loading page {i}");
		    int iteration = i;
		    tasks.Add(Task.Run(async () =>
		    {
			    ResultPage? page;
			    try
			    {
				    page = await q.GetPageAsync(iteration);
				    
				    if (!page.HasValue)
				    {
					    LessRam.Logger.LogError(
						    $"{iteration} no value"); //sometimes this log looks weird, but it should never happen anyway
					    return;
				    }

				    List<(VGLevel, VGLevelWrapper)> levels = new(page.Value.ResultCount);
				    foreach (Item entry in page.Value.Entries)
				    {
					    if (SingletonBase<ArcadeManager>.Inst.skippedLoad)
					    {
						    return;
					    }

					    if (entry.IsInstalled)
					    {
						    var level = CreateEntry(facepunch, entry);
						    if (level.HasValue)
						    {
							    levels.Add(level.Value);
						    }
					    }
				    }
				    
				    lock (_lockObject)
				    {
					    foreach (var vgLevel in levels)
					    {
						    LessRam.Levels.Add(vgLevel.Item1.name, vgLevel.Item2);
						    ArcadeLevelDataManager.Inst.ArcadeLevels.Add(vgLevel.Item1);
						    facepunch.TotalSteamWorkshopSubscriptionsDone++;
					    }
				    }
				    
			    }
			    finally
			    {
				    semaphore.Release();
			    }
		    }));
	    }

	    await Task.WhenAll(tasks);
	    stopWatch.Stop();
	    
	    SteamWorkshopFacepunch.inst.isLoadingLevels = false;
	    LessRam.Logger.LogInfo($"Time to load levels [{stopWatch.ElapsedMilliseconds}ms]");
    }

    static (VGLevel,VGLevelWrapper)? CreateEntry(SteamWorkshopFacepunch facepunch, Item entry)
    {
	    VGLevelWrapper? level = MakeLevelRealmObject(entry.Id.ToString(), entry.Directory);

	    if (level == null)
	    {
		    return null;
	    }

	    VGLevel vGLevel = ScriptableObject.CreateInstance<VGLevel>();
	    if (vGLevel.InitArcadeData(entry.Directory) && vGLevel.InitSteamInfoFix(entry.Id, entry.Directory))
	    {
		    vGLevel.name = entry.Id.ToString();
		    return (vGLevel, level);
	    }

	    return null;
    }

    static VGLevelWrapper? MakeLevelRealmObject(string id, string directory)
    {
	    if (string.IsNullOrEmpty(directory))
	    {
		    return null;
	    }

	    VGLevelWrapper level = new()
	    {
		    LevelPath = directory
	    };

	    DataManager.FileTypeDefinition imgTypeInfo = DataManager.inst.GetFileTypeInfo(DataManager.FileType.Level_Image);
	    string imagePath = directory + "\\" + imgTypeInfo.CurrentFile;
	    if (!LSFile.FileExists(imagePath))
	    {
		    imagePath = directory + "\\" + imgTypeInfo.LegacyFile;
	    }
	    level.ImagePath = imagePath;
	    
	    DataManager.FileTypeDefinition audioTypeInfo = DataManager.inst.GetFileTypeInfo(DataManager.FileType.Level_Audio);
	    string audioPath = directory + "\\" + audioTypeInfo.CurrentFile;
	    if (!LSFile.FileExists(audioPath))
	    {
		    audioPath = directory + "\\" + audioTypeInfo.LegacyFile;
	    }
	    level.AudioPath = audioPath;
	    return level;
    }
}

[HarmonyPatch(typeof(ArcadeMenu))]
public static class ArcadeMenuPatch
{
	private static List<Coroutine> activeCoroutines = new();
	public readonly static List<Sprite> activeSprites = new();
	public readonly static List<AudioClip> activeClips = new();
	
	[HarmonyPatch(nameof(ArcadeMenu.SelectPage), typeof(int), typeof(bool))]
	[HarmonyPrefix]
	static bool PreSelectPage(int _page, bool _forceButton, ArcadeMenu __instance)
	{
		__instance.StartCoroutine(SelectPage(_page, _forceButton, __instance));
		return false;
	}

	public static void CleanLevels()
	{
		foreach (var activeCoroutine in activeCoroutines)
		{
			if(activeCoroutine != null)
				ArcadeLevelDataManager.Inst.StopCoroutine(activeCoroutine);
		}
		activeCoroutines.Clear();
		
		for (var i = 0; i < activeClips.Count;)
		{

			if (activeClips[i])
			{
				if (AudioManager.Inst.CurrentAudioClip != activeClips[i] && (!ArcadeManager.Inst.CurrentArcadeLevel ||  ArcadeManager.Inst.CurrentArcadeLevel.LevelMusic != activeClips[i]))
				{
					Object.Destroy(activeClips[i]);
					activeClips.RemoveAt(i);
					continue;
				}
			}

			i++;
		}

		for (var i = 0; i < activeSprites.Count;)
		{
			if (activeSprites[i] && (!ArcadeManager.Inst.CurrentArcadeLevel ||  ArcadeManager.Inst.CurrentArcadeLevel.AlbumArt != activeSprites[i]))
			{
				Object.Destroy(activeSprites[i].texture);
				Object.Destroy(activeSprites[i]);
				activeSprites.RemoveAt(i);
				continue;
			}
			i++;
		}
	}
	static IEnumerator SelectPage(int _page, bool _forceButton, ArcadeMenu arcadeMenu)
	{
		CleanLevels();
		
		if (arcadeMenu.Page != _page)
		{
			SingletonBase<AudioManager>.Inst.PlaySound("PageSwap", 1);
		}

		if (arcadeMenu.SearchedLevels.Count <= 0)
		{
			if (arcadeMenu.SearchedLevels.Count > 0) //todo
			{
				arcadeMenu.ShowNoResults();
			}
			else
			{
				arcadeMenu.ShowNoLevels();
			}
		}
		else
		{
			if (_page < 0 || (arcadeMenu.SearchedLevels.Count > 0 && _page * 12 >= arcadeMenu.SearchedLevels.Count))
			{
				yield break;
			}

			if (arcadeMenu.NoResults.IsVisible)
			{
				arcadeMenu.NoResults.Hide();
			}

			if (arcadeMenu.NoLevels.IsVisible)
			{
				arcadeMenu.NoLevels.Hide();
			}

			if (arcadeMenu.KeyboardButtons[0].UIButton.IsVisible)
			{
				arcadeMenu.HideAllKeyboardButtons();
			}

			arcadeMenu.PageIsChanging = true;
			arcadeMenu.StartCoroutine(arcadeMenu.DelayAnim(_forceButton));
			arcadeMenu.Page = _page;

			int startIndex = _page * 12;
			int endIndex = Mathf.Clamp(startIndex + 12, 0, arcadeMenu.SearchedLevels.Count - 1) - startIndex;

			arcadeMenu.PageSlider.UpdateValueAndRange(_page,
				new Vector2(0f, Mathf.Ceil((arcadeMenu.SearchedLevels.Count - 1) / 12)),
				new Vector2(0f, 9f));
			arcadeMenu.PageSlider.UpdateValue(false);

			arcadeMenu.PageSlider.LeftButton.interactable = !arcadeMenu.IsFirstPage();
			arcadeMenu.PageSlider.RightButton.interactable = !arcadeMenu.IsLastPage();

			arcadeMenu.LeftPageTrigger.gameObject.SetActive(true);
			arcadeMenu.RightPageTrigger.gameObject.SetActive(true);

			arcadeMenu.LeftPageTrigger.interactable = !arcadeMenu.IsFirstPage();
			arcadeMenu.RightPageTrigger.interactable = !arcadeMenu.IsLastPage();

			//if (!arcadeMenu.MenuBackButton.IsVisible)
			{
				//arcadeMenu.MenuBackButton.Show();
			}

			int buttonIndex = 0;
			foreach (ArcadeHelpers.ArcadeButtonRefs button in arcadeMenu.LevelButtons)
			{
				if (buttonIndex > endIndex)
				{
					button.Button.ClearActions();
					button.Button.LockButtonState(true);
					button.Button.onClick.RemoveAllListeners();
					if (button.LevelButton.IsVisible)
					{
						LSHelpers.Delay(Random.Range(0f, 0.025f), new Action(() => button.LevelButton.Hide()));
					}
				}
				else
				{
					activeCoroutines.Add(ArcadeLevelDataManager.Inst.StartCoroutine(CreateButton(button, arcadeMenu.SearchedLevels[startIndex + buttonIndex], arcadeMenu)));
				}

				buttonIndex++;
			}
		}
	}

	static IEnumerator CreateButton(ArcadeHelpers.ArcadeButtonRefs button, VGLevel level, ArcadeMenu arcadeMenu)
	{
		VGLevelWrapper? levelRealm = LessRam.Levels.GetValueOrDefault(level.name, null);
	
		if (levelRealm != null)
		{
			arcadeMenu.StartCoroutineAsync(LevelLoaderHelper.LoadAudio(level, levelRealm!.AudioPath), out var task1);
			arcadeMenu.StartCoroutineAsync(LevelLoaderHelper.LoadImage(level, levelRealm!.ImagePath), out var task2);

			yield return task1.Wait();
			yield return task2.Wait();
		}

		DataManager.DifficultySetting difficulty = arcadeMenu.GetDifficulty(level.Difficulty);

		button.Button.GetComponent<DirectedNavigation>().Active = false;
		button.Button.LockButtonState(true);
		button.Button.onClick.RemoveAllListeners();
		button.Button.ClearActions();

		button.Button.OnSelectButton += () =>
		{
			arcadeMenu.LastSelectedButtonOffset = 0;
			arcadeMenu.LastSelectedButton = button.Button.gameObject;
			arcadeMenu.selectedLevelGO = button.Button.gameObject;
			arcadeMenu._songPreviewDebounce.Run(new Action(() => arcadeMenu.PlaySongPreview(level)),
				0.6f, arcadeMenu);
		};

		button.Button.OnHoverEnterButton += () =>
		{
			arcadeMenu.LastSelectedButtonOffset = 0;
			arcadeMenu.LastSelectedButton = button.Button.gameObject;
			arcadeMenu.selectedLevelGO = button.Button.gameObject;
			arcadeMenu._songPreviewDebounce.Run(new Action(() => arcadeMenu.PlaySongPreview(level)),
				0.3f, arcadeMenu);
		};

		button.Button.onClick.AddListener(() =>
		{
			arcadeMenu.LastSelectedButtonOffset = 0;
			arcadeMenu.LastSelectedButton = button.Button.gameObject;
			arcadeMenu.selectedLevelGO = button.Button.gameObject;
			if (AudioManager.Inst.currentSongGroup == level.TrackName)
			{
				arcadeMenu._songPreviewDebounce.ResetTime(arcadeMenu);
			}

			arcadeMenu.SetupSongMenu(level);
			arcadeMenu.LeftPageTrigger.interactable = false;
			arcadeMenu.RightPageTrigger.interactable = false;
			//VGPlayerManager.Inst.LastSelection = arcadeMenu.KeyboardButtons;
			//arcadeMenu.ViewManager.Pages[1].SetSelection(arcadeMenu.BackToSongListButton);
			arcadeMenu.SetSelectedGO(null);
			arcadeMenu.ViewManager.SwapPage("Song Menu");
		});

		button.LevelButton.UpdateTitle(LSText.ClampString(level.TrackName, 20, " -"));
		button.LevelButton.UpdateDifficulty(difficulty);
		button.LevelButton.UpdateAlbumArt(level.AlbumArt);
		button.LevelButton.UpdateContent(arcadeMenu.getLevelButtonContent(level));
		button.LevelButton.UpdateRank(SingletonBase<SavesManager>.Inst
			.FetchArcadeSave(level.LevelData.LevelID, _spoof: true).LevelRank);
		button.LevelButton.Stutter();
		button.LevelButton.Show(0f, 0.1f);

		button.Button.GetComponent<DirectedNavigation>().Active = true;
		button.Button.LockButtonState(_lock: false);
	}
}

[HarmonyPatch(typeof(ArcadeLevelDataManager))]
public static class ArcadeDataPatch
{
	[HarmonyPatch(nameof(ArcadeLevelDataManager.GetLocalCustomLevel))]
	[HarmonyPostfix]
	static void preGetLevel(ArcadeLevelDataManager __instance, string _id, ref VGLevel __result)
	{
		if (!__result || __result.LevelMusic)
		{
			return;
		}

		VGLevelWrapper? levelRealm = LessRam.Levels.GetValueOrDefault(_id, null);
		if (levelRealm == null)
		{
			return;
		}
		__instance.StartCoroutineAsync(LevelLoaderHelper.LoadAudio(__result, levelRealm!.AudioPath));
		__instance.StartCoroutineAsync(LevelLoaderHelper.LoadImage(__result, levelRealm!.ImagePath));
	}
}

[HarmonyPatch(typeof(LSText))]
public static class LSTextPatch
{
	[HarmonyPatch(nameof(LSText.ClampString))]
	[HarmonyPrefix]
	static bool PreClamp(ref string __result, string _inputStr, int _maxLength, string _end)
	{
		if (!SteamWorkshopFacepunch.inst.isLoadingLevels) //stupid hack, if we are loading LSText.sb would be accessed by multiple threads
		{
			return true;
		}

		if (string.IsNullOrEmpty(_inputStr) || _inputStr.Length <= _maxLength)
		{
			__result = _inputStr;
			return false;
		}

		StringBuilder sb = new();
		if (_end == null)
		{
			sb.Append(_inputStr.Substring(0, _maxLength));
		}
		else
		{
			sb.Append(_inputStr.Substring(0, _maxLength - (_end.Length - 1)));
			sb.Append(_end);
		}
		__result = sb.ToString();
		return false;
	}
}