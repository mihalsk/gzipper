using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GZip
{
    class Decompressor : IRunnable
    {
        static Object lockIn = new Object();
        static Object lockOut = new Object();
        readonly BlockingCollection<byte[]> _parserOutputQue;
        readonly BlockingCollection<byte[]> _decompressorOutputQue;
        public static readonly EventQueue<byte[]> _decompressorInWorkQue = new EventQueue<byte[]>();
        AutoResetEvent _decompressorInWorkQueWaitHandler = new AutoResetEvent(true);
        public Decompressor(BlockingCollection<byte[]> parserOutputQue, BlockingCollection<byte[]> decompressorOutputQue)
        {
            _parserOutputQue = parserOutputQue;
            _decompressorOutputQue = decompressorOutputQue;
            _decompressorInWorkQue.Dequeued += _decompressorInWorkQue_Dequeued;
        }

        private void _decompressorInWorkQue_Dequeued(object sender, EventArgs e)
        {
            _decompressorInWorkQueWaitHandler.Set();
        }

        public void Run()
        {
            while (!_parserOutputQue.IsCompleted)
            {
                byte[] buffer = new byte[0];
                try
                {
                    lock (lockIn)
                    {
                        buffer = _parserOutputQue.Take();
                        _decompressorInWorkQue.Enqueue(buffer);
                    }
                }
                catch (InvalidOperationException e)
                {
                    Console.WriteLine($"_parserOutputQue.Take() {e.Message}");
                    break;
                }
                var ungzBuffer = Decompress(buffer);
                byte[] temp;
                while (_decompressorInWorkQue.TryPeek(out temp))
                {
                    if (temp != buffer)
                    {
                        //_decompressorInWorkQueWaitHandler.Reset();
                        _decompressorInWorkQueWaitHandler.WaitOne();
                    }
                    else
                    {
                        lock (lockOut)
                        {
                            if (_decompressorInWorkQue.TryDequeue(out temp))
                            {
                                _decompressorOutputQue.Add(ungzBuffer);
                                //Console.WriteLine("decompressor block success");
                            }
                            else
                                Console.WriteLine("_decompressorInWorkQue.TryDequeue error");
                        }
                    }
                }
            }
            lock (lockOut)
                if (_decompressorInWorkQue.IsEmpty)
                    _decompressorOutputQue.CompleteAdding();
            //Console.WriteLine("decompressor end");
        }

        private byte[] Decompress(byte[] data, int bufferSize)
        {
            MemoryStream input = new MemoryStream(data);
            MemoryStream output = new MemoryStream();
            using (GZipStream dstream = new GZipStream(input, CompressionMode.Decompress))
            {
                dstream.CopyTo(output, bufferSize);
            }
            return output.ToArray();
        }
        private byte[] Decompress(byte[] data)
        {
            MemoryStream input = new MemoryStream(data);
            MemoryStream output = new MemoryStream();
            using (GZipStream dstream = new GZipStream(input, CompressionMode.Decompress))
            {
                dstream.CopyTo(output, data.Length);
            }
            return output.ToArray();
        }
    }
}
