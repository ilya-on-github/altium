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

        [TestCase(@"files/example.txt", @"files/example_expected.txt", 2)]
        // [TestCase(@"files/example_1GB.txt", @"files/example_expected.txt", 50000)]
        public async Task Sort_ProducesCorrectResult(string inputFileName, string expectedOutputFileName, int batchSize)
        {
            // arrange
            var options = new FileSorterOptions
            {
                BatchSize = batchSize
            };
            _fixture.Register(() => options);
            var outFileName = CreateOutFileName(inputFileName);
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

        // [TestCase(@"files/1GB.txt", 50000)]
        // [TestCase(@"files/10GB.txt", 50000)]
        public void Sort_DoesntThrow(string fileName, int batchSize)
        {
            // arrange
            var options = new FileSorterOptions
            {
                BatchSize = batchSize
            };
            _fixture.Register(() => options);
            var sorter = _fixture.Create<FileSorter>();
            var outFileName = CreateOutFileName(fileName);

            // assert
            Assert.DoesNotThrowAsync(async () =>
            {
                // act
                await sorter.Sort(fileName, outFileName, CancellationToken.None);
            });
        }

        private static string CreateOutFileName(string srcFileName)
        {
            var dirName = Path.GetDirectoryName(srcFileName);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(srcFileName);
            var ext = Path.GetExtension(srcFileName);

            var fileName = $"{fileNameWithoutExt}_sorted{ext}";

            return dirName == null
                ? fileName
                : Path.Combine(dirName, fileName);
        }
    }
}