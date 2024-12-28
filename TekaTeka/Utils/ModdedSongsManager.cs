using System.Text;
using TekaTeka.Plugins;
using Tommy;

namespace TekaTeka.Utils
{
    internal class ModdedSongsManager
    {
        public HashSet<int> currentSongs = new HashSet<int>();
        public Dictionary<int, SongMod> uniqueIdToMod = new Dictionary<int, SongMod>();
        public Dictionary<string, SongMod> idToMod = new Dictionary<string, SongMod>();
        public Dictionary<string, SongMod> songFileToMod = new Dictionary<string, SongMod>();
        public MusicDataInterface musicData => TaikoSingletonMonoBehaviour<DataManager>.Instance.MusicData;
        public InitialPossessionDataInterface initialPossessionData =>
            TaikoSingletonMonoBehaviour<DataManager>.Instance.InitialPossessionData;

        public ModdedSongsManager()
        {

            foreach (MusicDataInterface.MusicInfoAccesser accesser in musicData.MusicInfoAccesserList)
            {
                this.currentSongs.Add(accesser.UniqueId);
            }
            this.SetupMods();
        }

        public List<SongMod> GetMods()
        {
            List<SongMod> mods = new List<SongMod>();
            foreach (string path in Directory.GetDirectories(CustomSongLoader.songsPath))
            {
                string folder = Path.GetFileName(path) ?? "";
                if (folder != "")
                {
                    FumenSongMod mod = new FumenSongMod(folder);
                    if (mod.enabled)
                    {
                        Logger.Log($"Mod {mod.modName} Loaded", LogType.Info);
                        mods.Add(mod);
                    }
                }
            }
            return mods;
        }

        public void SetupMods()
        {
            List<SongMod> mods = this.GetMods();
            foreach (SongMod mod in mods)
            {
                if (mod.enabled)
                {
                    mod.AddMod(this);
                }
            }
        }

        public SongMod? GetModPath(int uniqueId)
        {
            if (this.uniqueIdToMod.ContainsKey(uniqueId))
            {
                return this.uniqueIdToMod[uniqueId];
            }
            else
            {
                return null;
            }
        }

        public SongMod? GetModPath(string songFileName)
        {
            if (this.songFileToMod.ContainsKey(songFileName))
            {
                return this.songFileToMod[songFileName];
            }
            else if (this.idToMod.ContainsKey(songFileName))
            {
                return this.idToMod[songFileName];
            }
            else
            {
                return null;
            }
        }
    }
}
