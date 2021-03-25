using System.IO;
using System.Threading.Tasks;

namespace Altium
{
    public class ExtendedStreamReader : StreamReader
    {
        private long _nextLineIndex;

        public ExtendedStreamReader(Stream stream)
            : base(stream)
        {
        }

        public async Task<string> ReadLineAsync(long lineIndex)
        {
            if (lineIndex < _nextLineIndex)
            {
                BaseStream.Seek(0, SeekOrigin.Begin);
                DiscardBufferedData();
                _nextLineIndex = 0;
            }

            while (!EndOfStream)
            {
                var line = await ReadLineAsync();

                _nextLineIndex++;

                if (_nextLineIndex - 1 == lineIndex)
                {
                    return line;
                }
            }

            return null;
        }
    }
}