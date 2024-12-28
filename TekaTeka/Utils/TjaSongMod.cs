using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TekaTeka.Utils
{
    internal class TjaSongMod : SongMod
    {
        public override bool IsValidMod()
        {
            throw new NotImplementedException();
        }

        public override void AddMod(ModdedSongsManager manager)
        {
            throw new NotImplementedException();
        }

        public override SongEntry GetSongEntry(string id, bool idIsSongFile = false)
        {
            throw new NotImplementedException();
        }

        public override string GetModFolder()
        {
            throw new NotImplementedException();
        }
    }
}
