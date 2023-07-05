using Abt.Comm.SimplePipe;
using System;
using System.Linq;
using System.Text;

namespace TestSimplePipe
{
    public class EventCounter
    {
        private ManualResetEvent evt = new ManualResetEvent(false);
        private int count = 0;

        public void Set()
        {
            Interlocked.Increment(ref count);
            evt.Set();
        }
        public void Reset()
        {
            Interlocked.Decrement(ref count);
            evt.Reset();
        }
          
        public int Count => count;

        public ValueTuple<int, bool> Wait(int timeout)
        {
            var res = !evt.WaitOne(timeout);
            return (Count, res);
        }
        public ValueTuple<int, bool> Wait(TimeSpan timeout)
        {
            var res = !evt.WaitOne(timeout);
            return (Count, res);
        }

    }

    [TestClass]
    public class TestSimplePipe
    {
        private static UnicodeEncoding encoding = new UnicodeEncoding();
        private static string ToStr(ReadOnlySpan<byte> bytes) => encoding.GetString(bytes);
        private static byte[] ToBytes(string str) => encoding.GetBytes(str);

        private static ValueTuple<int, bool> WC(int n = 1, bool isTimeout = false) => new ValueTuple<int, bool>(n, isTimeout);

        [TestMethod]
        public void HelloEcho()
        {
            var serverConnected = new EventCounter();
            var serverDisconnected = new EventCounter();
            var serverClosed = new EventCounter();
            Exception? serverErr = null;

            var pipeName = Guid.NewGuid().ToString();
            var server = new SimpleNamedPipeServer(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Connected:
                        serverConnected.Set();
                        break;
                    case Disconnected:
                        serverDisconnected.Set();
                        break;
                    case Received received:
                        pipeEvent.Pipe.WriteAsync(ToBytes("echo: " + ToStr(received.Data.Span))).Wait();
                        break;
                    case Closed:
                        serverClosed.Set();
                        break;
                    case ExceptionTrapped ex:
                        serverErr = ex.Exception;
                        break;
                }
            });

            var echoComplete = new EventCounter();
            var clientDisconnected = new EventCounter();
            Exception? clientErr = null;
            string echoMessage = "";
            var client = new SimpleNamedPipeClient(pipeName, (pipeEvent) =>
            {
                switch(pipeEvent)
                {
                    case Disconnected:
                        clientDisconnected.Set();
                        break;
                    case Received received:
                        echoMessage = ToStr(received.Data.Span);
                        echoComplete.Set();
                        break;
                    case ExceptionTrapped ex:
                        clientErr = ex.Exception;
                        break;
                }
            });
            Assert.AreEqual(WC(), serverConnected.Wait(1000));

            var hello = "HELLO WORLD!";
            client.WriteAsync(ToBytes(hello)).Wait();
            Assert.AreEqual(WC(), echoComplete.Wait(1000),"wait echo message");
            client.Dispose();
            Assert.AreEqual(WC(), clientDisconnected.Wait(1000));
            Assert.AreEqual(WC(), serverDisconnected.Wait(1000));
            server.Dispose();
            Assert.AreEqual(WC(),serverClosed.Wait(1000));
            Assert.IsNull(serverErr);
            Assert.IsNull(clientErr);
            Assert.AreEqual("echo: HELLO WORLD!", echoMessage);
        }

        public void HelloNtimes(int repeat)
        {
            var serverConnected = new EventCounter();
            var serverDisconnected = new EventCounter();
            var serverClosed = new EventCounter();
            Exception? serverErr = null;

            var pipeName = Guid.NewGuid().ToString();
            var server = new SimpleNamedPipeServer(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Connected:
                        serverConnected.Set();
                        break;
                    case Disconnected:
                        serverDisconnected.Set();
                        break;
                    case Received received:
                        pipeEvent.Pipe.WriteAsync(ToBytes("echo: " + ToStr(received.Data.Span))).Wait();
                        break;
                    case Closed:
                        serverClosed.Set();
                        break;
                    case ExceptionTrapped ex:
                        serverErr = ex.Exception;
                        break;
                }
            });

            var echoComplete = new EventCounter();
            var clientDisconnected = new EventCounter();
            Exception? clientErr = null;
            var actualValues = new List<string>();
            var remain = repeat;
            var client = new SimpleNamedPipeClient(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Disconnected:
                        clientDisconnected.Set();
                        break;
                    case Received received:
                        actualValues.Add(ToStr(received.Data.Span));
                        if(0 == Interlocked.Decrement(ref remain))
                        {
                            echoComplete.Set();
                        }
                        break;
                    case ExceptionTrapped ex:
                        clientErr = ex.Exception;
                        break;
                }
            });

            Assert.AreEqual(WC(), serverConnected.Wait(1000));

            var expectedValues = new List<string>();
            for(var i = 0; i < repeat; ++i)
            {
                var str = $"HELLO WORLD![{i}]";
                expectedValues.Add("echo: " + str);
                client.WriteAsync(ToBytes(str)).Wait();
            }

            Assert.AreEqual(WC(), echoComplete.Wait(1000), "wait echo message");
            client.Dispose();
            Assert.AreEqual(WC(), clientDisconnected.Wait(1000));
            Assert.AreEqual(WC(), serverDisconnected.Wait(1000));
            server.Dispose();
            Assert.AreEqual(WC(), serverClosed.Wait(1000));
            Assert.IsNull(serverErr);
            Assert.IsNull(clientErr);
            Assert.IsTrue(Enumerable.SequenceEqual(expectedValues, actualValues));
        }

        [TestMethod]
        public void Hello3times()
        {
            HelloNtimes(3);
        }

        public void ConnectNtimes(int repeat)
        {
            var serverConnected = new EventCounter();
            var serverDisconnected = new EventCounter();
            var serverClosed = new EventCounter();
            Exception? serverErr = null;

            var pipeName = Guid.NewGuid().ToString();
            var server = new SimpleNamedPipeServer(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Connected:
                        serverConnected.Set();
                        break;
                    case Disconnected:
                        serverDisconnected.Set();
                        break;
                    case Received received:
                        pipeEvent.Pipe.WriteAsync(ToBytes("echo: " + ToStr(received.Data.Span))).Wait();
                        break;
                    case Closed:
                        serverClosed.Set();
                        break;
                    case ExceptionTrapped ex:
                        serverErr = ex.Exception;
                        break;
                }
            });

            var expectedValues = new List<string>();
            Exception? clientErr = null;
            var actualValues = new List<string>();
            var remain = repeat;
            for (var i = 0; i < repeat; ++i)
            {
                var echoComplete = new EventCounter();
                var clientDisconnected = new EventCounter();
                var client = new SimpleNamedPipeClient(pipeName, (pipeEvent) =>
                {
                    switch (pipeEvent)
                    {
                        case Disconnected:
                            clientDisconnected.Set();
                            break;
                        case Received received:
                            actualValues.Add(ToStr(received.Data.Span));
                            echoComplete.Set();
                            break;
                        case ExceptionTrapped ex:
                            clientErr = ex.Exception;
                            break;
                    }
                });

                Assert.AreEqual(WC(), serverConnected.Wait(1000));

                var str = $"HELLO WORLD![{i}]";
                expectedValues.Add("echo: " + str);
                client.WriteAsync(ToBytes(str)).Wait();
                Assert.AreEqual(WC(), echoComplete.Wait(1000), "wait echo message");
                client.Dispose();
                Assert.AreEqual(WC(), clientDisconnected.Wait(1000));
                Assert.AreEqual(WC(), serverDisconnected.Wait(1000));
                Assert.IsNull(clientErr);
                serverConnected.Reset();
                serverDisconnected.Reset();
            }

            server.Dispose();
            Assert.AreEqual(WC(), serverClosed.Wait(1000));
            Assert.IsNull(serverErr);
            Assert.IsTrue(Enumerable.SequenceEqual(expectedValues, actualValues));
        }

        [TestMethod]
        public void Connect3times()
        {
            ConnectNtimes(3);
        }

        [TestMethod]
        public void DisconnectByServer()
        {
            var serverConnected = new EventCounter();
            var serverDisconnected = new EventCounter();
            var serverClosed = new EventCounter();
            Exception? serverErr = null;

            var pipeName = Guid.NewGuid().ToString();
            var server = new SimpleNamedPipeServer(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Connected:
                        serverConnected.Set();
                        break;
                    case Disconnected:
                        serverDisconnected.Set();
                        break;
                    case Received received:
                        pipeEvent.Pipe.WriteAsync(ToBytes("echo: " + ToStr(received.Data.Span))).Wait();
                        (pipeEvent.Pipe as SimpleNamedPipeServer)!.Disconnect();
                        break;
                    case Closed:
                        serverClosed.Set();
                        break;
                    case ExceptionTrapped ex:
                        serverErr = ex.Exception;
                        break;
                }
            });

            //未接続のときにDisconnectを呼んでも影響はない
            server.Disconnect();

            {
                var echoComplete = new EventCounter();
                var clientDisconnected = new EventCounter();
                Exception? clientErr = null;
                string echoMessage = "";
                var client = new SimpleNamedPipeClient(pipeName, (pipeEvent) =>
                {
                    switch (pipeEvent)
                    {
                        case Disconnected:
                            clientDisconnected.Set();
                            break;
                        case Received received:
                            echoMessage = ToStr(received.Data.Span);
                            echoComplete.Set();
                            break;
                        case ExceptionTrapped ex:
                            clientErr = ex.Exception;
                            break;
                    }
                });
                Assert.AreEqual(WC(), serverConnected.Wait(1000));

                var hello = "HELLO WORLD!";
                client.WriteAsync(ToBytes(hello)).Wait();
                Assert.AreEqual(WC(), echoComplete.Wait(1000), "wait echo message");

                Assert.AreEqual(WC(), serverDisconnected.Wait(1000));
                Assert.AreEqual(WC(), clientDisconnected.Wait(1000));
                client.Dispose();
                Assert.IsNull(clientErr);
                Assert.AreEqual("echo: HELLO WORLD!", echoMessage);
            }

            serverConnected.Reset();
            serverDisconnected.Reset();

            //再接続可能か？
            {
                var echoComplete = new EventCounter();
                var clientDisconnected = new EventCounter();
                Exception? clientErr = null;
                string echoMessage = "";
                var client = new SimpleNamedPipeClient(pipeName, (pipeEvent) =>
                {
                    switch (pipeEvent)
                    {
                        case Disconnected:
                            clientDisconnected.Set();
                            break;
                        case Received received:
                            echoMessage = ToStr(received.Data.Span);
                            echoComplete.Set();
                            break;
                        case ExceptionTrapped ex:
                            clientErr = ex.Exception;
                            break;
                    }
                });
                Assert.AreEqual(WC(), serverConnected.Wait(1000));

                var hello = "HELLO WORLD![2]";
                client.WriteAsync(ToBytes(hello)).Wait();
                Assert.AreEqual(WC(), echoComplete.Wait(1000), "wait echo message");

                Assert.AreEqual(WC(), serverDisconnected.Wait(1000));
                Assert.AreEqual(WC(), clientDisconnected.Wait(1000));
                client.Dispose();
                Assert.IsNull(clientErr);
                Assert.AreEqual("echo: HELLO WORLD![2]", echoMessage);
            }

            server.Dispose();
            Assert.AreEqual(WC(), serverClosed.Wait(1000));
            Assert.IsNull(serverErr);
        }

        [TestMethod]
        public void WriteCancel()
        {
            const int BufSize = SimpleNamedPipeBase.MinBufferSize;

            var serverConnected = new EventCounter();
            var serverDisconnected = new EventCounter();
            var serverClosed = new EventCounter();
            var serverReceived = new EventCounter();
            Exception? serverErr = null;

            var pipeName = Guid.NewGuid().ToString();
            var server = new SimpleNamedPipeServer(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Connected:
                        serverConnected.Set();
                        break;
                    case Disconnected:
                        serverDisconnected.Set();
                        break;
                    case Received received:
                        pipeEvent.Pipe.WriteAsync(received.Data).Wait();
                        serverReceived.Set();
                        break;
                    case Closed:
                        serverClosed.Set();
                        break;
                    case ExceptionTrapped ex:
                        serverErr = ex.Exception;
                        break;
                }
            }, BufSize);

            var echoComplete = new EventCounter();
            var clientDisconnected = new EventCounter();
            Exception? clientErr = null;
            string? echoMessage = null;
            var client = new SimpleNamedPipeClient(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Disconnected:
                        clientDisconnected.Set();
                        break;
                    case Received received:
                        try
                        {
                            echoMessage = ToStr(received.Data.Span);
                        }
                        catch { }
                        echoComplete.Set();
                        break;
                    case ExceptionTrapped ex:
                        clientErr = ex.Exception;
                        break;
                }
            }, BufSize);

            var data = new byte[SimpleNamedPipeBase.TypicalbufferSize * 10000];

            Assert.AreEqual(WC(), serverConnected.Wait(1000));
            var cts = new CancellationTokenSource();
            _ = Task.Delay(1).ContinueWith((t) => cts.Cancel());
            Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
            {
                await client.WriteAsync(data, cts.Token);
            }).Wait();

            //早いマシンだとキャンセルが間に合わないかもしれないのでチェック
            Assert.AreEqual(WC(0, true), serverReceived.Wait(1), "Sending completed before cancellation processing.");

            //キャンセル後に送信
            client.WriteAsync(ToBytes("Hello World")).Wait();
            Assert.AreEqual(WC(), serverReceived.Wait(1000), "wait for serverReceived");
            Assert.AreEqual(WC(), echoComplete.Wait(1000), "wait for serverReceived");
            Assert.AreEqual("Hello World", echoMessage);

            client.Dispose();
            Assert.AreEqual(WC(), clientDisconnected.Wait(1000));
            Assert.AreEqual(WC(), serverDisconnected.Wait(1000));
            server.Dispose();
            Assert.AreEqual(WC(), serverClosed.Wait(1000));
            Assert.IsNull(serverErr);
            Assert.IsNull(clientErr);
        }

        [TestMethod]
        public void WriteCancelImmediate()
        {
            const int BufSize = SimpleNamedPipeBase.MinBufferSize;

            var serverConnected = new EventCounter();
            var serverDisconnected = new EventCounter();
            var serverClosed = new EventCounter();
            var serverReceived = new EventCounter();
            Exception? serverErr = null;

            var pipeName = Guid.NewGuid().ToString();
            var server = new SimpleNamedPipeServer(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Connected:
                        serverConnected.Set();
                        break;
                    case Disconnected:
                        serverDisconnected.Set();
                        break;
                    case Received received:
                        pipeEvent.Pipe.WriteAsync(received.Data).Wait();
                        serverReceived.Set();
                        break;
                    case Closed:
                        serverClosed.Set();
                        break;
                    case ExceptionTrapped ex:
                        serverErr = ex.Exception;
                        break;
                }
            }, BufSize);

            var echoComplete = new EventCounter();
            var clientDisconnected = new EventCounter();
            Exception? clientErr = null;
            string? echoMessage = null;
            var client = new SimpleNamedPipeClient(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Disconnected:
                        clientDisconnected.Set();
                        break;
                    case Received received:
                        try
                        {
                            echoMessage = ToStr(received.Data.Span);
                        }
                        catch { }
                        echoComplete.Set();
                        break;
                    case ExceptionTrapped ex:
                        clientErr = ex.Exception;
                        break;
                }
            }, BufSize);

            var data = new byte[SimpleNamedPipeBase.TypicalbufferSize * 10000];

            Assert.AreEqual(WC(), serverConnected.Wait(1000));
            var cts = new CancellationTokenSource();
            cts.Cancel();   //キャンセルシグナル状態で送信をする
            Assert.ThrowsExceptionAsync<OperationCanceledException>(async() =>
            {
                await client.WriteAsync(data, cts.Token);
            }).Wait();

            //キャンセル後に送信
            client.WriteAsync(ToBytes("Hello World")).Wait();
            Assert.AreEqual(WC(), serverReceived.Wait(1000), "wait for serverReceived");
            Assert.AreEqual(WC(), echoComplete.Wait(1000), "wait for serverReceived");
            Assert.AreEqual("Hello World", echoMessage);

            client.Dispose();
            Assert.AreEqual(WC(), clientDisconnected.Wait(1000));
            Assert.AreEqual(WC(), serverDisconnected.Wait(1000));
            server.Dispose();
            Assert.AreEqual(WC(), serverClosed.Wait(1000));
            Assert.IsNull(serverErr);
            Assert.IsNull(clientErr);
        }

        [TestMethod]
        public void TooLongWriteSize()
        {
            const int BufSize = SimpleNamedPipeBase.MinBufferSize;

            var serverConnected = new EventCounter();
            var serverDisconnected = new EventCounter();
            var serverClosed = new EventCounter();
            Exception? serverErr = null;

            var pipeName = Guid.NewGuid().ToString();
            var server = new SimpleNamedPipeServer(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Connected:
                        serverConnected.Set();
                        break;
                    case Disconnected:
                        serverDisconnected.Set();
                        break;
                    case Received received:
                        pipeEvent.Pipe.WriteAsync(ToBytes("echo: " + ToStr(received.Data.Span))).Wait();
                        break;
                    case Closed:
                        serverClosed.Set();
                        break;
                    case ExceptionTrapped ex:
                        serverErr = ex.Exception;
                        break;
                }
            }, BufSize, BufSize);

            var echoComplete = new EventCounter();
            var clientDisconnected = new EventCounter();
            Exception? clientErr = null;
            string echoMessage = "";
            var client = new SimpleNamedPipeClient(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Disconnected:
                        clientDisconnected.Set();
                        break;
                    case Received received:
                        echoMessage = ToStr(received.Data.Span);
                        echoComplete.Set();
                        break;
                    case ExceptionTrapped ex:
                        clientErr = ex.Exception;
                        break;
                }
            }, BufSize, BufSize);

            const int OverBufSize = BufSize + 1;

            Assert.AreEqual(WC(), serverConnected.Wait(1000));

            var data = new byte[OverBufSize];
            Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(async() =>
            {
                await client.WriteAsync(data);
            }).Wait();
            client.Dispose();
            server.Dispose();
        }

        [TestMethod]
        public void OverbufferTransfer()
        {
            var serverConnected = new EventCounter();
            var serverDisconnected = new EventCounter();
            var serverClosed = new EventCounter();
            Exception? serverErr = null;

            const int BuffserSize = 1024;
            const int SampleSize = BuffserSize * 4;

            byte[] expected = new byte[SampleSize];
            for (int i = 0; i<SampleSize; ++i)
            {
                expected[i] = (byte)i;
            }

            var pipeName = Guid.NewGuid().ToString();
            var server = new SimpleNamedPipeServer(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Connected:
                        serverConnected.Set();
                        break;
                    case Disconnected:
                        serverDisconnected.Set();
                        break;
                    case Received received:
                        pipeEvent.Pipe.WriteAsync(received.Data).Wait();
                        break;
                    case Closed:
                        serverClosed.Set();
                        break;
                    case ExceptionTrapped ex:
                        serverErr = ex.Exception;
                        break;
                }
            }, BuffserSize);

            var echoComplete = new EventCounter();
            var clientDisconnected = new EventCounter();
            Exception? clientErr = null;
            byte[]? actual = null;
            var client = new SimpleNamedPipeClient(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Disconnected:
                        clientDisconnected.Set();
                        break;
                    case Received received:
                        actual = received.Data.ToArray();
                        echoComplete.Set();
                        break;
                    case ExceptionTrapped ex:
                        clientErr = ex.Exception;
                        break;
                }
            }, BuffserSize);
            Assert.AreEqual(WC(), serverConnected.Wait(1000));

            client.WriteAsync(expected).Wait();
            Assert.AreEqual(WC(), echoComplete.Wait(1000), "wait echo message");
            Assert.IsTrue(Enumerable.SequenceEqual(expected, actual!));

            client.Dispose();
            Assert.AreEqual(WC(), clientDisconnected.Wait(1000));
            Assert.AreEqual(WC(), serverDisconnected.Wait(1000));
            server.Dispose();
            Assert.AreEqual(WC(), serverClosed.Wait(1000));
            Assert.IsNull(serverErr);
            Assert.IsNull(clientErr);
        }

        [TestMethod]
        public void MultiWrite()
        {
            var serverConnected = new EventCounter();
            var serverDisconnected = new EventCounter();
            var serverClosed = new EventCounter();
            Exception? serverErr = null;

            const int repeat = 20;
            int remain = repeat;
            var expected = Enumerable.Range(0, repeat).Select(e => $"HELLO WORLD! [{e:d2}]").ToArray();

            var pipeName = Guid.NewGuid().ToString();
            var server = new SimpleNamedPipeServer(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Connected:
                        serverConnected.Set();
                        break;
                    case Disconnected:
                        serverDisconnected.Set();
                        break;
                    case Received received:
                        pipeEvent.Pipe.WriteAsync(received.Data).Wait();
                        break;
                    case Closed:
                        serverClosed.Set();
                        break;
                    case ExceptionTrapped ex:
                        serverErr = ex.Exception;
                        break;
                }
            });

            var echoComplete = new EventCounter();
            var clientDisconnected = new EventCounter();
            Exception? clientErr = null;
            var actual = new List<string>();
            var client = new SimpleNamedPipeClient(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Disconnected:
                        clientDisconnected.Set();
                        break;
                    case Received received:
                        actual.Add(ToStr(received.Data.Span));
                        if(0 == Interlocked.Decrement(ref remain))
                        {
                            echoComplete.Set();
                        }
                        break;
                    case ExceptionTrapped ex:
                        clientErr = ex.Exception;
                        break;
                }
            });
            Assert.AreEqual(WC(), serverConnected.Wait(1000));

            Parallel.ForEach(expected, (e) =>
            {
                client.WriteAsync(ToBytes(e)).Wait();
            });

            Assert.AreEqual(WC(), echoComplete.Wait(1000), "wait echo message");
            client.Dispose();
            Assert.AreEqual(WC(), clientDisconnected.Wait(1000));
            Assert.AreEqual(WC(), serverDisconnected.Wait(1000));
            server.Dispose();
            Assert.AreEqual(WC(), serverClosed.Wait(1000));
            Assert.IsNull(serverErr);
            Assert.IsNull(clientErr);

            actual.Sort();
            Assert.IsTrue(Enumerable.SequenceEqual(expected, actual));

        }

        [TestMethod]
        public void ReceiverTaskException()
        {
            var serverConnected = new EventCounter();
            var serverDisconnected = new EventCounter();
            var serverClosed = new EventCounter();

            var pipeName = Guid.NewGuid().ToString();
            var server = new SimpleNamedPipeServer(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Connected:
                        serverConnected.Set();
                        break;
                    case Disconnected:
                        serverDisconnected.Set();
                        break;
                    case Received received:
                        pipeEvent.Pipe.WriteAsync(ToBytes("echo: " + ToStr(received.Data.Span))).Wait();
                        break;
                    case Closed:
                        serverClosed.Set();
                        break;
                    case ExceptionTrapped ex:
                        break;
                }
            });

            var echoComplete = new EventCounter();
            var clientDisconnected = new EventCounter();
            Exception? clientErr = null;
            var clientExceptionEvent = new EventCounter();
            var client = new SimpleNamedPipeClient(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Disconnected:
                        clientDisconnected.Set();
                        break;
                    case Received received:
                        throw new Exception("client exception");
                    case ExceptionTrapped ex:
                        clientErr = ex.Exception;
                        clientExceptionEvent.Set();
                        break;
                }
            });
            Assert.AreEqual(WC(), serverConnected.Wait(1000));

            var hello = "HELLO WORLD!";
            client.WriteAsync(ToBytes(hello)).Wait();
            Assert.AreEqual(WC(), clientExceptionEvent.Wait(1000));
            Assert.AreEqual("client exception", clientErr?.Message);
            client.Dispose();
            Assert.AreEqual(WC(), clientDisconnected.Wait(1000));
            Assert.AreEqual(WC(), serverDisconnected.Wait(1000));
            server.Dispose();
            Assert.AreEqual(WC(), serverClosed.Wait(1000));
        }

        [TestMethod]
        public void LimitSizeException()
        {
            var serverConnected = new EventCounter();
            var serverDisconnected = new EventCounter();
            var serverClosed = new EventCounter();
            Exception? serverErr = null;
            var serverErrEvent = new EventCounter();

            var pipeName = Guid.NewGuid().ToString();
            var server = new SimpleNamedPipeServer(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Connected:
                        serverConnected.Set();
                        break;
                    case Disconnected:
                        serverDisconnected.Set();
                        break;
                    case Received received:
                        pipeEvent.Pipe.WriteAsync(ToBytes("echo: " + ToStr(received.Data.Span))).Wait();
                        break;
                    case Closed:
                        serverClosed.Set();
                        break;
                    case ExceptionTrapped ex:
                        serverErr = ex.Exception;
                        serverErrEvent.Set();
                        break;
                }
            },1024, 8);

            var echoComplete = new EventCounter();
            var clientDisconnected = new EventCounter();
            Exception? clientErr = null;
            string echoMessage = "";
            var client = new SimpleNamedPipeClient(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Disconnected:
                        clientDisconnected.Set();
                        break;
                    case Received received:
                        echoMessage = ToStr(received.Data.Span);
                        echoComplete.Set();
                        break;
                    case ExceptionTrapped ex:
                        clientErr = ex.Exception;
                        break;
                }
            }, 1024, 18);
            Assert.AreEqual(WC(), serverConnected.Wait(1000));

            //18byte制限に対して20byte
            var data = ToBytes("0123456789");
            Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(async () =>
            {
                await client.WriteAsync(data);
            }).Wait();

            //サーバー8byte制限に対して16byte
            data = ToBytes("01234567");
            try
            {
                client.WriteAsync(data).Wait();
            }
            catch { }//接続断による例外が発生する可能性あるが正常動作。

            Assert.AreEqual(WC(), serverErrEvent.Wait(1000));
            Assert.IsNotNull(serverErr);
            Assert.AreEqual(serverErr.GetType(), typeof(InvalidDataException));
            client.Dispose();
            Assert.AreEqual(WC(), clientDisconnected.Wait(1000));
            Assert.AreEqual(WC(), serverDisconnected.Wait(1000));
            server.Dispose();
            Assert.AreEqual(WC(), serverClosed.Wait(1000));
        }

        [TestMethod]
        public void CreateException()
        {
            var pipeName1 = Guid.NewGuid().ToString();
            var serverDisconnected = new EventCounter();
            var clientDisconnected = new EventCounter();

            var server1 = new SimpleNamedPipeServer(pipeName1, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Disconnected: 
                        serverDisconnected.Set(); 
                        break;
                }
            });

            Assert.ThrowsException<IOException>(() =>
            {
                new SimpleNamedPipeServer(pipeName1, (pipeEvent) => { });
            });

            var client1 = new SimpleNamedPipeClient(pipeName1, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Disconnected:
                        clientDisconnected.Set();
                        break;
                }
            });

            //クライアントの同時接続数は1つのみ
            Assert.ThrowsException<TimeoutException>(() =>
            {
                new SimpleNamedPipeClient(pipeName1, (pipeEvent) => { });
            });

            var pipeName2 = Guid.NewGuid().ToString();
            //存在しないパイプに接続
            Assert.ThrowsException<TimeoutException>(() =>
            {
                new SimpleNamedPipeClient(pipeName2, (pipeEvent) => { });
            });

            client1.Dispose();
            Assert.AreEqual(WC(), clientDisconnected.Wait(1000));
            Assert.AreEqual(WC(), serverDisconnected.Wait(1000));
            server1.Dispose();
        }

        [TestMethod]
        public void UnreachedException()
        {
            var serverConnected = new EventCounter();
            var serverDisconnected = new EventCounter();
            var clientDisconnected = new EventCounter();
            var serverClosed = new EventCounter();

            var pipeName = Guid.NewGuid().ToString();
            var server = new SimpleNamedPipeServer(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Connected:
                        serverConnected.Set();
                        break;
                    case Disconnected:
                        serverDisconnected.Set();
                        break;
                }
            });

            //未接続で送信
            Assert.ThrowsExceptionAsync<InvalidOperationException>(async() =>
            {
                await server.WriteAsync(ToBytes("ABCDEF"));
            }).Wait();

            var client = new SimpleNamedPipeClient(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Disconnected:
                        clientDisconnected.Set();
                        break;
                }
            });
            Assert.AreEqual(WC(), serverConnected.Wait(1000));
            client.WriteAsync(ToBytes("HELLO WORLD!")).Wait();
            client.Dispose();
            Assert.AreEqual(WC(), clientDisconnected.Wait(1000));
            Assert.AreEqual(WC(), serverDisconnected.Wait(1000));
            //切断後に送信 クライアント
            Assert.ThrowsExceptionAsync<ObjectDisposedException>(async () =>
            {
                await client.WriteAsync(ToBytes("Hello"));
            }).Wait();
            //切断後に送信 サーバー
            Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await server.WriteAsync(ToBytes("Hello"));
            }).Wait();

            server.Dispose();
        }

        [TestMethod]
        public void ServerShutdown()
        {
            var serverConnected = new EventCounter();
            var serverDisconnected = new EventCounter();
            var serverClosed = new EventCounter();
            var serverNeedCloseEvent = new EventCounter(); 
            Exception? serverErr = null;

            var pipeName = Guid.NewGuid().ToString();

            var server = new SimpleNamedPipeServer(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Connected:
                        serverConnected.Set();
                        break;
                    case Disconnected:
                        serverDisconnected.Set();
                        break;
                    case Received received:
                        pipeEvent.Pipe.WriteAsync(ToBytes("echo: " + ToStr(received.Data.Span))).Wait();
                        serverNeedCloseEvent.Set();
                        break;
                    case Closed:
                        serverClosed.Set();
                        break;
                    case ExceptionTrapped ex:
                        serverErr = ex.Exception;
                        break;
                }
            });

            var echoComplete = new EventCounter();
            var clientDisconnected = new EventCounter();
            Exception? clientErr = null;
            string echoMessage = "";
            var client = new SimpleNamedPipeClient(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Disconnected:
                        clientDisconnected.Set();
                        break;
                    case Received received:
                        echoMessage = ToStr(received.Data.Span);
                        echoComplete.Set();
                        break;
                    case ExceptionTrapped ex:
                        clientErr = ex.Exception;
                        break;
                }
            });

            Assert.AreEqual(WC(), serverConnected.Wait(1000));
            client.WriteAsync(ToBytes("HELLO WORLD!")).Wait();
            Assert.AreEqual(WC(), serverNeedCloseEvent.Wait(1000));
            server.Dispose();
            Assert.AreEqual(WC(), echoComplete.Wait(1000));
            Assert.AreEqual(WC(), serverDisconnected.Wait(1000));
            Assert.AreEqual(WC(), serverClosed.Wait(1000));
            Assert.AreEqual(WC(), clientDisconnected.Wait(1000));
            Assert.IsNull(clientErr);
            Assert.IsNull(serverErr);
            Assert.AreEqual("echo: HELLO WORLD!", echoMessage);
        }

        [TestMethod]
        [TestCategory("Hevy")]
        public void Hello1000times()
        {
            HelloNtimes(1000);
        }

        [TestMethod]
        [TestCategory("Hevy")]
        public void Connect1000times()
        {
            ConnectNtimes(1000);
        }

        [TestMethod]
        [TestCategory("Hevy")]
        public void TransferMaxDataSize()
        {
            var serverConnected = new EventCounter();
            var serverDisconnected = new EventCounter();
            var serverClosed = new EventCounter();
            Exception? serverErr = null;

            var expected = new byte[SimpleNamedPipeBase.MaxDataSize];
            for(int i = 0; i<expected.Length; ++i)
            {
                expected[i] = (byte)i;
            }
            var actual = new byte[SimpleNamedPipeBase.MaxDataSize];

            var pipeName = Guid.NewGuid().ToString();
            var server = new SimpleNamedPipeServer(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Connected:
                        serverConnected.Set();
                        break;
                    case Disconnected:
                        serverDisconnected.Set();
                        break;
                    case Received received:
                        pipeEvent.Pipe.WriteAsync(received.Data).Wait();
                        break;
                    case Closed:
                        serverClosed.Set();
                        break;
                    case ExceptionTrapped ex:
                        serverErr = ex.Exception;
                        break;
                }
            }, SimpleNamedPipeBase.TypicalbufferSize, SimpleNamedPipeBase.MaxDataSize);

            var echoComplete = new EventCounter();
            var clientDisconnected = new EventCounter();
            Exception? clientErr = null;
            var client = new SimpleNamedPipeClient(pipeName, (pipeEvent) =>
            {
                switch (pipeEvent)
                {
                    case Disconnected:
                        clientDisconnected.Set();
                        break;
                    case Received received:
                        received.Data.CopyTo(actual);
                        echoComplete.Set();
                        break;
                    case ExceptionTrapped ex:
                        clientErr = ex.Exception;
                        break;
                }
            }, SimpleNamedPipeBase.TypicalbufferSize, SimpleNamedPipeBase.MaxDataSize);
            Assert.AreEqual(WC(), serverConnected.Wait(1000));

            client.WriteAsync(expected).Wait();
            Assert.AreEqual(WC(), echoComplete.Wait(TimeSpan.FromSeconds(60)), "wait echo message");
            client.Dispose();
            Assert.AreEqual(WC(), clientDisconnected.Wait(1000));
            Assert.AreEqual(WC(), serverDisconnected.Wait(1000));
            server.Dispose();
            Assert.AreEqual(WC(), serverClosed.Wait(1000));
            Assert.IsNull(serverErr);
            Assert.IsNull(clientErr);
            Assert.IsTrue(Enumerable.SequenceEqual(expected, actual));
        }
    }
}
