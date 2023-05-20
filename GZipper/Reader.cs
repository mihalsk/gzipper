using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

namespace GZip
{
    class Reader : IRunnable
    {
        //const int BufferSize = 0x700000; //0x700000;
        readonly string _inputfileName;
        readonly BlockingCollection<byte[]> _readerOutputQue;
        public Reader(string fileName, BlockingCollection<byte[]> readerOutputQue)
        {
            _inputfileName = fileName;
            _readerOutputQue = readerOutputQue;
        }

        public void Run()
        {
            using (FileStream originalFileStream = File.Open(_inputfileName, FileMode.Open))
            {
                int readCount;
                byte[] buffer = new byte[GZip.BufferSize];
                while ((readCount = originalFileStream.Read(buffer, 0, GZip.BufferSize)) > 0)
                {
                    if (readCount == GZip.BufferSize)
                    {
                        int i = 0;
                        while (Parser.gzipMemberHeader.Contains(buffer[buffer.Length - 1]))
                        {
                            originalFileStream.Seek(-Parser.gzipMemberHeader.Length, SeekOrigin.Current);
                            Array.Resize(ref buffer, buffer.Length - Parser.gzipMemberHeader.Length);
                            i++;
                        }
                        _readerOutputQue.Add((byte[])buffer.Clone());
                        buffer = new byte[GZip.BufferSize];
                        //Console.WriteLine($"read block success({i})");
                    }
                    else
                    {
                        Array.Resize(ref buffer, readCount);
                        _readerOutputQue.Add(buffer);
                        //Console.WriteLine("read block success resize");
                        break;
                    }
                }
            }
            _readerOutputQue.CompleteAdding();
            //Console.WriteLine("reader end");
        }
    }
}
