using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Altium
{
    public class FileSorter
    {
        private readonly FileSorterOptions _options;

        private readonly Regex _lineRegex = new Regex(@"^(?<num>\d+)\.\s(?<text>.*)$");

        public FileSorter(IOptions<FileSorterOptions> options)
        {
            _options = options.Value;
        }

        public async Task Sort(string fileName, string outFileName, CancellationToken ct)
        {
            if (!File.Exists(fileName))
            {
                throw new ArgumentException($"File '{fileName}' doesn't exist.", nameof(fileName));
            }

            var sw = new Stopwatch();
            sw.Start();

            var linesIndex = new List<LineInfo>();
            await using var file = File.OpenRead(fileName);
            using var streamReader = new StreamReader(file);

            // Идея в том, чтобы прочесть файл, но сохранить не содержимое,
            // а пару числовых значений для каждой строки:
            //      1. её координаты в файле (позиция начала строки)
            //      2. значение ранга этой строки, необходимое для сортировки
            // Тем самым объём используемой памяти будет значительно ниже, чем если грузить в неё сам файл.
            // А после такой индексации отсортировать значения на основе их ранга и записать
            // в новый в файл, но уже в правильном порядке. При этом для чтения из исходного файла
            // позиции начала строк уже были сохранены.

            var lineIndex = 0;
            string line;
            while (!streamReader.EndOfStream)
            {
                ct.ThrowIfCancellationRequested();

                // TODO: исправить проблему с некорректным вычислением позиции начала строки
                // StreamReader использует буфер, из-за чего file.Position не даёт нужное значение.
                var position = file.Position;
                line = await streamReader.ReadLineAsync();

                var lineInfo = IndexLine(position, lineIndex, line);

                linesIndex.Add(lineInfo);

                lineIndex++;
            }

            // сортировка на основе вычисленого ранга
            // TODO: придумать, как учитывать длину строки в значении TextScore при ранжировании
            var linesOrdered = linesIndex.OrderBy(x => x.TextLength)
                .ThenByDescending(x => x.TextScore)
                .ThenBy(x => x.Number)
                .ToList();

            await using var outFile = File.Create(outFileName);
            await using var streamWriter = new StreamWriter(outFile);

            var batchSize = _options.WriteBatchSize;
            var i = 0;

            foreach (var lineInfo in linesOrdered)
            {
                ct.ThrowIfCancellationRequested();

                file.Position = lineInfo.PositionInFile;

                line = await streamReader.ReadLineAsync();
                await streamWriter.WriteLineAsync(line);

                i++;

                if (i % batchSize == 0)
                {
                    await streamWriter.FlushAsync();
                }
            }

            // TODO: убрать по завершении
            Debug.WriteLine($"Ok, elapsed: {sw.Elapsed.TotalMilliseconds}.");
            Debug.WriteLine(linesOrdered);
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

            var maxLineLength = 1024; // TODO: придумать как избавиться от этого параметра

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