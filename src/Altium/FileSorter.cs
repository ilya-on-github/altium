using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Altium
{
    public class FileSorter
    {
        private readonly FileSorterOptions _options;

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

            long curOffset = 0;
            string line;
            while (!streamReader.EndOfStream)
            {
                ct.ThrowIfCancellationRequested();

                // TODO: исправить проблему с некорректным вычислением позиции начала строки
                // StreamReader использует буфер, из-за чего file.Position не даёт нужное значение.
                line = await streamReader.ReadLineAsync();

                var lineInfo = IndexLine(curOffset, line);

                linesIndex.Add(lineInfo);

                // ReSharper disable once AssignNullToNotNullAttribute
                var offset = streamReader.CurrentEncoding.GetByteCount(line)
                             + streamReader.CurrentEncoding.GetByteCount(Environment.NewLine);

                curOffset += offset;
            }

            // сортировка на основе вычисленого ранга
            // TODO: придумать, как учитывать длину строки в значении TextScore при ранжировании
            var linesOrdered = linesIndex.OrderBy(x => x.TextLength)
                .ThenByDescending(x => x.TextScore)
                .ThenBy(x => x.Number)
                .ToList();

            await using var outFile = File.Create(outFileName);
            await using var streamWriter = new StreamWriter(outFile);

            foreach (var lineInfo in linesOrdered)
            {
                ct.ThrowIfCancellationRequested();

                // 1024 chars = 1024 bytes UTF-8
                // 10_000_000_000 // 2^64
                if (lineInfo.Offset < 0)
                {
                    throw new InvalidOperationException("Offset: {}");
                }

                file.Position = lineInfo.Offset;
                streamReader.DiscardBufferedData();
                line = await streamReader.ReadLineAsync();

                await streamWriter.WriteLineAsync(line);
            }

            // TODO: убрать по завершении
            Debug.WriteLine($"Ok, elapsed: {sw.Elapsed.TotalMilliseconds}.");
            Debug.WriteLine(linesOrdered);
        }

        private LineInfo IndexLine(long offset, string lineStr)
        {
            throw new NotImplementedException();
            var line = Line.Parse(lineStr);
            const string order = "0123456789AaBbCcDdEeFfGgHhIiJjKkLlMmNnOoPpQqRrSsTtUuVvWwXxYyZz";

            // TODO: исправить
            // должны учитываться
            // - символ
            // - позиция
            // Количество вариантов на каждой позиции = ширина множества,
            // по условию это 26*2 (символы A-Z в двух регистрах) + 10 (цифры) = 62
            // Для учёта позиции, чтобы старший разряд всегда перевешивал, вычисление должно быть
            // 62^i, где i - номер разряда (для символа на позии 0 i = 1024)
            // Минусы:
            // 1. 62^1024 - это infinity, такое не вычислить
            // 2. Сложность напрямую зависит от ширины алфавита и от длины строк, решение плохо масштабируется
            // 
            // Мб последовательно сортировать по символу?
            // Рекурсивно сортировать по разряду, запоминая позиции с одинаковым символом в данном разряде.
            // Сложности:
            // 1. Надо каким-то образом не сбить указатели блоков в файле, потому что свап строк - не атомарная операция.
            // 2. Граничные ситуации, например как когда в первом разряде в каждой строке одинаковый символ.
            // 1) пройти по файлу, отсортировав по первому символу, запомнив диапазоны файла, где все строки начинаются
            // с одного символа

            var maxLineLength = 1024;

            long score = 0;
            for (var i = 0; i < line.Text.Length; i++)
            {
                // позиция важна, нужно учитывать индекс символа
                var c = line.Text[i];
                var cIndex = order.IndexOf(c);
                if (cIndex == -1)
                {
                    continue; // не учитывается в ранге
                }

                // чем раньше встречается в order, тем выше score
                // чем ближе символ к началу позиции, тем выше score
                score += (maxLineLength - i) * (order.Length - cIndex);
            }

            return new LineInfo(offset, line.Text.Length, score, line.Number);
        }

        internal class LineInfo
        {
            public long Offset { get; }
            public int TextLength { get; }
            public long TextScore { get; }
            public long Number { get; }

            public LineInfo(long offset, int textLength, long textScore, long number)
            {
                Offset = offset;
                TextLength = textLength;
                TextScore = textScore;
                Number = number;
            }
        }
    }
}