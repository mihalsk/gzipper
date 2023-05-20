using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GZip
{
    class Parser : IRunnable
    {
        static Object lockIn = new Object();
        static Object lockOut = new Object();

        public static readonly byte[] gzipMemberHeader = new byte[10] { 0x1f, 0x8b, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00 };
        //static readonly byte[] gzipMemberHeader = new byte[10] { 0x1f, 0x8b, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0a }; // .Net Core 3.1 CompressionLevel.Optimal
        readonly Dictionary<byte, byte> gzipMemberHeaderOffsets = new Dictionary<byte, byte> { { 0x1f, 9 }, { 0x8b, 8 }, { 0x08, 7 }, { 0x00, 2 }, { 0x04, 1 } };
        //static readonly Dictionary<byte, byte> gzipMemberHeaderOffsets = new Dictionary<byte, byte> { { 0x1f, 9 }, { 0x8b, 8 }, { 0x08, 7 }, { 0x00, 2 }, { 0x04, 1 }, { 0x0a, 10 } }; // .Net Core 3.1 CompressionLevel.Fastest
        readonly BlockingCollection<byte[]> _readerOutputQue;
        readonly BlockingCollection<byte[]> _parserOutputQue;
        public static EventQueue<byte[]> _parserInWorkQue = new EventQueue<byte[]>();
        AutoResetEvent parserInWorkQueWaitHandler = new AutoResetEvent(true);

        static byte[] tail = new byte[0];
        public Parser(BlockingCollection<byte[]> readerOutputQue, BlockingCollection<byte[]> parserOutputQue)
        {
            _readerOutputQue = readerOutputQue;
            _parserOutputQue = parserOutputQue;
            _parserInWorkQue.Dequeued += _parserInWorkQue_Dequeued;
        }

        private void _parserInWorkQue_Dequeued(object sender, EventArgs e)
        {
            parserInWorkQueWaitHandler.Set();
        }

        public void Run()
        {

            while (!_readerOutputQue.IsCompleted)
            {
                //try
                {
                    int offset = 0;
                    int oldPos = 0;
                    byte[] buffer;
                    lock (lockIn)
                    {
                        buffer = _readerOutputQue.Take(); // взяли блок
                        _parserInWorkQue.Enqueue(buffer);
                    }
                    //var gzBuffer = Compress(buffer);
                    while (offset < buffer.Length)
                    {
                        var curSignPos = FindPattern(buffer, offset, gzipMemberHeader);
                        //Console.WriteLine(curSignPos);
                        if (curSignPos == -1) // не нашли образец в блоке - скидываем в "хвост"
                        {

                            byte[] temp;
                            while (_parserInWorkQue.TryPeek(out temp))
                            {
                                if (temp != buffer)
                                {
                                    //parserInWorkQueWaitHandler.Reset();
                                    parserInWorkQueWaitHandler.WaitOne();
                                }
                                else
                                {
                                    lock (lockOut)
                                    {
                                        if (_parserInWorkQue.TryDequeue(out temp))
                                        {
                                            var tailPos = tail.Length;
                                            Array.Resize(ref tail, tail.Length + buffer.Length - oldPos);
                                            Array.Copy(buffer, oldPos, tail, tailPos, buffer.Length - oldPos);
                                            //Console.WriteLine($"b {tail[0]} {tail[1]} {tail[2]} {tail[3]} {tail[4]} {tail[5]} {tail[6]} {tail[7]} {tail[8]} {tail[9]} {tail[10]} {tail[11]}");
                                            if (_readerOutputQue.IsCompleted && _parserInWorkQue.IsEmpty) _parserOutputQue.Add((byte[])tail.Clone());

                                            //_parserOutputQue.Add(tail); //
                                            //Console.WriteLine("in tail");

                                            break;
                                        }
                                        else
                                            Console.WriteLine("_parserInWorkQue.TryDequeue error");
                                    }
                                }
                            }
                            break;
                        }
                        else // нашли образец
                        {
                            if (oldPos == 0) // первый найденый образец - дописываем всё что до образца в "хвост" и скидываем его("хвост") в выходную очередь 
                            {
                                //tail = buffer.Take(curSignPos).ToArray();
                                //byte[] _tail = new byte[0];
                                if (curSignPos == 0)
                                {
                                    offset = (curSignPos + gzipMemberHeader.Length);
                                    continue;
                                }
                                byte[] temp;
                                while (_parserInWorkQue.TryPeek(out temp))
                                {
                                    if (temp != buffer)
                                    {
                                        //parserInWorkQueWaitHandler.Reset();
                                        parserInWorkQueWaitHandler.WaitOne();
                                    }
                                    else
                                    {
                                        lock (lockOut)
                                        {
                                            var tailPos = tail.Length;
                                            //Console.WriteLine($"*len {tail.Length}");
                                            Array.Resize(ref tail, tail.Length + curSignPos);
                                            Array.Copy(buffer, 0, tail, tailPos, curSignPos);
                                            //Console.WriteLine($"e {tail[0]} {tail[1]} {tail[2]} {tail[3]} {tail[4]} {tail[5]} {tail[6]} {tail[7]} {tail[8]} {tail[9]} {tail[10]} {tail[11]}");
                                            _parserOutputQue.Add((byte[])tail.Clone());
                                            tail = new byte[0];

                                        }
                                        //Console.WriteLine("tail in que");
                                        break;
                                    }
                                }

                            }
                            else // не первый найденый образец - всё что между предыдущим найдёнышем и текущим скидываем в выходную очередь
                            {

                                byte[] block = new byte[curSignPos - oldPos];
                                byte[] temp;
                                while (_parserInWorkQue.TryPeek(out temp))
                                {
                                    if (temp != buffer)
                                    {
                                        //parserInWorkQueWaitHandler.Reset();
                                        parserInWorkQueWaitHandler.WaitOne();
                                    }
                                    else
                                    {
                                        //Console.WriteLine($"bl {block[0]} {block[1]} {block[2]} {block[3]} {block[4]} {block[5]} {block[6]} {block[7]} {block[8]} {block[9]} {block[10]} {block[11]}");
                                        lock (lockOut)
                                        {
                                            Array.Copy(buffer, oldPos, block, 0, curSignPos - oldPos);
                                            _parserOutputQue.Add((byte[])block.Clone());
                                        }
                                        //Console.WriteLine("block in que");
                                        break;
                                    }
                                }
                            }
                            oldPos = curSignPos;
                            offset = (curSignPos + gzipMemberHeader.Length);
                        }
                    }
                }
                //catch (Exception e)
                {
                    //    Console.WriteLine($"Ошибка Parser.Run: {e.Message}");
                }
            }
            lock (lockOut)
                if (_parserInWorkQue.IsEmpty)
                {
                    _parserOutputQue.CompleteAdding();
                    tail = new byte[0];
                }
            //Console.WriteLine("parser end");
        }

        /// <summary>Ищет образец в потоке начиная с указанного смещения по алгоритму Бойера-Мура-Хорспула.</summary>
        /// <param name=”stream”>Поток в котором ищем.</param>
        /// <param name=”offset”>Смещение с которого начинаем поиск.</param>
        /// <param name=”pattern”>Образец для поиска.</param>
        /// <value>Если образец найден то <c>позиция образца</c>, иначе <c>-1<c>.</value>
        int FindPattern(byte[] stream, int offset, byte[] pattern)
        {
            bool found = false;
            int curOffset = 0;
            int filePos = offset + pattern.Length - 1;
            var index = filePos;
            while (filePos < stream.Length)
            {
                int k = 0;
                for (int i = pattern.Length - 1; i >= 0; i--)
                {
                    index = filePos - k;
                    var currentByte = stream[index];
                    if (pattern[i] != currentByte)
                    {
                        if (i == pattern.Length - 1)
                            curOffset = gzipMemberHeaderOffsets.ContainsKey(currentByte) ? gzipMemberHeaderOffsets[currentByte] : pattern.Length;
                        else
                            curOffset = gzipMemberHeaderOffsets[pattern[i]];
                        filePos += curOffset;
                        break;
                    }
                    k += 1;
                    if (i == 0) found = true;
                }
                if (found)
                {
                    var pos = filePos - k + 1;
                    return pos;
                }
            }
            return -1;
        }
    }
}
