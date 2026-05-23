using System.Collections;
using CielaSpike;
using UnityEngine;
using UnityEngine.Networking;


namespace LessRam;

//kinda to replace a task, since unity hates them
public static class LevelLoaderHelper
{
	public static IEnumerator LoadAudio(VGLevel level, string path)
	{
		
		var handler = new DownloadHandlerAudioClip(path, AudioType.OGGVORBIS);
		handler.streamAudio = true;

		UnityWebRequest audioUwr = new UnityWebRequest(path, "GET", (DownloadHandler)handler, null);
		yield return audioUwr.SendWebRequest();
		if (audioUwr.result != UnityWebRequest.Result.Success)
		{
			LessRam.Logger.LogError(audioUwr.error);
			audioUwr.Dispose();
			yield break;
		}
		
		yield return Ninja.JumpToUnity;
		
		if (!level)
		{
			audioUwr.Dispose();
			yield break;
		}
		
		AudioClip audioClip = DownloadHandlerAudioClip.GetContent(audioUwr);
		audioUwr.Dispose();
		
		if (!audioClip)
		{
			LessRam.Logger.LogError($"Remove invalid song from arcade. No audio file. [{level.name}]");
			yield break;
		}
		
		ArcadeMenuPatch.activeClips.Add(audioClip);
		audioClip.name = level.name;
		level.LevelMusic = audioClip;
	}

	public static IEnumerator LoadImage(VGLevel level, string path)
	{
		Sprite albumArt = null;
		UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(path, false);
		yield return uwr.SendWebRequest();
		
		if (uwr.result != UnityWebRequest.Result.Success)
		{
			LessRam.Logger.LogError(uwr.error);
			uwr.Dispose();
			yield break;
		}
		
		Texture2D texture = DownloadHandlerTexture.GetContent(uwr);
		int width = texture.width;
		int height = texture.height;

		if (width <= 512 && height <= 512 && width == height)
		{
			albumArt = Sprite.Create(texture, new Rect(0f, 0f, width, height),
				new Vector2(0.5f, 0.5f), 72, 0u, SpriteMeshType.FullRect);
		}

		uwr.Dispose();
		
		yield return Ninja.JumpToUnity;

		if (!level)
		{
			Object.Destroy(texture);
			Object.Destroy(albumArt);
			yield break;
		}
		
		ArcadeMenuPatch.activeSprites.Add(albumArt!);
		level.AlbumArt = albumArt;
	}
}

public static class VGLevelExtension
{
	public static bool InitSteamInfoFix(this VGLevel level, ulong _id, string _folder)
	{
		if (string.IsNullOrEmpty(_folder))
		{
			return false;
		}
		level.SteamInfo = new VGLevel.SteamData
		{
			ItemID = _id
		};
		
		level.BaseLevelData = new VGLevel.LevelDataBase
		{
			LevelID = _id.ToString(),
			LocalFolder = _folder
		};
		level.LevelData = level.BaseLevelData;
		
		return true;
	}
}