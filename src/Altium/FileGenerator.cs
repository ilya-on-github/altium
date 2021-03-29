using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Altium
{
    public class FileGenerator
    {
        private readonly FileGeneratorOptions _options;
        private readonly Random _rnd = new Random();
        private string[] _vocabulary;
        private readonly object _sync = new object();

        public FileGenerator(IOptions<FileGeneratorOptions> options)
        {
            _options = options.Value;
        }

        public async Task<CreateFileStats> CreateFile(string outFileName, CancellationToken ct)
        {
            Init();

            long currentLength = 0;
            Task prevAppendTask = null;
            var batchCount = 0;

            var genTimes = new List<TimeSpan>();
            var writeTimes = new List<TimeSpan>();
            var waitTimes = new List<TimeSpan>();

            var genSw = new Stopwatch();
            var waitSw = new Stopwatch();

            while (currentLength < _options.DesiredFileLength)
            {
                genSw.Restart();

                var batch = CreateBatch();

                genTimes.Add(genSw.Elapsed);

                if (prevAppendTask != null)
                {
                    if (!prevAppendTask.IsCompleted)
                    {
                        waitSw.Restart();

                        await prevAppendTask;

                        waitTimes.Add(waitSw.Elapsed);
                    }

                    currentLength = new FileInfo(outFileName).Length;
                }

                if (currentLength < _options.DesiredFileLength)
                {
                    prevAppendTask = Task.Run(async () =>
                    {
                        var writeSw = new Stopwatch();
                        writeSw.Start();

                        await File.AppendAllLinesAsync(outFileName, batch, ct);

                        writeTimes.Add(writeSw.Elapsed);
                    }, ct);

                    batchCount++;
                }
            }

            return new CreateFileStats
            {
                BatchCount = batchCount,
                GenTimes = genTimes,
                WaitTimes = waitTimes,
                WriteTimes = writeTimes
            };
        }

        private string[] CreateBatch()
        {
            var size = _options.BatchSize;

            var batch = new string[size];

            for (var i = 0; i < size; i++)
            {
                string currentLine;

                // При таком подходе макс расстояние между дубликатами строк в файле
                // будет ограничено длиной очереди.
                var useExistingLine = _rnd.Next(0, 100) < _options.ReuseLineChance;
                if (useExistingLine && i != 0)
                {
                    currentLine = batch[_rnd.Next(0, i)];
                }
                else
                {
                    currentLine = CreateLine().ToString();
                }

                batch[i] = currentLine;
            }

            return batch;
        }

        private Line CreateLine()
        {
            var number = _rnd.Next(0, _options.NumberMaxValue + 1);
            var textMinLength = _rnd.Next(1, _options.TextMaxLength);
            var text = new StringBuilder();

            while (text.Length < textMinLength)
            {
                var word = _vocabulary[_rnd.Next(0, _vocabulary.Length)];

                if (text.Length > 0)
                {
                    text.Append(" ");
                }

                text.Append(word);
            }

            return new Line(number, text.ToString());
        }

        private void Init()
        {
            if (_vocabulary != null) return;

            lock (_sync)
            {
                if (_vocabulary != null) return;

                _vocabulary = new string[_options.VocabularyLength];

                var hashSet = new HashSet<string>();
                for (var i = 0; i < _options.VocabularyLength; i++)
                {
                    const int maxAttempts = 20;

                    var attempt = 0;
                    for (; attempt < maxAttempts; attempt++)
                    {
                        var wordLength = _rnd.Next(1, _options.MaxWordLength + 1);
                        var word = CreateWord(wordLength);

                        if (!hashSet.Contains(word))
                        {
                            _vocabulary[i] = word;
                            hashSet.Add(word);
                            break;
                        }
                    }

                    if (attempt == maxAttempts)
                    {
                        throw new IndexOutOfRangeException(
                            $"Can't create a new unique word after {maxAttempts} attempts.");
                    }
                }
            }
        }

        private static string CreateWord(int length)
        {
            var sb = new StringBuilder();

            var remainingLength = length;
            while (sb.Length < length)
            {
                var part = Guid.NewGuid().ToString("N");

                sb.Append(part.Length > remainingLength
                    ? part.Substring(0, remainingLength)
                    : part);
            }

            return sb.ToString();
        }
    }
}