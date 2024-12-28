using AsmResolver.Patching;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Cysharp.Threading.Tasks.Linq;
using HarmonyLib;
using System.Collections;
using System.Text;
using TekaTeka.Utils;

namespace TekaTeka.Plugins
{
    internal class CustomSongLoader
    {
        public const string CHARTS_FOLDER = "fumen";
        public const string PRACTICE_DIVISIONS_FOLDER = "fumencsv";
        public const string ASSETS_FOLDER = "ReadAssets";
        public const string SONGS_FOLDER = "sound";

        public static readonly string songsPath = Path.Combine(BepInEx.Paths.GameRootPath, "TekaSongs");

        static List<MusicDataInterface.MusicInfo> customSongsList = new List<MusicDataInterface.MusicInfo>();

        static ModdedSongsManager songsManager;

        public static void InitializeLoader()
        {
            if (!Directory.Exists(songsPath))
            {
                Directory.CreateDirectory(songsPath);
            }
        }

#region Append Custom Songs DB

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DataManager), nameof(DataManager.Awake))]
        static void DataManagerAwake_Postfix(DataManager __instance)
        {
            if (__instance.InitialPossessionData == null)
            {
                return;
            }
            songsManager = new ModdedSongsManager();
        }

#endregion

#region Load Custom Chart

        static IEnumerator emptyEnumerator()
        {
            yield return null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(FumenLoader.PlayerData), "ReadCoroutine")]
        static unsafe bool ReadSongChart_Prefix(FumenLoader.PlayerData __instance, ref string filePath,
                                                ref Il2CppSystem.Collections.IEnumerator __result)
        {
            // Lets just hope this doesnt cause any leaks...
            __instance.DestroyFumenBuffer();

            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string songId = fileName.Substring(0, fileName.LastIndexOf('_')); // abcdef_e -> abcdef
            string diff = fileName.Substring(fileName.LastIndexOf('_') + 1);
            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> bytes;

            SongMod? mod = songsManager.GetModPath(songId);
            if (mod != null)
            {
                SongEntry songEntry = mod.GetSongEntry(fileName);
                bytes = songEntry.GetFumenBytes();
                filePath = songEntry.GetFilePath();
            }
            else
            {
                bytes = Cryptgraphy.ReadAllAesAndGZipBytes(filePath, Cryptgraphy.AesKeyType.Type2);
            }

            __instance.fumenPath = filePath;

            if (__instance != null)
            {
                __instance.WriteFumenBuffer(bytes);
                __instance.isReadEnd = true;
                __instance.isReadSucceed = true;
            }
            __result = emptyEnumerator().WrapToIl2Cpp();
            return false;
        }

#endregion

#region Load Custom Practice Chart

        [HarmonyPrefix]
        [HarmonyPatch(typeof(FumenDivisionManager), nameof(FumenDivisionManager.Load))]
        static bool ReadSongPracticeDivision_Prefix(FumenDivisionManager __instance, ref string musicuid)
        {
            string originalFile =
                Path.Combine(UnityEngine.Application.streamingAssetsPath, PRACTICE_DIVISIONS_FOLDER, musicuid + ".bin");

            SongMod? mod = songsManager.GetModPath(musicuid);
            string modName = mod != null ? mod.GetModFolder() : "";

            string filePath = Path.Combine(songsPath, modName, PRACTICE_DIVISIONS_FOLDER, musicuid);
            if (File.Exists(originalFile) || (!File.Exists(filePath + ".bin") && !File.Exists(filePath + ".csv")))
            {
                return true;
            };

            bool isEncrypted = File.Exists(filePath + ".bin") && !File.Exists(filePath + ".csv");

            string csvString;

            if (isEncrypted)
            {
                var bytes = Cryptgraphy.ReadAllAesAndGZipBytes(filePath + ".bin", Cryptgraphy.AesKeyType.Type2);
                csvString = Encoding.UTF8.GetString(bytes);
            }
            else
            {
                csvString = File.ReadAllText(filePath + ".csv");
            }
            var datas = __instance.loader_.CreateDivisionData(csvString);
            __instance.datas_ = datas;
            __instance.IsLoadFinished = true;
            __instance.musicuId_ = musicuid;
            return false;
        }

#endregion

#region Load Custom Song file

        static IEnumerator CustomSongLoad(CriPlayer player)
        {
            if (player != null)
            {
                player.isLoadingAsync = true;
                player.isCancelLoadingAsync = false;
                player.IsLoadSucceed = false;
                player.IsPrepared = false;
                player.LoadingState = CriPlayer.LoadingStates.Loading;
                player.LoadTime = -1.0f;
                player.loadStartTime = UnityEngine.Time.time;

                if (player.CueSheetName == "")
                {
                    player.isLoadingAsync = false;
                    player.LoadingState = CriPlayer.LoadingStates.Finished;
                    // This is wrong, and it causes a warning, but honestly, i have no idea how to use UniTask and the
                    // game softlocks anyway(I think)
                    yield return false;
                }

                string originalFile = Path.Combine(UnityEngine.Application.streamingAssetsPath, SONGS_FOLDER,
                                                   player.CueSheetName + ".bin");

                string songFile = player.CueSheetName.TrimStart('P'); // PSONG_.. -> SONG_..

                SongMod? mod = songsManager.GetModPath(songFile);
                string modName = mod != null ? mod.GetModFolder() : "";
                string modFile = Path.Combine(songsPath, modName, SONGS_FOLDER, player.CueSheetName);
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> bytes;

                if (mod != null)
                {
                    SongEntry songEntry = mod.GetSongEntry(songFile, true);
                    bytes = songEntry.GetSongBytes(player.CueSheetName.StartsWith("P"));
                }
                else
                {
                    bytes = Cryptgraphy.ReadAllAesBytes(originalFile, Cryptgraphy.AesKeyType.Type0);
                }

                var cueSheet = CriAtom.AddCueSheet(player.CueSheetName, bytes, null, null);
                player.CueSheet = cueSheet;

                player.isLoadingAsync = false;
                player.IsLoadSucceed = true;
                player.IsPrepared = true;
                player.LoadingState = CriPlayer.LoadingStates.Finished;

                // Not exactly the same code, but good enough for perfect conditions
            }
        }

        // Patch when loading a SONG_ file
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CriPlayer), nameof(CriPlayer.LoadAsync))]
        static bool CriPlayerLoadAsync_Prefix(CriPlayer __instance, ref Il2CppSystem.Collections.IEnumerator __result)
        {
            if (!__instance.CueSheetName.StartsWith("SONG_") && !__instance.CueSheetName.StartsWith("PSONG_"))
            {
                return true;
            }

            __result = CustomSongLoad(__instance).WrapToIl2Cpp();
            if (__result == null)
            {
                return true;
            }

            return false;
        }

        // Patch when loading a PSONG_ file(Preview song)
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CriPlayer), nameof(CriPlayer.LoadPreviewBgmAsync))]
        static bool CriPlayerBgmLoadAsync_Prefix(CriPlayer __instance,
                                                 ref Il2CppSystem.Collections.IEnumerator __result, ref int downloadId)
        {
            // If it is a dlc or music pass song, use original
            if (downloadId != -1)
            {
                return true;
            }

            if (!__instance.CueSheetName.StartsWith("SONG_") && !__instance.CueSheetName.StartsWith("PSONG_"))
            {
                return true;
            }

            __result = CustomSongLoad(__instance).WrapToIl2Cpp();
            if (__result == null)
            {
                return true;
            }

            return false;
        }

#endregion
    }
}
