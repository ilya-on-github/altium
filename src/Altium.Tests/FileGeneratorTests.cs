using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Altium.Tests
{
    [TestFixture]
    public class FileGeneratorTests
    {
        private Fixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _fixture = new Fixture();
            _fixture.Register(() => new FileGenerator(Options.Create(_fixture.Create<FileGeneratorOptions>())));
        }

        [Test]
        public async Task CreateFile_ProducesFileOfDesiredLength()
        {
            // arrange
            var fileName = $"{Guid.NewGuid():N}.txt";
            long sizeMb = 10000;

            // UTF-8: 1 symbol = 1 byte
            // 1024 symbols = 1024 bytes
            // Memory available = 2 to 2.5 GB
            // which is 2 000 000 000 to 2 500 000 000 bytes

            var options = new FileGeneratorOptions
            {
                DesiredFileLength = sizeMb * 1_000_000,
                BatchSize = 50000, // при значении выше выигрыш в производительности уже не значительный
            };

            _fixture.Register(() => options);
            var generator = _fixture.Create<FileGenerator>();

            var sw = new Stopwatch();
            sw.Start();

            // act
            var stats = await generator.CreateFile(fileName, CancellationToken.None);

            // assert
            sw.Stop();

            Assert.IsTrue(new FileInfo(fileName).Length > options.DesiredFileLength);

            var waitAvg = stats.WaitTimes.Any() ? stats.WaitTimes.Average(x => x.TotalSeconds) : 0;
            var writeAvg = stats.WriteTimes.Any() ? stats.WriteTimes.Average(x => x.TotalSeconds) : 0;
            var genAvg = stats.GenTimes.Any() ? stats.GenTimes.Average(x => x.TotalSeconds) : 0;

            // Запись должна занимать больше времени, чем генерация. В этом случае простои I/O будут минимальны.
            Assert.IsTrue(writeAvg > genAvg, $"genAvg: {genAvg:0.000} s, writeAvg: {writeAvg:0.000} s.");

            Assert.Pass(
                $"Total: {sw.Elapsed.TotalSeconds:0.000} s, batch count: {stats.BatchCount}, write avg: {writeAvg:0.000} s, batch gen avg: {genAvg:0.000} s, wait avg: {waitAvg:0.000} s.");
        }
    }
}