using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GZip
{
    class GZip
    {
        /// <summary>Размер блока для чтения.</summary>
        public const int BufferSize = 0x700000; //0x700000; 7340032 12  5242880 92
        const int bcMaxSize = 15;
        /// <summary>Заголовок gzip в .Net .</summary>
        static readonly byte[] gzipMemberHeader = new byte[10] { 0x1f, 0x8b, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00 }; // .Net Framework
        //static readonly byte[] gzipMemberHeader = new byte[10] { 0x1f, 0x8b, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0a }; // .Net Core 3.1 CompressionLevel.Optimal
        /// <summary>Словарь смещений для байтов заголовка (для алгоритма Бойера-Мура-Хорспула).</summary>
        static readonly Dictionary<byte, byte> gzipMemberHeaderOffsets = new Dictionary<byte, byte> { { 0x1f, 9 }, { 0x8b, 8 }, { 0x08, 7 }, { 0x00, 2 }, { 0x04, 1 } }; // .Net Framework
        //static readonly Dictionary<byte, byte> gzipMemberHeaderOffsets = new Dictionary<byte, byte> { { 0x1f, 9 }, { 0x8b, 8 }, { 0x08, 7 }, { 0x00, 2 }, { 0x04, 1 }, { 0x0a, 10 } }; // .Net Core 3.1 CompressionLevel.Fastest
        /// <summary>Максимальное число потоков обработки.</summary>
        int MaxThreadCount = Environment.ProcessorCount;
        /// <summary>Текущее число потоков обработки.</summary>
        static volatile int curentThreadCount = 0;
        /// <summary>Очереди блоков.</summary>
        private readonly BlockingCollection<byte[]> readerOutputQue;

        private readonly BlockingCollection<byte[]> parserOutputQue;

        private readonly BlockingCollection<byte[]> compressorOutputQue;

        private readonly BlockingCollection<byte[]> decompressorOutputQue;

        public GZip(List<FileNames> fileNames, string args0)
        {
            if (args0 == "compress")
            {
                foreach (var fileName in fileNames)
                {
                    readerOutputQue = new BlockingCollection<byte[]>(bcMaxSize);
                    compressorOutputQue = new BlockingCollection<byte[]>(bcMaxSize);
                    Console.WriteLine($"{fileName.inputFileName} - {fileName.outputFileName}");
                    List<IRunnable> workers = new List<IRunnable>();
                    workers.Add(new Reader(fileName.inputFileName, readerOutputQue));
                    for (var i = 1; i <= MaxThreadCount; i++)
                    {
                        workers.Add(new Compressor(readerOutputQue, compressorOutputQue));
                    }
                    workers.Add(new Writer(compressorOutputQue, fileName.outputFileName));

                    List<Thread> threads = new List<Thread>();
                    foreach (var worker in workers)
                    {
                        threads.Add(new Thread(() => { worker.Run(); }));
                    }
                    foreach (var thread in threads)
                    {
                        thread.Start();
                    }
                    foreach (var thread in threads)
                    {
                        thread.Join();
                    }
                    workers.Clear();
                    readerOutputQue.Dispose();
                    compressorOutputQue.Dispose();
                }
            }

            if (args0 == "decompress")
            {
                foreach (var fileName in fileNames)
                {
                    readerOutputQue = new BlockingCollection<byte[]>(bcMaxSize);
                    parserOutputQue = new BlockingCollection<byte[]>(bcMaxSize);
                    decompressorOutputQue = new BlockingCollection<byte[]>(bcMaxSize);
                    Console.WriteLine($"{fileName.inputFileName} - {fileName.outputFileName}");
                    List<IRunnable> workers = new List<IRunnable>();
                    workers.Add(new Reader(fileName.inputFileName, readerOutputQue));
                    for (var i = 1; i <= MaxThreadCount; i += 1) // MaxThreadCount
                    {
                        workers.Add(new Parser(readerOutputQue, parserOutputQue));
                    }
                    for (var i = 1; i <= MaxThreadCount; i += 1) // MaxThreadCount
                    {
                        workers.Add(new Decompressor(parserOutputQue, decompressorOutputQue));
                    }
                    workers.Add(new Writer(decompressorOutputQue, fileName.outputFileName));

                    List<Thread> threads = new List<Thread>();
                    foreach (var worker in workers)
                    {
                        threads.Add(new Thread(() => { worker.Run(); }));
                    }
                    foreach (var thread in threads)
                    {
                        thread.Start();
                    }
                    //foreach (var thread in threads)
                    //{
                    //    thread.Join();
                    //}
                    while (threads.Any(t => t.IsAlive))
                    {
                        Thread.Sleep(500);
                        Console.WriteLine($"r {readerOutputQue?.Count} / pIn {Parser._parserInWorkQue.Count} / p {parserOutputQue?.Count} / {{c {compressorOutputQue?.Count} / dIn {Decompressor._decompressorInWorkQue.Count} / d {decompressorOutputQue?.Count}}}");
                    }
                    threads.Clear();
                    workers.Clear();
                    readerOutputQue.Dispose();
                    parserOutputQue.Dispose();
                    decompressorOutputQue.Dispose();
                }
            }
        }
    }
}
