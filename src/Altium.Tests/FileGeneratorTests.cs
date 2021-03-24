using System;
using System.Diagnostics;
using System.IO;
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
            long sizeMb = 10_000;

            // UTF-8: 1 symbol = 1 byte
            // 1024 symbols = 1024 bytes
            // Memory available = 2 to 2.5 GB
            // which is 2 000 000 000 to 2 500 000 000 bytes

            var options = new FileGeneratorOptions
            {
                DesiredFileLength = sizeMb * 1_000_000,
                BatchSize = 1_000_000
            };

            _fixture.Register(() => options);
            var generator = _fixture.Create<FileGenerator>();

            var sw = new Stopwatch();
            sw.Start();

            // act
            await generator.CreateFile(fileName, CancellationToken.None);

            // assert
            sw.Stop();
            Debug.WriteLine($"{sizeMb} MB: {sw.Elapsed.TotalMilliseconds} ms");

            Assert.IsTrue(new FileInfo(fileName).Length > options.DesiredFileLength);
        }
    }
}