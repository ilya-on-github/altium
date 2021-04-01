using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Altium.Sort
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string fileName;
            if (args.Length < 1)
            {
                Console.Write("File name: ");

                fileName = Console.ReadLine();
            }
            else
            {
                fileName = args[0];
            }

            if (!File.Exists(fileName))
            {
                Console.WriteLine($"File '{fileName}' doesn't exist.");
            }

            var options = Options.Create(new FileSorterOptions
            {
                BatchSize = 50000
            });

            var sorter = new FileSorter(options);
            var outFileName = FileSorter.DefaultOutFileName(fileName);

            var sw = new Stopwatch();
            sw.Start();

            Task.WaitAll(sorter.Sort(fileName, outFileName, CancellationToken.None));

            sw.Stop();

            Console.WriteLine($"Sorted. Output: '{outFileName}', elapsed: {sw.Elapsed.TotalSeconds:0.000} s.");
        }
    }
}