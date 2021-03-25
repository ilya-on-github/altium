using System.IO;
using System.Threading.Tasks;

namespace Altium
{
    public static class StreamReaderExtensions
    {
        public static async Task<string> ReadLineAsync(this StreamReader reader, long lineIndex)
        {
            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            reader.DiscardBufferedData();

            var i = 0;
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();

                if (i == lineIndex)
                {
                    return line;
                }

                i++;
            }

            return null;
        }
    }
}