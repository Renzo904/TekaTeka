using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TekaTeka.Utils
{
    internal abstract class SongMod
    {
        public bool enabled { get; set; }
        public string name { get; set; }
        protected string modFolder { get; set; }

        public abstract void AddMod(ModdedSongsManager manager);

        public abstract bool IsValidMod();

        public abstract string GetModFolder();

        public abstract SongEntry GetSongEntry(string id, bool idIsSongFile = false);
    }
}
