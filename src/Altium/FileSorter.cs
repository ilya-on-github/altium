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

            var batchFileNames = await Split(fileName, _options.BatchSize, ct);

            await Merge(batchFileNames, outFileName, ct);

            batchFileNames.ForEach(File.Delete);
        }

        /// <summary>
        /// Splits a file into a number of sorted batches.
        /// </summary>
        /// <param name="fileName">File to split.</param>
        /// <param name="lineCount">Number of lines per single batch.</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private static async Task<List<string>> Split(string fileName, int lineCount, CancellationToken ct)
        {
            var fileLength = new FileInfo(fileName).Length;
            long bytesRead = 0;
            var batchCount = 0;

            var batchFileNames = new List<string>();

            var writeTasks = new List<Task>();
            while (bytesRead < fileLength)
            {
                ct.ThrowIfCancellationRequested();

                var batch = await ReadBatch(fileName, bytesRead, lineCount);

                bytesRead += batch.ByteCount;

                var batchFileName = CreateBatchFileName(fileName, batchCount);

                var writeBatch = WriteBatch(batchFileName, 0,
                    OrderByLine(batch.Lines, x => x)
                        .Select(x => x.ToString()), ct);

                writeTasks.Add(writeBatch);
                batchFileNames.Add(batchFileName);

                batchCount++;
            }

            await Task.WhenAll(writeTasks);

            return batchFileNames;
        }

        /// <summary>
        /// Produces a single sorted file from a number of sorted batches.
        /// </summary>
        /// <param name="fileNames">Batches to merge.</param>
        /// <param name="outFileName">Result file.</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private static async Task Merge(IEnumerable<string> fileNames, string outFileName, CancellationToken ct)
        {
            // курсор на каждый из созданных файлов и чтение по одной строке + сортировка и запись
            var cursors = fileNames.Select(x => new FileCursor(x)).ToList();

            await Task.WhenAll(cursors.Select(x => x.ReadLineAsync()));

            await using var outFile = File.OpenWrite(outFileName);
            await using var outWriter = new StreamWriter(outFile);

            var prevWrite = Task.CompletedTask;
            while (cursors.Any())
            {
                ct.ThrowIfCancellationRequested();

                // курсоры уже отсортированы
                var c = OrderByLine(cursors, x => x.CurrentLine)
                    .First();

                await prevWrite;
                prevWrite = outWriter.WriteLineAsync(c.CurrentLine.ToString());

                if (c.EndOfFile)
                {
                    cursors.Remove(c);
                    c.Dispose();
                }
                else
                {
                    await c.ReadLineAsync();
                }
            }
        }

        /// <summary>
        /// Reads a number of lines from file..
        /// </summary>
        /// <param name="fileName">File to read.</param>
        /// <param name="offset">Position to start from.</param>
        /// <param name="lineCount">Number of lines to read.</param>
        /// <returns></returns>
        private static async Task<Batch> ReadBatch(string fileName, long offset, int lineCount)
        {
            await using var file = File.OpenRead(fileName);
            file.Position = offset;

            using var reader = new StreamReader(file);

            long byteCount = 0;
            var lines = new List<Line>(lineCount);
            var i = 0;
            while (i < lineCount && !reader.EndOfStream)
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

        /// <summary>
        /// Writes lines to a file.
        /// </summary>
        /// <param name="fileName">File to write.</param>
        /// <param name="offset">Position to start from.</param>
        /// <param name="lines">Lines to write.</param>
        /// <param name="ct">You know.</param>
        /// <returns></returns>
        private static async Task WriteBatch(string fileName, long offset, IEnumerable<string> lines,
            CancellationToken ct)
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

        private static string CreateBatchFileName(string srcFileName, int batchIndex)
        {
            var dirName = Path.GetDirectoryName(srcFileName);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(srcFileName);
            var ext = Path.GetExtension(srcFileName);

            var fileName = $"{fileNameWithoutExt}_{batchIndex}{ext}";

            return dirName == null
                ? fileName
                : Path.Combine(dirName, fileName);
        }

        private static IOrderedEnumerable<T> OrderByLine<T>(IEnumerable<T> src, Func<T, Line> lineSelector)
        {
            return src.OrderBy(x => lineSelector(x).Text)
                .ThenBy(x => lineSelector(x).Number);
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

        private class FileCursor : IDisposable
        {
            private readonly StreamReader _reader;
            private Line _lastRead;

            public string FileName { get; }
            public Line CurrentLine => _lastRead;
            public bool EndOfFile => _reader.EndOfStream;

            public FileCursor(string fileName)
            {
                FileName = fileName;
                _reader = File.OpenText(fileName);
            }

            public async Task<Line> ReadLineAsync()
            {
                var lineStr = await _reader.ReadLineAsync();
                _lastRead = Line.Parse(lineStr);

                return _lastRead;
            }

            public void Dispose()
            {
                _reader.Dispose();
            }
        }
    }
}