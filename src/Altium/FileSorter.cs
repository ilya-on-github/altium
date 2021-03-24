using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Altium
{
    public class FileSorter
    {
        private readonly Regex _lineRegex = new Regex(@"^(?<num>\d+)\.\s(?<text>.*)$");

        public async Task Sort(string fileName, CancellationToken ct)
        {
            if (!File.Exists(fileName))
            {
                throw new ArgumentException($"File '{fileName}' doesn't exist.", nameof(fileName));
            }

            var directoryName = Path.GetDirectoryName(Path.GetFullPath(fileName));
            // ReSharper disable once AssignNullToNotNullAttribute
            var outFileName = Path.Combine(directoryName,
                Path.GetFileNameWithoutExtension(fileName) + "_sorted" + Path.GetExtension(fileName));


            var sw = new Stopwatch();
            sw.Start();

            var ranks = new List<LineInfo>();
            using (var file = File.OpenRead(fileName))
            {
                using (var streamReader = new StreamReader(file))
                {
                    var lineIndex = 0;
                    string line;
                    while (!streamReader.EndOfStream)
                    {
                        var position = file.Position;
                        line = await streamReader.ReadLineAsync();

                        var rank = IndexLine(position, lineIndex, line);

                        ranks.Add(rank);

                        lineIndex++;
                    }

                    var linesOrdered = ranks.OrderBy(x => x.TextLength)
                        .ThenByDescending(x => x.TextScore)
                        .ThenBy(x => x.Number)
                        .ToList();

                    using (var outFile = File.Create(outFileName))
                    {
                        using (var streamWriter = new StreamWriter(outFile))
                        {
                            var batchSize = 1000;
                            var i = 0;

                            foreach (var lineInfo in linesOrdered)
                            {
                                file.Position = lineInfo.PositionInFile;

                                line = await streamReader.ReadLineAsync();
                                await streamWriter.WriteLineAsync(line);

                                i++;

                                if (i % batchSize == 0)
                                {
                                    await streamWriter.FlushAsync();
                                }
                            }
                        }
                    }

                    Debug.WriteLine($"Ok, elapsed: {sw.Elapsed.TotalMilliseconds}.");
                    Debug.WriteLine(linesOrdered);
                }
            }
        }

        private LineInfo IndexLine(long linePosition, long lineIndex, string line)
        {
            var match = _lineRegex.Match(line);

            if (!match.Success)
            {
                throw new ArgumentException("Invalid line format.", nameof(line));
            }

            var number = long.Parse(match.Groups["num"].Value);
            var text = match.Groups["text"].Value;

            const string order = "0123456789AaBbCcDdEeFfGgHhIiJjKkLlMmNnOoPpQqRrSsTtUuVvWwXxYyZz";

            var maxLineLength = 1024; // TODO: пересмотреть необходимость этого параметра

            long score = 0;
            for (var i = 0; i < text.Length; i++)
            {
                // позиция важна, нужно учитывать индекс символа
                var c = text[i];
                var cIndex = order.IndexOf(c);
                if (cIndex == -1)
                {
                    continue; // не учитывается в ранге
                }

                // чем раньше встречается в order, тем выше score
                // чем ближе символ к началу позиции, тем выше score
                score += (maxLineLength - i) * (order.Length - cIndex);
            }

            return new LineInfo(linePosition, lineIndex, text.Length, score, number);
        }

        internal class LineInfo
        {
            public long PositionInFile { get; }
            public long Index { get; }
            public int TextLength { get; }
            public long TextScore { get; }
            public long Number { get; }

            public LineInfo(long position, long index, int textLength, long textScore, long number)
            {
                PositionInFile = position;
                Index = index;
                TextLength = textLength;
                TextScore = textScore;
                Number = number;
            }
        }
    }
}