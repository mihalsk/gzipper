using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace GZip
{
    class Compressor : IRunnable
    {
        static Object lockIn = new Object();
        static Object lockOut = new Object();
        readonly BlockingCollection<byte[]> _readerOutputQue;
        readonly BlockingCollection<byte[]> _compressorOutputQue;
        static readonly EventQueue<byte[]> _compressorInWorkQue = new EventQueue<byte[]>();
        AutoResetEvent _compressorInWorkQueWaitHandler = new AutoResetEvent(true);

        public Compressor(BlockingCollection<byte[]> readerOutputQue, BlockingCollection<byte[]> compressorOutputQue)
        {
            _readerOutputQue = readerOutputQue;
            _compressorOutputQue = compressorOutputQue;
            _compressorInWorkQue.Dequeued += _compressorInWorkQue_Dequeued;
        }

        private void _compressorInWorkQue_Dequeued(object sender, EventArgs e)
        {
            _compressorInWorkQueWaitHandler.Set();
        }

        public void Run()
        {
            while (_readerOutputQue != null && !_readerOutputQue.IsCompleted)
            {
                try
                {
                    byte[] buffer;
                    lock (lockIn)
                    {
                        buffer = _readerOutputQue.Take(); // читаем по порядку
                        _compressorInWorkQue.Enqueue(buffer); // пихаем в рабочую очередь - в том же порядке?
                    }
                    var gzBuffer = Compress(buffer);
                    byte[] temp;
                    while (_compressorInWorkQue.TryPeek(out temp)) // проверяем наш ли кусок в начале очереди?
                    {
                        if (temp != buffer) // не наш кусок
                        {
                            //_compressorInWorkQueWaitHandler.Reset();
                            _compressorInWorkQueWaitHandler.WaitOne(); // стопоримся до события по Dequeue
                            Console.WriteLine("not InWorkQue.");
                        }
                        else // наш кусок - извлекаем
                        {
                            //byte[] temp2;
                            lock (lockOut)
                            {
                                if (_compressorInWorkQue.TryDequeue(out temp)) // извлекаем (удаляем из рабочей очереди несжатый)
                                {
                                    _compressorOutputQue.Add(gzBuffer); // пихаем сжатый
                                    Console.WriteLine("compress block success");
                                    break;
                                }
                                else
                                    Console.WriteLine("_compressorInWorkQue.TryDequeue error");
                                ///break;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Ошибка Compressor.Run: {e.Message}");
                }
            }
            lock (lockOut)
                if (_compressorInWorkQue.IsEmpty)
                    _compressorOutputQue.CompleteAdding();
            Console.WriteLine("compressor end");
        }

        private byte[] Compress(byte[] data)
        {
            MemoryStream output = new MemoryStream();
            using (GZipStream dstream = new GZipStream(output, CompressionLevel.Optimal))
            {
                dstream.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }
    }
}
