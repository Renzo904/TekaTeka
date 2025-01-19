using TekaTeka.Plugins;
using tja2fumen;

namespace TekaTeka.Utils
{
    internal class TjaSongEntry : SongEntry
    {
        string modFolder;
        string modPath;

        tja2fumen.TJASong song;
        TjaSongMod.Genre genre;

        string modFolderPath => Path.Combine(CustomSongLoader.songsPath, "TJAsongs",
                                             TjaSongMod.GenreFolders[(int)this.genre], this.modFolder);

        string tjaPath => Path.Combine(this.modFolderPath, this.modFolder + ".tja");
        string savesPath => Path.Combine(this.modFolderPath, "saves.json");

        string wavePath => Path.Combine(this.modFolderPath, this.GetWaveName());

        public TjaSongEntry(string modFolder, int id, TjaSongMod.Genre genre)
        {
            this.modFolder = modFolder;
            this.genre = genre;
            this.modPath = modFolderPath;
            song = tja2fumen.Parsers.ParseTja(this.tjaPath);
            tja2fumen.FumenCourse? fumenCourse = null;
            int[] shinuchiScores = new int[5];
            int i = 0;
            foreach (string diff in tja2fumen.Constants.COURSE_NAMES)
            {
                if (song.courses.ContainsKey(diff))
                {
                    fumenCourse = tja2fumen.Converters.ConvertTjaToFumen(song.courses[diff]);
                    shinuchiScores[i] = fumenCourse.shinuchiScore;
                }
                else
                {
                    shinuchiScores[i] = 0;
                }
                i++;
            }

            uint songHash = _3rdParty.MurmurHash2.Hash(File.ReadAllBytes(this.tjaPath)) & 0xFFFF_FFF;

            this.musicInfo = new MusicDataInterface.MusicInfo { SongNameJP = this.song.metadata.titleJA,
                                                                SongNameEN = this.song.metadata.titleEN,
                                                                SongNameKO = this.song.metadata.titleKO,
                                                                SongNameCN = this.song.metadata.titleCN,
                                                                SongNameTW = this.song.metadata.titleTW,
                                                                SongNameFR = this.song.metadata.title,
                                                                SongNameES = this.song.metadata.title,
                                                                SongNameDE = this.song.metadata.title,
                                                                SongNameIT = this.song.metadata.title,
                                                                SongSubJP = this.song.metadata.subtitleJA,
                                                                SongSubEN = this.song.metadata.subtitleEN,
                                                                SongSubKO = this.song.metadata.subtitleKO,
                                                                SongSubCN = this.song.metadata.subtitleCN,
                                                                SongSubTW = this.song.metadata.subtitleTW,
                                                                SongSubFR = this.song.metadata.subtitle,
                                                                SongSubES = this.song.metadata.subtitle,
                                                                SongSubDE = this.song.metadata.subtitle,
                                                                SongSubIT = this.song.metadata.subtitle,
                                                                ShinutiEasy = shinuchiScores[0],
                                                                ShinutiNormal = shinuchiScores[1],
                                                                ShinutiHard = shinuchiScores[2],
                                                                ShinutiMania = shinuchiScores[3],
                                                                ShinutiUra = shinuchiScores[4],
                                                                SongFileName = $"SONG_{songHash}",
                                                                GenreNo = (int)this.genre,
                                                                Order = 1300000,
                                                                HasPreviewInPackage = true,
                                                                IsDefault = true,
                                                                CanMyBattleSong = false,
                                                                IsCap = true,
                                                                IsOfflinePickUp = true,
                                                                InPackage = 1 };

            foreach (var branch in tja2fumen.Constants.COURSE_IDS.Keys)
            {

                string fumenBranch = branch;
                if (fumenBranch == "Oni")
                {
                    fumenBranch = "Mania";
                }
                var branchProperty = typeof(MusicDataInterface.MusicInfo).GetProperty($"Branch{fumenBranch}");
                var starProperty = typeof(MusicDataInterface.MusicInfo).GetProperty($"Star{fumenBranch}");
                if (branchProperty != null)
                {
                    branchProperty.SetValue(
                        musicInfo, song.courses.ContainsKey(branch) ? song.courses[branch].hasBranches : false);
                }
                if (starProperty != null)
                {
                    starProperty.SetValue(musicInfo, song.courses.ContainsKey(branch) ? song.courses[branch].level : 0);
                }
            }

            musicInfo.UniqueId = id;
            musicInfo.Id = songHash.ToString();
        }

        public override string GetFilePath()
        {
            return this.tjaPath;
        }

        public string GetWaveName()
        {
            return this.song.metadata.wave;
        }

        public override byte[] GetFumenBytes()
        {
            string diff = this.songFile.Split('_')[1];
            string ? tjaDiff;
            tja2fumen.Constants.COURSE_IDS_REVERSE.TryGetValue(diff, out tjaDiff);
            if (tjaDiff == null)
            {
                tjaDiff = "Ura";
            }
            var tjaParsed = tja2fumen.Parsers.ParseTja(this.GetFilePath());
            tja2fumen.FumenCourse? fumenCourse = null;
            if (tjaParsed.courses.ContainsKey(tjaDiff))
            {
                fumenCourse = tja2fumen.Converters.ConvertTjaToFumen(tjaParsed.courses[tjaDiff]);
            }
            else
            {
                // If the course doesn't exists, get the first course from hardest to easiest
                foreach (var course in tja2fumen.Constants.COURSE_IDS.Keys.Reverse())
                {
                    if (tjaParsed.courses.ContainsKey(course))
                    {
                        fumenCourse = tja2fumen.Converters.ConvertTjaToFumen(tjaParsed.courses[course]);
                        break;
                    }
                }
            }

            if (fumenCourse != null)
            {
                tja2fumen.Converters.FixDkNoteTypesCourse(fumenCourse);
                return tja2fumen.Writer.getFumenBytes(fumenCourse);
            }
            else
            {
                return Array.Empty<byte>();
            }
        }

        public override byte[] GetSongBytes(bool isPreview = false)
        {
            var directory = Path.GetDirectoryName(this.modFolderPath);

            string acbFile = Path.GetFileNameWithoutExtension(this.song.metadata.wave) + ".acb";
            int offset;
            if (isPreview)
            {
                acbFile = "P" + acbFile;
                offset = (int)(this.song.metadata.demoStart * 1000);
            }
            else
            {
                offset = (int)((60 / this.song.bpm * 1000) * 4);
            }

            var acbPath = Path.Combine(this.modFolderPath, acbFile);

            tja2fumen.TJAConvert.FileType fileType;
            switch (Path.GetExtension(this.wavePath))
            {
            case ".wav":
                fileType = tja2fumen.TJAConvert.FileType.WAV;
                break;
            case ".mp3":
                fileType = tja2fumen.TJAConvert.FileType.MP3;
                break;
            case ".ogg":
                fileType = tja2fumen.TJAConvert.FileType.OGG;
                break;
            default:
                fileType = tja2fumen.TJAConvert.FileType.UNK;
                break;
            }

            if (tja2fumen.TJAConvert.ConvertToAcb(this.wavePath, fileType, isPreview, offset))
            {
                return File.ReadAllBytes(acbPath);
            }
            return null;
        }
    }
}
