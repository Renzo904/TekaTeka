using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using System.Collections;
using System.Text;
using TekaTeka.Utils;
using Scripts.UserData;
using Il2CppSystem.Security.Cryptography;

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

        static CommonObjects commonObjects => TaikoSingletonMonoBehaviour<CommonObjects>.Instance;

        static ModdedSongsManager songsManager;

        public static void InitializeLoader()
        {
            if (!Directory.Exists(songsPath))
            {
                Directory.CreateDirectory(songsPath);
            }

            if (!Directory.Exists(Path.Combine(songsPath, "TJAsongs")))
            {
                Directory.CreateDirectory(Path.Combine(songsPath, "TJAsongs"));
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UnityEngine.Logger), nameof(UnityEngine.Logger.LogException))]
        [HarmonyPatch(new Type[] { typeof(Il2CppSystem.Exception), typeof(UnityEngine.Object) })]
        static void DemistifyStackTrace(Il2CppSystem.Exception exception, UnityEngine.Object context)
        {
            Logger.Log(exception.GetStackTrace(true));
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

#region Custom Save Data

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ApplicationUserDataSave._SaveAsync_d__21),
                      nameof(ApplicationUserDataSave._SaveAsync_d__21.MoveNext))]
        public static void PatchSave(ref ApplicationUserDataSave._SaveAsync_d__21 __instance)
        {
            if (__instance.__1__state == 0)
            {
                var userData = __instance.__4__this.data;

                var backup = new Scripts.UserData.MusicInfoEx[userData.MusicsData.Datas.Length];

                userData.MusicsData.Datas.CopyTo(backup, 0);
                userData = songsManager.FilterModdedData(userData);

                var xml = XmlSerializerBehaviour.Serializer(userData);

                xml = Cryptgraphy.EncryptValueC(xml);

                var dataSize = System.BitConverter.GetBytes(xml.Length);

                var sha256Data = Cryptgraphy.GetHashByte<SHA256CryptoServiceProvider>(xml);

                xml = Cryptgraphy.CompositData(dataSize, sha256Data, xml);

                __instance._compositionData_5__2 = xml;

                commonObjects.Platform.Save.SaveAsync(__instance._compositionData_5__2);

                Scripts.UserData.MusicInfoEx[] datas = userData.MusicsData.Datas;
                Array.Resize(ref datas, backup.Length);
                Array.Copy(backup, 3000, datas, 3000, backup.Length - 3000);
                userData.MusicsData.Datas = datas;

                TaikoSingletonMonoBehaviour<SaveIcon>.Instance.Deactive();
                __instance.__1__state = 2;
                __instance._compositionData_5__2 = null;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Scripts.UserData.UserData), nameof(Scripts.UserData.UserData.FixData))]
        static void PatchLoad(ref Scripts.UserData.UserData __instance)
        {
            int oldLen = __instance.MusicsData.Datas.Length;
            int newLen = __instance.MusicsData.Datas.Length + songsManager.tjaSongs;

            Scripts.UserData.MusicInfoEx[] newArray = __instance.MusicsData.Datas;
            Scripts.UserData.MusicInfo2PEx[] newArray2 = Scripts.Scene.SceneDataExchanger.MusicData2P.Datas;

            Scripts.UserData.Flag.UserFlagDataDefine.FlagData[] songArray =
                __instance.UserFlagData.userFlagData[(int)Scripts.UserData.Flag.UserFlagData.FlagType.Song].ToArray();
            Scripts.UserData.Flag.UserFlagDataDefine.FlagData[] tittleSongArray =
                __instance.UserFlagData.userFlagData[(int)Scripts.UserData.Flag.UserFlagData.FlagType.TitleSongId]
                    .ToArray();

            Array.Resize(ref newArray, newLen);
            Array.Resize(ref newArray2, newLen);
            Array.Resize(ref songArray, newLen);
            Array.Resize(ref tittleSongArray, newLen);
            for (int i = 3000; i < newLen; i++)
            {
                newArray[i] = new Scripts.UserData.MusicInfoEx();
                newArray2[i] = new Scripts.UserData.MusicInfo2PEx();

                newArray[i].SetDefault();
                newArray2[i].SetDefault();
            }

            foreach (SongMod mod in songsManager.modsEnabled)
            {
                if (mod is TjaSongMod)
                {
                    TjaSongMod tjaMod = (TjaSongMod)mod;
                    int uniqueId = tjaMod.uniqueId;
                    var musicData = __instance.MusicsData;

                    __instance.MusicsData = musicData;
                    try
                    {

                        var musicInfo = tjaMod.LoadUserData();

                        newArray[uniqueId] = musicInfo;
                    }
                    catch (Exception e)
                    {
                        var musicInfo = new Scripts.UserData.MusicInfoEx();
                        musicInfo.SetDefault();
                        newArray[uniqueId] = musicInfo;
                    }

                    songArray[uniqueId] =
                        new Scripts.UserData.Flag.UserFlagDataDefine.FlagData() { Id = tjaMod.uniqueId };
                    tittleSongArray[uniqueId] =
                        new Scripts.UserData.Flag.UserFlagDataDefine.FlagData() { Id = tjaMod.uniqueId };
                }
            }

            __instance.MusicsData.Datas = newArray;
            Scripts.Scene.SceneDataExchanger.MusicData2P.Datas = newArray2;

            __instance.UserFlagData.userFlagData[(int)Scripts.UserData.Flag.UserFlagData.FlagType.Song] = songArray;
            __instance.UserFlagData.userFlagData[(int)Scripts.UserData.Flag.UserFlagData.FlagType.TitleSongId] =
                tittleSongArray;
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
            string songId = fileName.Split("_").First();

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
                    var request = Cryptgraphy.ReadAllAesBytesAsync(originalFile, Cryptgraphy.AesKeyType.Type0);
                    while (!request.IsDone)
                    {
                        yield return null;
                    }
                    bytes = request.Bytes;
                }

                var cueSheet = CriAtom.AddCueSheetAsync(player.CueSheetName, bytes, null, null);

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
