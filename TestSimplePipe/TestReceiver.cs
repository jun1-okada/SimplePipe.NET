using Abt.Comm.SimplePipe;
using System.Runtime.InteropServices;
using System.Text;

namespace TestSimplePipe
{
    [TestClass]
    public class TestReceiver
    {

        private static ValueTuple<Header, ReadOnlyMemory<byte>> CreateTestData(string data)
        {
            var encoding = new UnicodeEncoding(false, false);

            var header = Header.Create(encoding.GetByteCount(data), true, true, false);
            var bytes = new byte[header.Size];
            var ptr = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.StructureToPtr(header, ptr, false);
                Marshal.Copy(ptr, bytes, 0, Header.HeaderLength);
                encoding.GetBytes(data);
                encoding.GetBytes(data.AsSpan(), bytes.AsSpan(Header.HeaderLength));
            }
            finally { Marshal.FreeHGlobal(ptr); }
            return new (header, bytes.AsMemory());
        }

        [TestMethod]
        public void NoDataPacket()
        {
            (var header, var bytes) = CreateTestData("");
            Assert.AreEqual(8, bytes.Length);

            var receiver = new Receiver(1024, 1024);
            var itr = receiver.Feed(bytes).GetEnumerator();
            Assert.IsTrue(itr.MoveNext());
            var packet = itr.Current;
            Assert.IsFalse(itr.MoveNext());
            Assert.AreEqual(header.Size, packet.Header.Size);
            Assert.AreEqual(header.DataOffset, packet.Header.DataOffset);
            Assert.AreEqual(header.IsStart, packet.Header.IsStart);
            Assert.AreEqual(header.IsEnd, packet.Header.IsEnd);
            Assert.AreEqual(header.IsCancnel, packet.Header.IsCancnel);
            Assert.IsTrue(packet.Data.IsEmpty);
            
        }

        [TestMethod]
        public void SinglePacket()
        {
            var encoding = new UnicodeEncoding(false, false);
            var expected = "ABCDE";
            (var header, var bytes) = CreateTestData(expected);
            var receiver = new Receiver(1024, 1024);
            var itr = receiver.Feed(bytes).GetEnumerator();
            Assert.IsTrue(itr.MoveNext());
            var packet = itr.Current;
            Assert.IsFalse(itr.MoveNext());
            Assert.AreEqual(header.Size, packet.Header.Size);
            Assert.AreEqual(header.DataOffset, packet.Header.DataOffset);
            Assert.AreEqual(header.IsStart, packet.Header.IsStart);
            Assert.AreEqual(header.IsEnd, packet.Header.IsEnd);
            Assert.AreEqual(header.IsCancnel, packet.Header.IsCancnel);
            Assert.AreEqual(expected, encoding.GetString(packet.Data.Span));
        }

        [TestMethod]
        public void MultiPacket()
        {
            var testData = new[]
            {
                "ABCDE",
                "FGHIJ",
                "KLMNO",
                "PRSTU",
                "VWXYZ",
            };
            var ms = new MemoryStream();
            foreach(var e in testData)
            {
                (var _, var bytes) = CreateTestData(e);
                ms.Write(bytes.Span);
            }
            var data = new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Position);
            var receiver = new Receiver(1024, 1024);
            var encoding = new UnicodeEncoding(false, false);
            var actual = receiver.Feed(data).Select(e => encoding.GetString(e.Data.Span)).ToArray();
            Assert.IsTrue(Enumerable.SequenceEqual(testData, actual));
        }

        [TestMethod]
        public void FragmentPacket()
        {
            var expected = "ABCDEFGHIJKLMNO";
            (var header, var bytes) = CreateTestData(expected);

            var remain = (int)header.Size;
            const int fragmentSize = 8;
            var receiver = new Receiver(1024, 1024);
            Packet? actual = null;
            while (remain > 0)
            {
                var size = Math.Min(fragmentSize, remain);
                foreach(var p in receiver.Feed(bytes.Slice(0, size)))
                {
                    if(p is not null)
                    {
                        actual = p;
                    }
                }
                bytes = bytes.Slice(size);
                remain -= size;
            }
            var encoding = new UnicodeEncoding(false, false);
            Assert.IsNotNull(actual);
            Assert.AreEqual(expected, encoding.GetString(actual.Data.Span));
        }

        [TestMethod]
        public void SplitHeaderPacket()
        {
            var expected = new[] {
                "ABCDE",
                "FGHIJ",
            };
            var ms = new MemoryStream();
            foreach(var e in expected)
            {
                (var _, var bytes) = CreateTestData(e);
                ms.Write(bytes.Span);
            }
            var buffer = new ConsumeBuffer(new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, (int)ms.Position));
            var receiver = new Receiver(1024, 1024);

            var encoding = new UnicodeEncoding(false, false);
            var actual = new List<string>();

            actual.AddRange(receiver.Feed(buffer.Consume(16)).Select(e => encoding.GetString(e.Data.Span)));
            actual.AddRange(receiver.Feed(buffer.Consume(1)).Select(e => encoding.GetString(e.Data.Span)));
            actual.AddRange(receiver.Feed(buffer.Consume()).Select(e => encoding.GetString(e.Data.Span)));
               
            Assert.IsTrue(Enumerable.SequenceEqual(expected, actual));
        }

        [TestMethod]
        public void ComplexPackets()
        {
            var expected = new[]
            {
                "ABCDE",
                "FGHIJKLMNO",
                "PQ",
                "RS",
                "TUVWXYZ",
            };
            var ms = new MemoryStream();
            foreach(var e in expected)
            {
                var (_, bytes) = CreateTestData(e);
                ms.Write(bytes.Span);
            }
            const int FeedSize = 16;
            var encoding = new UnicodeEncoding(false, false);

            int remain = (int)ms.Position;
            var buffer = new ConsumeBuffer(new ReadOnlyMemory<byte>(ms.GetBuffer(), 0, remain));
            var receiver = new Receiver(1024, 1024);
            var actual = new List<string>();
            while (remain > 0)
            {
                var size = Math.Min(remain, FeedSize);
                actual.AddRange(receiver.Feed(buffer.Consume(size)).Select(e=>encoding.GetString(e.Data.Span)));
                remain -= size;
            }
            Assert.IsTrue(Enumerable.SequenceEqual(expected, actual));
        }

        [TestMethod]
        public void LimitSizePacket()
        {
            var (_, bytes) = CreateTestData("ABCDE");
            var receiver = new Receiver(1024, 8);
            Assert.ThrowsException<InvalidDataException>(() =>
            {
                receiver.Feed(bytes).Count();
            });
        }
    }
}