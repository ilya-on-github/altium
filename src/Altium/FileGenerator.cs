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
        private readonly object _sync = new object();

        private readonly FileGeneratorOptions _options;
        private readonly Random _rnd = new Random();

        private string[] _vocabulary;

        public FileGenerator(IOptions<FileGeneratorOptions> options)
        {
            _options = options.Value;
        }

        public async Task CreateFile(string outFileName, CancellationToken ct)
        {
            InitVocabulary();

            var batchSize = _options.BatchSize;
            long currentLength;

            var cache = new List<string>(_options.LineCacheSize);

            do
            {
                var batch = new string[batchSize];

                for (var i = 0; i < batchSize; i++)
                {
                    string currentLine;
                    var useExistingLine = _rnd.Next(0, 100) < _options.ReuseLineChance;
                    if (useExistingLine && cache.Any())
                    {
                        currentLine = cache[_rnd.Next(0, cache.Count)];
                    }
                    else
                    {
                        currentLine = CreateLine();
                    }

                    batch[i] = currentLine;

                    if (cache.Count < _options.LineCacheSize)
                    {
                        cache.Add(currentLine);
                    }
                    else
                    {
                        cache[_rnd.Next(0, cache.Count)] = currentLine;
                    }
                }

                await File.AppendAllLinesAsync(outFileName, batch, ct);

                currentLength = new FileInfo(outFileName).Length;
            } while (currentLength < _options.DesiredFileLength);
        }

        private string CreateLine()
        {
            var number = _rnd.Next(0, _options.NumberMaxValue);
            var textDesiredLength = _rnd.Next(1, _options.TextMaxLength);

            var lineSb = new StringBuilder()
                .Append(number)
                .Append(".");

            var lineDesiredLength = lineSb.Length + textDesiredLength;

            while (lineSb.Length < lineDesiredLength)
            {
                var word = GetWord();

                // 1 - это длина пробела, которым отделяется слово
                if (lineSb.Length + 1 + word.Length > _options.TextMaxLength)
                {
                    // не добавляем слово, если длина строки превысит максимальную
                    break;
                }

                lineSb.Append(" ").Append(word);
            }

            return lineSb.ToString();
        }

        private string[] CreateVocabulary(int length)
        {
            return Enumerable.Range(0, length)
                .Select(x => Guid.NewGuid().ToString("N"))
                .ToArray();
        }

        private string GetWord()
        {
            return _vocabulary[_rnd.Next(0, _vocabulary.Length)];
        }

        private void InitVocabulary()
        {
            if (_vocabulary == null)
            {
                lock (_sync)
                {
                    if (_vocabulary == null)
                    {
                        _vocabulary = CreateVocabulary(_options.VocabularyLength);
                    }
                }
            }
        }
    }
}