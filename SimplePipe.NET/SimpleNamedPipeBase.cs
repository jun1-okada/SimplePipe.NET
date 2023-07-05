using System;
using System.Runtime.InteropServices;
using System.IO.Pipes;

namespace Abt.Comm.SimplePipe
{
    /// <summary>
    /// 通信ヘッダー構造体
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
    internal struct Header
    {
        private UInt32 size;
        private UInt16 dataOffset;
        /// <summary>
        /// フラグ領域(16bit)
        /// </summary>
        /// <remarks>利用しているのは下位3bit分のみ。残りは予約用</remarks>
        private UInt16 bitset;

        /// <summary>
        /// ヘッダー、データ本体を含めた全体のサイズ
        /// </summary>
        public UInt32 Size => size;
        /// <summary>
        /// データ本体の先頭からのオフセット
        /// </summary>
        public UInt16 DataOffset => dataOffset;

        /// <summary>
        /// ヘッダーとリザーブ領域を除いた純粋なデータサイズ
        /// </summary>
        public UInt32 DataSize
        {
            get
            {
                if (size < dataOffset) throw new InvalidOperationException();
                return size - dataOffset;
            }
        }

        /// <summary>
        /// データの始まりを示すスタートビット
        /// </summary>
        public bool IsStart => (bitset & 0x01) != 0;
        /// <summary>
        /// データの終わりを示すエンドビット
        /// </summary>
        public bool IsEnd => (bitset & 0x02) != 0;
        /// <summary>
        /// 送信キャンセルを示すキャンセルビット
        /// </summary>
        public bool IsCancnel => (bitset & 0x04) != 0;
        /// <summary>
        /// 現在のインスタンスの内容でヘッダー部とヘッダーリザーブ部のバイトイメージを生成する
        /// </summary>
        /// <returns>現在の内容のバイトイメージ</returns>
        public readonly byte[] ToBytes()
        {
            var bytes = new byte[dataOffset];
            var ptr = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.StructureToPtr(this, ptr, false);
                Marshal.Copy(ptr, bytes, 0, HeaderLength);
            }
            finally { Marshal.FreeHGlobal(ptr); }
            return bytes;
        }

        /// <summary>
        /// リザーブ部を含めないヘッダーサイズ
        /// </summary>
        public static readonly int HeaderLength = Marshal.SizeOf(typeof(Header));

        /// <summary>
        /// キャンセルパケット用のヘッダー
        /// </summary>
        /// <remarks>
        /// キャンセル用のパケットはデータ部を持たない
        /// </remarks>
        public static readonly Header CancelHeader = Create(0, false, false, true);

        /// <summary>
        /// ヘッダーインスタンスを引数から作成
        /// </summary>
        /// <param name="dataSize">ヘッダーを含まないデータのサイズ</param>
        /// <param name="startBit">スタートビット</param>
        /// <param name="endBit">エンドビット</param>
        /// <param name="cancelBit">キャンセルビット</param>
        /// <returns></returns>
        public static Header Create(int dataSize, bool startBit, bool endBit, bool cancelBit)
        {
            return new Header
            {
                size = (UInt32)(HeaderLength + dataSize),
                dataOffset = (UInt16)HeaderLength,
                bitset = (UInt16)((startBit ? 0x01 : 0) | (endBit ? 0x02 : 0) | (cancelBit ? 0x04 : 0)),
            };
        }

        /// <summary>
        /// バイト列からヘッダー構造体インスタンスを生成
        /// </summary>
        /// <param name="bytes">生成元のバイト列</param>
        /// <returns>ヘッダー構造体インスタンス</returns>
        /// <exception cref="ArgumentException"><paramref name="bytes"/>が不正</exception>
        public static Header FromBytes(ReadOnlySpan<byte> bytes)
        {
            if(bytes.Length < HeaderLength)
            {
                throw new ArgumentException("bytes is too shrot", nameof(bytes));
            }
            var handle = GCHandle.Alloc(bytes.Slice(0,HeaderLength).ToArray(), GCHandleType.Pinned);
            try {
                return (Header?)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(Header)) 
                    ?? throw new ArgumentException("bytes is not Header bytes image", nameof(bytes));
            }finally { handle.Free(); }
        }
    }

    /// <summary>
    /// パケットデータ
    /// </summary>
    /// <param name="Header">ヘッダー</param>
    /// <param name="Data">データ</param>
    internal record Packet(Header Header, ReadOnlyMemory<byte> Data)
    {
        public static readonly Packet CancelPacket = new Packet(Header.CancelHeader, ReadOnlyMemory<byte>.Empty);
    };

    /// <summary>
    /// 前方から自身を「消費」していくバッファークラス
    /// </summary>
    internal sealed class ConsumeBuffer
    {
        /// <summary>
        /// 現在のバッファー
        /// </summary>
        private ReadOnlyMemory<byte> data;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="bytes">対象となるバッファー</param>
        public ConsumeBuffer(ReadOnlyMemory<byte> bytes)
        {
            data = bytes;
        }

        /// <summary>
        /// true:バッファーが存在しない
        /// </summary>
        public bool IsEmpty => data.IsEmpty;

        /// <summary>
        /// 現在のバッファー
        /// </summary>
        public ReadOnlyMemory<byte> Data => data;

        /// <summary>
        /// 現在のバッファーサイズ
        /// </summary>
        public int Size => data.Length;

        /// <summary>
        /// 前方からバッファーを<paramref name="size"/>バイト消費し、消費したものを戻り値とする。
        /// </summary>
        /// <param name="size">消費するバイト数</param>
        /// <returns>バッファー先頭から<paramref name="size"/>分の消費したバッファー</returns>
        /// <exception cref="ArgumentException"><paramref name="size"/>が不正</exception>
        public ReadOnlyMemory<byte> Consume(int size)
        {
            if(size > data.Length)
            {
                throw new ArgumentException($"{nameof(size)} is too large", nameof(size));
            }
            var res = data.Slice(0, size);
            data = data.Slice(size);
            return res;
        }

        /// <summary>
        /// バッファー全体を消費する。自身のバッファーは空となる
        /// </summary>
        /// <returns>全てのバッファー</returns>
        public ReadOnlyMemory<byte> Consume()
        {
            var res = data;
            data = ReadOnlyMemory<byte>.Empty;
            return res;
        }
    }

    /// <summary>
    /// 受信データをパケット単位に復元するクラス
    /// </summary>
    internal sealed class Receiver
    {
        /// <summary>
        /// ステートマシン用ステート基底クラス
        /// </summary>
        private abstract class StateBase
        {
            protected readonly Receiver owner;
            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="owner">親クラスのインスタンス</param>
            protected StateBase(Receiver owner)
            {
                this.owner = owner;
            }

            /// <summary>
            /// データ制限サイズ
            /// </summary>
            protected int Limit => owner.limitSize;

            /// <summary>
            /// 一時保存プールへの追加
            /// </summary>
            /// <param name="buffer">追加するバッファー</param>
            protected void AddToPool(ReadOnlySpan<byte> buffer)
            {
                owner.pool.Write(buffer);
            }

            /// <summary>
            /// 一時保存プール領域のクリア
            /// </summary>
            protected void ClearPool()
            {
                owner.pool.Seek(0, SeekOrigin.Begin);
            }

            /// <summary>
            /// 現在のプール領域をバッファーとして取得。コピーではなくプールデータへの参照になる。
            /// </summary>
            /// <returns>現在のプール領域を示すバッファー</returns>
            protected ReadOnlyMemory<byte> PoolMemory() => new ReadOnlyMemory<byte>(owner.pool.GetBuffer(), 0, (int)owner.pool.Position);

            /// <summary>
            /// バッファーからヘッダー領域を復元
            /// </summary>
            /// <param name="buffer">バッファー</param>
            /// <returns>復元したヘッダーインスタンス</returns>
            /// <exception cref="InvalidDataException"><paramref name="buffer"/>が不正</exception>
            protected Header GetHeaderAndThrowIfBadHeader(ReadOnlySpan<byte> buffer)
            {
                var head = Header.FromBytes(buffer);
                if (head.Size < Header.HeaderLength || head.DataOffset < Header.HeaderLength)
                {
                    throw new InvalidDataException("invalid packet header");
                }
                if ((head.Size - Header.HeaderLength) > Limit)
                {
                    //データ制限サイズを超えている
                    throw new InvalidDataException("receive limit size is overflow");
                }
                return head;
            }

            /// <summary>
            /// アイドルステートインスタンス
            /// </summary>
            protected Idle Idle => owner.idle;
            /// <summary>
            /// 継続ステートインスタンス
            /// </summary>
            protected Continuation Continuation => owner.continuation;
            /// <summary>
            /// ヘッダー読み込み途中ステートインスタンス
            /// </summary>
            protected Insufficient Insufficient => owner.insufficient;

            /// <summary>
            /// 受信データ処理
            /// </summary>
            /// <param name="buffer">受信データ</param>
            /// <returns>(遷移後のステート,復元したパケット（存在しない場合はIsEmpty==true）</returns>
            public abstract ValueTuple<StateBase, Packet?> Feed(ConsumeBuffer buffer);
        }

        private readonly Idle idle;
        private readonly Continuation continuation;
        private readonly Insufficient insufficient;

        /// <summary>
        /// 現在のステート
        /// </summary>
        private StateBase state;

        /// <summary>
        /// アイドル状態ステート
        /// </summary>
        private sealed class Idle : StateBase
        {
            public Idle(Receiver owner) : base(owner) { }

            /// <summary>
            /// 受信データ処理
            /// </summary>
            /// <param name="buffer">受信データ</param>
            /// <returns>(遷移後のステート,復元したパケット（存在しない場合はIsEmpty==true）</returns>
            public override ValueTuple<StateBase, Packet?> Feed(ConsumeBuffer buffer)
            {
                if(buffer.Size < Header.HeaderLength)
                {
                    //ヘッダー復元に不十分な状態ではInsufficientへ遷移する
                    // 現在のバッファーをInsufficientインスタンスへ通知する
                    this.Insufficient.Continue(buffer);
                    return (this.Insufficient, null);
                }
                //バッファーからヘッダーを復元
                var header = GetHeaderAndThrowIfBadHeader(buffer.Data.Span);
                if(header.Size > buffer.Size)
                {
                    //データ部の受信には足りない状態ではContinuationへ遷移する
                    // すでに受信したデータを継続ステートインスタンスへ通知する
                    buffer.Consume(Header.HeaderLength);
                    this.Continuation.Continue(header, buffer.Consume().Span);
                    return (this.Continuation, null);
                }
                //データ部まで完全に受信できた
                // データ部を取得して戻り値とする。ステートはIdleを維持する。
                buffer.Consume(header.DataOffset);
                return (this, new Packet(header, buffer.Consume((int)header.DataSize)));
            }
        }

        /// <summary>
        /// 継続ステートクラス
        /// </summary>
        private sealed class Continuation : StateBase
        {
            /// <summary>
            /// 継続受信中のヘッダー情報
            /// </summary>
            private Header pendingHeader;

            /// <summary>
            /// 受信データ残サイズ
            /// </summary>
            private int remain = 0;

            public Continuation(Receiver owner) : base(owner) { }

            /// <summary>
            /// 継続処理準備
            /// </summary>
            /// <param name="head">継続受信のパケットヘッダー</param>
            /// <param name="buffer">受信済みのデータ</param>
            public void Continue(Header head, ReadOnlySpan<byte> buffer)
            {
                //ヘッダーを保存
                pendingHeader = head;
                //一時プール領域をクリアし、受信済みデータを格納
                ClearPool();
                AddToPool(buffer);
                //受信データ残サイズを設定
                remain = (int)head.Size - Header.HeaderLength - buffer.Length;
            }

            /// <summary>
            /// 受信データ処理
            /// </summary>
            /// <param name="buffer">受信データ</param>
            /// <returns>(遷移後のステート,復元したパケット（存在しない場合はIsEmpty==true）</returns>
　            public override (StateBase, Packet?) Feed(ConsumeBuffer buffer)
            {
                var appendSize = Math.Min(remain, buffer.Size);
                AddToPool(buffer.Consume(appendSize).Span);
                remain -= appendSize;
                if(remain > 0)
                {
                    return (this, null);
                }
                return (this.Idle, new Packet(pendingHeader, PoolMemory().Slice(pendingHeader.DataOffset - Header.HeaderLength)));
            }
        }

        /// <summary>
        /// ヘッダー読み込み途中ステートインスタンス
        /// </summary>
        private sealed class Insufficient : StateBase
        {
            /// <summary>
            /// 必要な残サイズ
            /// </summary>
            private int remain = 0;

            public Insufficient(Receiver owner) : base(owner) { }

            /// <summary>
            /// 準備処理
            /// </summary>
            /// <param name="buffer">ヘッダー途中までのバッファーデータ</param>
            /// <exception cref="ArgumentException"><paramref name="buffer"/>パラメータ不正</exception>
            public void Continue(ConsumeBuffer buffer)
            {
                //必要な残サイズを設定
                remain = Header.HeaderLength - buffer.Size;
                if(remain <= 0)
                {
                    throw new ArgumentException($"{nameof(buffer)} size is too big");
                }
                //初期データ登録
                ClearPool();
                AddToPool(buffer.Consume().Span);
            }

            /// <summary>
            /// 受信データ処理
            /// </summary>
            /// <param name="buffer">受信データ</param>
            /// <returns>(遷移後のステート,復元したパケット（存在しない場合はIsEmpty==true）</returns>
            public override (StateBase, Packet?) Feed(ConsumeBuffer buffer)
            {
                if(remain > buffer.Size)
                {
                    //後続のデータが必要
                    AddToPool(buffer.Consume().Span);
                    remain -= buffer.Size;
                    return (this, null);
                }
                //取得したバッファーから必要なデータをコピー
                AddToPool(buffer.Consume(remain).Span);

                //バッファーからヘッダーを復元
                var header = GetHeaderAndThrowIfBadHeader(PoolMemory().Span);
                if(header.Size - Header.HeaderLength > buffer.Size)
                {
                    //bufferの残データではパケット全体には足りない場合はContinuationへ遷移する
                    this.Continuation.Continue(header, buffer.Consume().Span);
                    return (this.Continuation, null);
                }
                //後続データも含めてパケット全体を復元
                buffer.Consume(header.DataOffset - Header.HeaderLength);
                //Idleステートへ遷移する
                return (this.Idle, new Packet(header, buffer.Consume((int)header.DataSize)));
            }
        }

        //受信データ制限サイズ
        private readonly int limitSize;
        //受信バッファーをまたいだ場合の一時保存領域
        private readonly MemoryStream pool;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="initReserveSize">初期一時保存領域サイズ</param>
        /// <param name="limitSize">データサイズ上限</param>
        public Receiver(int initReserveSize, int limitSize)
        {
            this.pool = new MemoryStream(initReserveSize);
            this.limitSize = limitSize;
            //各ステートのインスタンスを生成
            this.idle = new Idle(this);
            this.continuation = new Continuation(this);
            this.insufficient = new Insufficient(this);
            //初期ステートは idle
            this.state = this.idle;
        }

        /// <summary>
        /// 受信データ投入
        /// </summary>
        /// <param name="received">受信データ</param>
        /// <returns><paramref name="received"/>から復元した受信パケットリスト（0個の場合もありうる）</returns>
        public IEnumerable<Packet> Feed(ReadOnlyMemory<byte> received)
        {
            var buffer = new ConsumeBuffer(received);
            //recievedのデータを消費しつくすまで処理を実行
            while (!buffer.IsEmpty)
            {
                (state, var packet) = state.Feed(buffer);
                if(packet is not null)
                {
                    //復元したパケットを返す
                    yield return packet;
                }
            }
        }

        /// <summary>
        /// ステートを初期化
        /// </summary>
        public void Reset()
        {
            pool.Seek(0, SeekOrigin.Begin);
            state = idle;
        }
    }

    /// <summary>
    /// データをパケット列にシリアライズ
    /// </summary>
    internal static class Serializer
    {
        /// <summary>
        /// <paramref name="buffer"/>のデータを<paramref name="splitSize"/>単位のサイズのパケットに分割する
        /// </summary>
        /// <param name="buffer">シリアライズ対象のデータ</param>
        /// <param name="splitSize">1パケット分のサイズ</param>
        /// <returns>パケット列</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="splitSize"/>が不正</exception>
        public static IEnumerable<Packet> Serialize(ConsumeBuffer buffer, int splitSize)
        {
            if(splitSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(splitSize));
            }
            int remain = buffer.Size;
            bool beginning = true;
            while(remain > 0)
            {
                var size = Math.Min(remain, splitSize);
                var fragment = buffer.Consume(size);
                var header = Header.Create(size, beginning, buffer.IsEmpty, false);
                beginning = false;
                yield return new Packet(header, fragment);
                remain -= size;
            }
        }
    }

    /// <summary>
    /// パケット列からデータを復元する
    /// </summary>
    internal sealed class Deserializer
    {
        private bool beginning = true;
        private readonly MemoryStream pool;
        private readonly int limitSize;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="reserveSize">一時保存領域初期サイズ</param>
        /// <param name="limitSize">データサイズ上限</param>
        /// <exception cref="ArgumentOutOfRangeException">引数の指定が不正</exception>
        public Deserializer(int reserveSize, int limitSize)
        {
            if(reserveSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reserveSize));
            }
            if(limitSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limitSize));
            }
            pool = new MemoryStream(reserveSize);
            this.limitSize = limitSize;
        }

        /// <summary>
        /// 状態の初期化
        /// </summary>
        public void Reset()
        {
            beginning = true;
            pool.Seek(0, SeekOrigin.Begin);
        }

        /// <summary>
        /// パケット追加
        /// </summary>
        /// <param name="packet">追加パケット</param>
        /// <returns>復元したデータ。存在しない場合はIsEmpty==true</returns>
        /// <exception cref="ArgumentException"><paramref name="packet"/>の内容が矛盾している</exception>
        /// <exception cref="InvalidOperationException">現在のステートが矛盾しており継続不能</exception>
        public ReadOnlyMemory<byte> Feed(Packet packet)
        {
            if (packet.Header.IsCancnel)
            {
                //キャンセルパケット時の処理
                // 現在受信途中のデータは破棄してステートを初期化
                beginning = true;
                pool.Seek(0, SeekOrigin.Begin);
                return ReadOnlyMemory<byte>.Empty;
            }
            if (beginning)
            {
                //初回パケット処理
                pool.Seek(0, SeekOrigin.Begin);
                if (!packet.Header.IsStart)
                {
                    //パケットに矛盾,スタートビットがtureでなければならない
                    throw new ArgumentException("packet hader is invalid.",nameof(packet));
                }
                beginning = false;
            }
            if(limitSize < (int)pool.Position + packet.Data.Length)
            {
                //現在のデータとパケットが矛盾
                throw new InvalidOperationException("size is too long");
            }
            pool.Write(packet.Data.Span);
            if(packet.Header.IsEnd)
            {
                //入力パケットが最後のパケット(エンドフラグ==true)
                beginning = true;
                //結合したプール領域の参照を返す
                return new ReadOnlyMemory<byte>(pool.GetBuffer(), 0, (int)pool.Position);
            }
            //復元できる状態ではない
            return ReadOnlyMemory<byte>.Empty;
        }
    }

    /// <summary>
    /// イベント通知クラスインタフェース
    /// </summary>
    public interface IPipeEvent 
    {
        /// <summary>
        /// イベント送信元インスタンス
        /// </summary>
        SimpleNamedPipeBase Pipe { get; }
    }

    /// <summary>
    /// 接続イベント（サーバーのみ）
    /// </summary>
    /// <param name="Pipe">イベント送信元</param>
    public record class Connected(SimpleNamedPipeBase Pipe) : IPipeEvent;

    /// <summary>
    /// 切断イベント
    /// </summary>
    /// <param name="Pipe">イベント送信元</param>
    /// <remarks>
    /// クライアントはこのイベントの時点でインスタンスは利用不能となる。
    /// サーバーは引き続き次のクライアントの接続を受け付け可能。
    /// </remarks>
    public record class Disconnected(SimpleNamedPipeBase Pipe) : IPipeEvent;

    /// <summary>
    /// データ受信イベント
    /// </summary>
    /// <param name="Pipe">イベント送信元</param>
    /// <param name="Data">復元した受信データ</param>
    public record class Received(SimpleNamedPipeBase Pipe, ReadOnlyMemory<byte> Data) : IPipeEvent;

    /// <summary>
    /// クローズ時イベント（サーバーのみ）
    /// </summary>
    /// <param name="Pipe">イベント送信元</param>
    /// <remarks>このイベントの時点でインスタンスは利用出来なくなる</remarks>
    public record class Closed(SimpleNamedPipeBase Pipe) : IPipeEvent;

    /// <summary>
    /// 非同期処理中に回復不能な例外を捕捉した場合に通知する。この時点でインスタンスは利用できなくなる。
    /// </summary>
    /// <param name="Pipe">イベント送信元</param>
    /// <param name="Exception">発生した例外</param>
    public record class ExceptionTrapped(SimpleNamedPipeBase Pipe, Exception Exception) : IPipeEvent;

    /// <summary>
    /// 名前付きパイプ基底クラス（サーバー、クライアント共通処理）
    /// </summary>
    public abstract class SimpleNamedPipeBase : IDisposable
    {
        /// <summary>
        /// 推奨バッファーサイズ
        /// </summary>
        public const int TypicalbufferSize = 64 * 1024;
        /// <summary>
        /// 最低バッファーサイズ
        /// </summary>
        public const int MinBufferSize = 40;
        /// <summary>
        /// 利用可能最大データサイズ
        /// </summary>
        /// <remarks>
        /// バイト配列での最大利用可能サイズに制限 0X7FFFFFC7 = 2,147,483,591
        /// https://learn.microsoft.com/ja-jp/dotnet/api/system.array?redirectedfrom=MSDN&view=net-6.0
        /// </remarks>
        public const int MaxDataSize = 2_147_483_591;
        /// <summary>
        /// 受信/送信バッファーサイズ
        /// </summary>
        private readonly int bufferSize;
        /// <summary>
        /// データサイズ上限
        /// </summary>
        private readonly int limitDataSize;
        /// <summary>
        /// .NETパイプストリームインスタンス
        /// </summary>
        private readonly PipeStream stream;
        /// <summary>
        /// 受信タスク非同期オブジェクト
        /// </summary>
        private Task receiverTask = Task.CompletedTask;
        /// <summary>
        /// 受信バッファー サイズは<see cref="bufferSize"/>となる
        /// </summary>
        private readonly byte[] receiveBuffer;
        /// <summary>
        /// 受信データ復元クラスインスタンス
        /// </summary>
        private readonly Receiver receiver;
        /// <summary>
        /// 受信データデシリアライズクラス
        /// </summary>
        private readonly Deserializer deserializer;

        /// <summary>
        /// .NETパイプストリームインスタンス
        /// </summary>
        protected PipeStream Stream => stream;

        /// <summary>
        /// 終了時の非同期操作中断用キャンセルオブジェクト
        /// </summary>
        protected CancellationTokenSource ctsClose = new CancellationTokenSource();

        /// <summary>
        /// イベント通知コールバック関数
        /// </summary>
        protected readonly Action<IPipeEvent> callback;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="callback">イベント通知コールバック関数</param>
        /// <param name="bufferSize">受信/送信バッファーサイズ</param>
        /// <param name="limitDataSize">データサイズ上限</param>
        /// <param name="stream">サーバーまたはクライアントのPipeストリームインスタンス</param>
        /// <exception cref="ArgumentOutOfRangeException">引数が不正</exception>
        public SimpleNamedPipeBase(Action<IPipeEvent> callback, int bufferSize, int limitDataSize, PipeStream stream)
        {
            if (bufferSize < MinBufferSize)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize), $"Must be greater than or equal to {nameof(MinBufferSize)}");
            }
            if (0 >= limitDataSize || limitDataSize > MaxDataSize )
            {
                throw new ArgumentOutOfRangeException(nameof(limitDataSize), $"Must be 0 <= {nameof(limitDataSize)} <= {nameof(MaxDataSize)}");
            }
            this.bufferSize = bufferSize;
            this.limitDataSize = limitDataSize;
            this.callback = callback;
            this.stream = stream;
            this.receiveBuffer = new byte[this.bufferSize];
            this.receiver = new Receiver(bufferSize, limitDataSize);
            this.deserializer = new Deserializer(bufferSize, limitDataSize);
        }

        /// <summary>
        /// インスタンス破棄処理
        /// </summary>
        public virtual void Dispose()
        {
            //非同期処理に中断通知
            ctsClose.Cancel();
            //パイプストリームを破棄
            stream.Dispose();

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 受信処理開始
        /// </summary>
        /// <returns>非同期オブジェクト</returns>
        /// <exception cref="InvalidOperationException">既に実行中</exception>
        protected Task BeginReceiveAsync()
        {
            if (!receiverTask.IsCompleted)
            {
                //動作中であったら例外
                throw new InvalidOperationException("ReceiverTask is running");
            }
            receiverTask = ReceiverTask();
            return receiverTask;
        }

        /// <summary>
        /// 受信処理
        /// </summary>
        /// <returns>非同期オブジェクト</returns>
        private async Task ReceiverTask()
        {
            try
            {
                receiver.Reset();
                deserializer.Reset();
                while (!ctsClose.IsCancellationRequested)
                {
                    var readedSize = await Stream.ReadAsync(receiveBuffer, ctsClose.Token);
                    if(readedSize == 0)
                    {
                        //接続が閉じられた
                        break;
                    }
                    foreach(var packet in receiver.Feed(new ReadOnlyMemory<byte>(receiveBuffer, 0, readedSize)))
                    {
                        var data = deserializer.Feed(packet);
                        if (!data.IsEmpty)
                        {
                            callback.Invoke(new Received(this, data));
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                //キャンセル時（何もしない）
            }
            catch (Exception ex)
            {
                if(Stream.IsConnected)
                {
                    //切断関連以外の例外は通知する
                    callback.Invoke(new ExceptionTrapped(this, ex));
                }
            }
            finally
            {
                //終了時は例外を無視
                try { OnTerminatedReceiver(); } catch { };
            }
        }

        /// <summary>
        /// 送信処理は1度に1つのデータ送信しか行えないように処理するセマフォ
        /// </summary>
        private Semaphore writeSemaphore = new Semaphore(1, 1);

        /// <summary>
        /// データ送信
        /// </summary>
        /// <param name="data">送信データ</param>
        /// <param name="ct">キャンセルトークン</param>
        /// <returns>非同期オブジェクト</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="data"/>が不正</exception>
        public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct=default)
        {
            if(data.Length > limitDataSize)
            {
                //データサイズ上限を超えている
                throw new ArgumentOutOfRangeException(nameof(data),"data.Length is too long");
            }
            //セマフォ待ち
            writeSemaphore.WaitOne();
            //バッファーサイズ単位に分割して送信
            try
            {
                //キャンセルはバッファー単位の送信完了ごとにチェックする
                var cts = CancellationTokenSource.CreateLinkedTokenSource(ct, ctsClose.Token);
                foreach (var packet in Serializer.Serialize(new ConsumeBuffer(data), bufferSize))
                {
                    cts.Token.ThrowIfCancellationRequested();
                    await stream.WriteAsync(packet.Header.ToBytes());
                    await stream.WriteAsync(packet.Data);
                }
            }
            catch (OperationCanceledException)
            {
                //送信キャンセルパケット送出
                await stream.WriteAsync(Header.CancelHeader.ToBytes());
                //キャンセル例外再送出
                throw;
            }
            finally { writeSemaphore.Release(); }   //セマフォ解放
        }


        /// <summary>
        /// データ受信タスク終了時に継承先の実装を呼び出す
        /// </summary>
        protected abstract void OnTerminatedReceiver();
    }
}
