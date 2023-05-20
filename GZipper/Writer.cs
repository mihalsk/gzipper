using System;
using System.Collections.Concurrent;
using System.IO;

namespace GZip
{
    class Writer : IRunnable
    {
        BlockingCollection<byte[]> _inputQue;
        string _outputFileName;
        //AutoResetEvent writerWaitHandler = new AutoResetEvent(true);
        public Writer(BlockingCollection<byte[]> inputQue, string outputFileName)
        {
            _inputQue = inputQue;
            _outputFileName = outputFileName;
        }

        public void Run()
        {
            using (FileStream outputFileStream = File.Open(_outputFileName, FileMode.Create))
            {
                while (!_inputQue.IsCompleted)
                {
                    try
                    {
                        byte[] buffer = _inputQue.Take();
                        outputFileStream.Write(buffer, 0, buffer.Length);
                        //Console.WriteLine("write block success");
                    }
                    catch (InvalidOperationException e)
                    {
                        Console.WriteLine($"Ошибка Writer.Run: {e.Message}");
                    }
                    //writerWaitHandler.WaitOne();
                }
            }
            Console.WriteLine("writer end");
        }
    }
}
