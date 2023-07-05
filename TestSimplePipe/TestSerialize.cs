using Abt.Comm.SimplePipe;
using System;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;

namespace TestSimplePipe
{
    [TestClass]
    public class TestSerialize
    {
        [TestMethod]
        public void Serialize()
        {
            var expected = "ABCDEFGHIJKLMNOPQRSTUBWXYZ";
            var encoding = new UnicodeEncoding(false, false);
            var buffer = new ConsumeBuffer(encoding.GetBytes(expected));
            var splitSize = 10 * sizeof(char);

            var itr = Serializer.Serialize(buffer, splitSize).GetEnumerator();
            {
                Assert.IsTrue(itr.MoveNext());
                var packet = itr.Current;
                Assert.IsFalse(packet.Data.IsEmpty);
                Assert.AreEqual("ABCDEFGHIJ", encoding.GetString(packet.Data.Span));
                Assert.IsTrue(packet.Header.IsStart);
                Assert.IsFalse(packet.Header.IsEnd);
                Assert.IsFalse(packet.Header.IsCancnel);
                Assert.AreEqual((int)packet.Header.Size, packet.Header.DataOffset + splitSize);
                Assert.AreEqual(Header.HeaderLength, (int)packet.Header.DataOffset);
                Assert.AreEqual(splitSize, (int)packet.Header.DataSize);
            }
            {
                Assert.IsTrue(itr.MoveNext());
                var packet = itr.Current;
                Assert.IsFalse(packet.Data.IsEmpty);
                Assert.AreEqual("KLMNOPQRST", encoding.GetString(packet.Data.Span));
                Assert.IsFalse(packet.Header.IsStart);
                Assert.IsFalse(packet.Header.IsEnd);
                Assert.IsFalse(packet.Header.IsCancnel);
                Assert.AreEqual((int)packet.Header.Size, packet.Header.DataOffset + splitSize);
                Assert.AreEqual(Header.HeaderLength, (int)packet.Header.DataOffset);
                Assert.AreEqual(splitSize, (int)packet.Header.DataSize);
            }
            {
                var tailSize = 6 * sizeof(char);
                Assert.IsTrue(itr.MoveNext());
                var packet = itr.Current;
                Assert.IsFalse(packet.Data.IsEmpty);
                Assert.AreEqual("UBWXYZ", encoding.GetString(packet.Data.Span));
                Assert.IsFalse(packet.Header.IsStart);
                Assert.IsTrue(packet.Header.IsEnd);
                Assert.IsFalse(packet.Header.IsCancnel);
                Assert.AreEqual((int)packet.Header.Size, packet.Header.DataOffset + tailSize);
                Assert.AreEqual(Header.HeaderLength, (int)packet.Header.DataOffset);
                Assert.AreEqual(tailSize, (int)packet.Header.DataSize);
            }
            Assert.IsFalse(itr.MoveNext());
        }

        [TestMethod]
        public void SerializeSingle()
        {
            var expected = "ABCDEFGHIJKLMNOPQRSTUBWXYZ";
            var encoding = new UnicodeEncoding(false, false);
            var buffer = new ConsumeBuffer(encoding.GetBytes(expected));
            var splitSize = 26 * sizeof(char);
            var itr = Serializer.Serialize(buffer, splitSize).GetEnumerator();
            {
                Assert.IsTrue(itr.MoveNext());
                var packet = itr.Current;
                Assert.IsFalse(packet.Data.IsEmpty);
                Assert.AreEqual("ABCDEFGHIJKLMNOPQRSTUBWXYZ", encoding.GetString(packet.Data.Span));
                Assert.IsTrue(packet.Header.IsStart);
                Assert.IsTrue(packet.Header.IsEnd);
                Assert.IsFalse(packet.Header.IsCancnel);
                Assert.AreEqual((int)packet.Header.Size, packet.Header.DataOffset + splitSize);
                Assert.AreEqual(Header.HeaderLength, (int)packet.Header.DataOffset);
                Assert.AreEqual(splitSize, (int)packet.Header.DataSize);
            }
            Assert.IsFalse (itr.MoveNext());
        }

        [TestMethod]
        public void SerializeEmpty()
        {
            var buffer = new ConsumeBuffer(new byte[0]);
            var itr = Serializer.Serialize(buffer, 10).GetEnumerator();
            Assert.IsFalse(itr.MoveNext());
        }
    }

    [TestClass]
    public class TestDeserializer
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
            return new(header, bytes.AsMemory());
        }

        [TestMethod]
        public void Deserialize()
        {
            var encoding = new UnicodeEncoding(false, false);
            var expect = new[]
            {
                "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
                "abcdefghijklmnopqrstuvwxyz",
            };

            var deserializer = new Deserializer(1024, 1024);

            var acutal = new List<string>();
            var splitSize = 10 * sizeof(char);
            var count = 0;
            foreach( var packet in expect.Select(e=>Serializer.Serialize(new ConsumeBuffer(encoding.GetBytes(e)), splitSize)).SelectMany(e=>e))
            {
                var buffer = deserializer.Feed(packet);
                if (!buffer.IsEmpty)
                {
                    acutal.Add(encoding.GetString(buffer.Span));
                }
                ++count;
            }
            Assert.AreEqual(6, count);
            Assert.IsTrue(Enumerable.SequenceEqual(expect, acutal));
        }

        [TestMethod]
        public void DeserializeSingle()
        {
            var encoding = new UnicodeEncoding(false, false);
            var expect = new[]
            {
                "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
                "abcdefghijklmnopqrstuvwxyz",
            };

            var deserializer = new Deserializer(1024, 1024);

            var acutal = new List<string>();
            var splitSize = expect.Select(e => encoding.GetByteCount(e)).Max();
            var count = 0;
            foreach (var packet in expect.Select(e => Serializer.Serialize(new ConsumeBuffer(encoding.GetBytes(e)), splitSize)).SelectMany(e => e))
            {
                var buffer = deserializer.Feed(packet);
                if (!buffer.IsEmpty)
                {
                    acutal.Add(encoding.GetString(buffer.Span));
                }
                ++count;
            }
            Assert.AreEqual(2, count);
            Assert.IsTrue(Enumerable.SequenceEqual(expect, acutal));
        }

        [TestMethod]
        public void DeserializeCancel()
        {
            var encoding = new UnicodeEncoding(false, false);
            var expect = new[]
            {
                "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
                "abcdefghijklmnopqrstuvwxyz",
            };

            var deserializer = new Deserializer(1024, 1024);

            var actual = new List<string>();
            var splitSize = 10 * sizeof(char);
            var count = 0;
            foreach (var packet in expect.Select(e => Serializer.Serialize(new ConsumeBuffer(encoding.GetBytes(e)), splitSize)).SelectMany(e => e))
            {
                if(count == 1){
                    deserializer.Feed(Packet.CancelPacket);
                    break;
                }
                else
                {
                    var buffer = deserializer.Feed(packet);
                    if (!buffer.IsEmpty)
                    {
                        actual.Add(encoding.GetString(buffer.Span));
                    }
                }
                ++count;
            }
            Assert.AreEqual(1, count);
            Assert.AreEqual(0, actual.Count);

            //キャンセル後に改めて実行しても動作するはず。
            count = 0;
            actual.Clear();
            foreach (var packet in expect.Select(e => Serializer.Serialize(new ConsumeBuffer(encoding.GetBytes(e)), splitSize)).SelectMany(e => e))
            {
                var buffer = deserializer.Feed(packet);
                if (!buffer.IsEmpty)
                {
                    actual.Add(encoding.GetString(buffer.Span));
                }
                ++count;
            }
            Assert.AreEqual(6, count);
            Assert.IsTrue(Enumerable.SequenceEqual(expect, actual));
        }
    }
}
