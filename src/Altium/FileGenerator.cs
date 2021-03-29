using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public async Task CreateFile(string outFileName, CancellationToken ct)
        {
            Init();

            var batchSize = _options.BatchSize;
            long currentLength;

            var lastLines = new Queue<Line>(_options.LineCacheSize);

            do
            {
                var batch = new string[batchSize];

                for (var i = 0; i < batchSize; i++)
                {
                    Line currentLine;

                    // При таком подходе макс расстояние между дубликатами строк в файле
                    // будет ограничено длиной очереди.
                    var useExistingLine = _rnd.Next(0, 100) < _options.ReuseLineChance;
                    if (useExistingLine && lastLines.Any())
                    {
                        currentLine = lastLines.Dequeue();
                    }
                    else
                    {
                        currentLine = CreateLine();

                        lastLines.Enqueue(currentLine);

                        if (lastLines.Count > _options.LineCacheSize)
                        {
                            lastLines.Dequeue();
                        }
                    }

                    batch[i] = currentLine.ToString();
                }

                await File.AppendAllLinesAsync(outFileName, batch, ct);

                currentLength = new FileInfo(outFileName).Length;
            } while (currentLength < _options.DesiredFileLength);
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