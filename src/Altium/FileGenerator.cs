using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Altium
{
    public class FileGenerator
    {
        private readonly FileGeneratorOptions _options;
        private readonly Random _rnd = new Random();

        public FileGenerator(IOptions<FileGeneratorOptions> options)
        {
            _options = options.Value;
        }

        public async Task CreateFile(string outFileName, CancellationToken ct)
        {
            var batchSize = _options.BatchSize;
            long currentLength;

            var cache = new List<Line>(_options.LineCacheSize);

            do
            {
                var batch = new string[batchSize];

                for (var i = 0; i < batchSize; i++)
                {
                    Line currentLine;
                    var useExistingLine = _rnd.Next(0, 100) < _options.ReuseLineChance;
                    if (useExistingLine && cache.Any())
                    {
                        currentLine = cache[_rnd.Next(0, cache.Count)];
                    }
                    else
                    {
                        currentLine = Line.Create(_options.NumberMaxValue, _options.TextMaxLength);
                    }

                    batch[i] = currentLine.ToString();

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
    }
}