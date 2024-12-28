using AsmResolver;
using Cpp2IL.Core.Graphs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TekaTeka.Utils
{
    internal abstract class SongEntry
    {
        public string songFile { get; set; }

        public MusicDataInterface.MusicInfo musicInfo { get; set; }

        public abstract byte[] GetFumenBytes();

        public abstract byte[] GetSongBytes(bool isPreview = false);

        public abstract string GetFilePath();
    }
}
