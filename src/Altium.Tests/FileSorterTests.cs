using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Altium.Tests
{
    [TestFixture]
    public class FileSorterTests
    {
        private Fixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _fixture = new Fixture();
            _fixture.Register(() => new FileSorter(Options.Create(_fixture.Create<FileSorterOptions>())));
        }

        [TestCase(@"files/example.txt", @"files/example_expected.txt")]
        public async Task Sort_ProducesCorrectResult(string inputFileName, string expectedOutputFileName)
        {
            // arrange
            var outFileName = $"{Guid.NewGuid()}.txt";
            var sorter = _fixture.Create<FileSorter>();

            // act
            await sorter.Sort(inputFileName, outFileName, CancellationToken.None);

            // assert
            await using var outputExpectedFile = File.OpenRead(expectedOutputFileName);
            using var outputExpectedReader = new StreamReader(outputExpectedFile);

            await using var outputActualFile = File.OpenRead(outFileName);
            using var outputActualReader = new StreamReader(outputActualFile);

            while (!outputExpectedReader.EndOfStream)
            {
                var expectedLine = await outputExpectedReader.ReadLineAsync();
                Assert.IsTrue(!outputActualReader.EndOfStream);
                var actualLine = await outputActualReader.ReadLineAsync();
                Assert.AreEqual(expectedLine, actualLine);
            }

            Assert.IsTrue(outputActualReader.EndOfStream);
        }
    }
}