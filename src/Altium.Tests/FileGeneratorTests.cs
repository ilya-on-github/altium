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

        [TestCase(1000, "out/1GB.txt")]
        [TestCase(10000, "out/10GB.txt")]
        public async Task CreateFile_ProducesFileOfDesiredLength(long sizeMb, string outFileName)
        {
            // arrange
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
            var stats = await generator.CreateFile(outFileName, CancellationToken.None);

            // assert
            sw.Stop();

            Assert.IsTrue(new FileInfo(outFileName).Length > options.DesiredFileLength);

            var waitAvg = stats.WaitTimes.Any() ? stats.WaitTimes.Average(x => x.TotalSeconds) : 0;
            var writeAvg = stats.WriteTimes.Any() ? stats.WriteTimes.Average(x => x.TotalSeconds) : 0;
            var genAvg = stats.GenTimes.Any() ? stats.GenTimes.Average(x => x.TotalSeconds) : 0;

            Assert.Pass(
                $"Total: {sw.Elapsed.TotalSeconds:0.000} s, batch count: {stats.BatchCount}, write avg: {writeAvg:0.000} s, batch gen avg: {genAvg:0.000} s, wait avg: {waitAvg:0.000} s.");
        }
    }
}