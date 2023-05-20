using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;

namespace GZip
{
    /// <summary>Структура для хранения пар имён.</summary>
    struct FileNames
    {
        public String inputFileName;
        public String outputFileName;
    }

    class Program
    {
        static int Main(string[] args)
        {
            List<FileNames> fileNames = new List<FileNames>();
            if ((args.Length < 3 || args.Length % 2 != 1) && (args[0] != "compress" || args[0] != "decompress"))
            {
                Console.WriteLine("Использование: GZipTest.exe compress [имя исходного файла] [имя архива] [имя исходного файла] [имя архива] ... [имя исходного файла] [имя архива]");
                Console.WriteLine("GZipTest.exe decompress [имя архива] [имя выходного файла] [имя архива] [имя выходного файла] ... [имя архива] [имя выходного файла]");
                return 1;
            }

            for (var i = 1; i < args.Count(); i += 2)
            {
                if (File.Exists(args[i]))
                    fileNames.Add(new FileNames() { inputFileName = args[i], outputFileName = args[i + 1] });
                else
                {
                    Console.WriteLine($"Исходный файл \"{args[i]}\" не существует");
                    Console.ReadKey();
                    return 1;
                }
            }
            if (fileNames.Count != (args.Length - 1) / 2) return 1;
            var timer = new Stopwatch();
            timer.Start();
            GZip gz = new GZip(fileNames, args[0]);
            timer.Stop();
            TimeSpan timeTaken = timer.Elapsed;
            Console.WriteLine("Время выполнения: " + timeTaken.ToString(@"m\:ss\.fff"));
            Console.WriteLine("Готово");
            Console.ReadKey();
            return 0;
        }
    }
}
