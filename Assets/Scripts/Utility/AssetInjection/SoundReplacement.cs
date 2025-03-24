// Project:         Daggerfall Unity
// Copyright:       Copyright (C) 2009-2023 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: TheLacus
// Contributors: Zed
// 
// Notes:
//

namespace DaggerfallWorkshop.Utility.AssetInjection;

using System.Collections;
using System.IO;
using UnityEngine;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Diagnostics.CodeAnalysis;
using UnityEngine.Networking;
using System;

/// <summary>
/// Handles import and injection of custom sounds and songs with the purpose of providing modding support.
/// Sound files are imported from mod bundles with load order or loaded directly from disk.
/// </summary>
public static class SoundReplacement
{
    #region Fields & Properties

    static readonly string soundPath = Path.Combine(Application.streamingAssetsPath, "Sound");

    /// <summary>
    /// Path to custom sounds and songs on disk.
    /// </summary>
    public static string SoundPath
    {
        get { return soundPath; }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Seek sound from mods.
    /// </summary>
    /// <param name="sound">Sound clip to seek.</param>
    /// <param name="audioClip">Audioclip with imported sound data.</param>
    /// <returns>True if sound is found.</returns>
    public static bool TryImportSound(SoundClips sound, [NotNullWhen(true)] out AudioClip? audioClip)
    {
        return TryImportAudioClip($"{sound}.wav", AudioType.WAV, false, out audioClip);
    }

    /// <summary>
    /// Seek song from mods.
    /// </summary>
    /// <param name="song">Song to seek.</param>
    /// <param name="audioClip">Audioclip with imported sound data.</param>
    /// <returns>True if song is found.</returns>
    public static bool TryImportSong(SongFiles song, [NotNullWhen(true)] out AudioClip? audioClip)
    {
        return TryImportAudioClip($"{song}.ogg", AudioType.OGGVORBIS, true, out audioClip);
    }

    /// <summary>
    /// Seek midi song from mods.
    /// </summary>
    /// <param name="filename">Name of song to seek including .mid extension.</param>
    /// <param name="songBytes">Midi data as a byte array.</param>
    /// <returns>True if song is found.</returns>
    public static bool TryImportMidiSong(string filename, [NotNullWhen(true)] out byte[]? songBytes)
    {
        return TryGetAudioBytes($"song_{filename}", out songBytes);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Import sound data from modding locations as an audio clip.
    /// </summary>
    private static bool TryImportAudioClip(string fileName, AudioType audioType, bool isStreaming, [NotNullWhen(true)] out AudioClip? audioClip)
    {
        audioClip = null;

        if (DaggerfallUnity.Settings.AssetInjection && !string.IsNullOrWhiteSpace(fileName))
        {
            // Seek from loose files
            string path = Path.Combine(soundPath, fileName);
            if (File.Exists(path))
            {
                AudioClip? clip = null;
                var onComplete = (AudioClip? ac) => { clip = ac; };
                var isCompressed = false; // TODO: Do we want to compress audio in memory?

                DaggerfallUnity.Instance.StartCoroutine(WebRequestAudio(path, onComplete, isStreaming, isCompressed, audioType));

                audioClip = clip;
                return audioClip != null;
            }

            // Seek from mods
            if (ModManager.Instance != null && ModManager.Instance.TryGetAsset(fileName, false, out audioClip))
            {
                if (audioClip != null && (audioClip.preloadAudioData || audioClip.LoadAudioData()))
                    return true;

                Debug.LogErrorFormat("Failed to load audiodata for audioclip {0}", fileName);
            }
        }

        return audioClip != null;
    }

    /// <summary>
    /// Import midi data from modding locations as a byte array.
    /// </summary>
    private static bool TryGetAudioBytes(string name, [NotNullWhen(true)] out byte[]? songBytes)
    {
        if (DaggerfallUnity.Settings.AssetInjection)
        {
            // Seek from loose files
            string path = Path.Combine(soundPath, name);
            if (File.Exists(path))
            {
                songBytes = File.ReadAllBytes(path);
                return true;
            }

            // Seek from mods
            if (ModManager.Instance != null)
            {
                if (ModManager.Instance.TryGetAsset<TextAsset>(name, false, out var textAsset))
                {
                    songBytes = textAsset.bytes;
                    return true;
                }
            }
        }

        songBytes = null;
        return false;
    }

    private static IEnumerator WebRequestAudio(string path, Action<AudioClip?> onComplete, bool isStreaming = false, bool isCompressed = false, AudioType audioType = AudioType.UNKNOWN)
    {
        using var wr = UnityWebRequestMultimedia.GetAudioClip($"file://{path}", audioType);
        if (wr.downloadHandler is not DownloadHandlerAudioClip handler) yield break;

        handler.streamAudio = isStreaming;
        handler.compressed = isCompressed;

        yield return wr.SendWebRequest();

        if (wr.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Failed to load audioclip: {wr.error}");
        }

        var audioClip = handler.audioClip;
        onComplete?.Invoke(audioClip);
    }

    #endregion
}
