using System;
using System.Collections.Generic;
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

            var fileLength = new FileInfo(fileName).Length;
            long bytesRead = 0;
            var batchCount = 0;

            var batchFileNames = new List<string>();

            var writeTasks = new List<Task>();
            while (bytesRead < fileLength)
            {
                ct.ThrowIfCancellationRequested();

                var batch = await ReadBatch(fileName, bytesRead);

                bytesRead += batch.ByteCount;

                var batchFileName = CreateBatchFileName(fileName, batchCount);

                var writeBatch = WriteBatch(batchFileName, 0,
                    batch.Lines.OrderBy(x => x.Text)
                        .ThenBy(x => x.Number)
                        .Select(x => x.ToString()), ct);

                writeTasks.Add(writeBatch);
                batchFileNames.Add(batchFileName);

                batchCount++;
            }

            await Task.WhenAll(writeTasks);

            // курсор на каждый из созданных файлов и чтение по одной строке + сортировка и запись
            var cursors = batchFileNames.Select(x => new FileCursor(x)).ToList();
            await Task.WhenAll(cursors.Select(x => x.ReadLineAsync()));

            await using var outFile = File.OpenWrite(outFileName);
            await using var outWriter = new StreamWriter(outFile);

            var i = 0;
            var prevWrite = Task.CompletedTask;
            while (cursors.Any())
            {
                var c = cursors.OrderBy(x => x.LastLine.Text)
                    .ThenBy(x => x.LastLine.Number)
                    .First();

                await prevWrite;
                prevWrite = outWriter.WriteLineAsync(c.LastLine.ToString());
                i++;

                if (i % _options.BatchSize == 0)
                {
                    await prevWrite;
                    await outWriter.FlushAsync();
                }

                if (c.EndOfFile)
                {
                    cursors.Remove(c);
                    c.Dispose();
                    File.Delete(c.FileName);
                }
                else
                {
                    await c.ReadLineAsync();
                }
            }
        }

        private class FileCursor : IDisposable
        {
            private readonly StreamReader _reader;
            private Line _lastLine;

            public string FileName { get; }
            public Line LastLine => _lastLine;
            public bool EndOfFile => _reader.EndOfStream;

            public FileCursor(string fileName)
            {
                FileName = fileName;
                _reader = File.OpenText(fileName);
            }

            public async Task<Line> ReadLineAsync()
            {
                var lineStr = await _reader.ReadLineAsync();
                _lastLine = Line.Parse(lineStr);

                return _lastLine;
            }

            public void Dispose()
            {
                _reader.Dispose();
            }
        }

        private string CreateBatchFileName(string srcFileName, int batchIndex)
        {
            var dirName = Path.GetDirectoryName(srcFileName);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(srcFileName);
            var ext = Path.GetExtension(srcFileName);

            var fileName = $"{fileNameWithoutExt}_{batchIndex}{ext}";

            return dirName == null
                ? fileName
                : Path.Combine(dirName, fileName);
        }

        private async Task<Batch> ReadBatch(string fileName, long offset)
        {
            await using var file = File.OpenRead(fileName);
            file.Position = offset;

            using var reader = new StreamReader(file);

            var batchSize = _options.BatchSize;

            long byteCount = 0;
            var lines = new List<Line>(batchSize);
            var i = 0;
            while (i < batchSize && !reader.EndOfStream)
            {
                var lineStr = await reader.ReadLineAsync();

                lines.Add(Line.Parse(lineStr));

                // ReSharper disable once AssignNullToNotNullAttribute
                byteCount += reader.CurrentEncoding.GetByteCount(lineStr)
                             + reader.CurrentEncoding.GetByteCount(Environment.NewLine);
                i++;
            }

            return new Batch(lines, byteCount);
        }

        private async Task WriteBatch(string fileName, long offset, IEnumerable<string> lines, CancellationToken ct)
        {
            await using var file = File.OpenWrite(fileName);
            file.Position = offset;

            await using var writer = new StreamWriter(file);

            foreach (var line in lines)
            {
                ct.ThrowIfCancellationRequested();

                await writer.WriteLineAsync(line);
            }
        }

        private class Batch
        {
            public IEnumerable<Line> Lines { get; }
            public long ByteCount { get; }

            public Batch(IEnumerable<Line> lines, long byteCount)
            {
                Lines = lines;
                ByteCount = byteCount;
            }
        }
    }
}