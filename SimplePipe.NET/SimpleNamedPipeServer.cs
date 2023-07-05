using System.IO.Pipes;
using System.Runtime.Versioning;

namespace Abt.Comm.SimplePipe
{
    /// <summary>
    /// 名前付きパイプサーバー
    /// </summary>
    public class SimpleNamedPipeServer : SimpleNamedPipeBase
    {
        /// <summary>
        /// .NET名前付きパイプサーバーストリームインスタンス
        /// </summary>
        private readonly NamedPipeServerStream pipeServer;

        /// <summary>
        /// 接続待ちタスク非同期オブジェクト
        /// </summary>
        private Task conectionTask = Task.CompletedTask;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="name">名前付きパイプ名称. プリフィックスの '\\pipe\.\' は不要</param>
        /// <param name="callback">イベントコールバック</param>
        /// <param name="bufferSize">受信/送信バッファーサイズ</param>
        /// <param name="limitDataSize">データサイズ上限</param>
        /// <exception cref="IOException">すでに同名のパイプサーバーが存在する、他</exception>
        /// <exception cref="ArgumentException">パラメータが不正</exception>
        /// <exception cref="ArgumentOutOfRangeException">パラメータが不正</exception>
        public SimpleNamedPipeServer(
            string name
            , Action<IPipeEvent> callback
            , int bufferSize = TypicalbufferSize
            , int limitDataSize = MaxDataSize)
            : base(callback, bufferSize, limitDataSize, new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, bufferSize, bufferSize))
        {
            pipeServer = (NamedPipeServerStream)Stream;
            conectionTask = WaitForConnectionAsync();
        }

        /// <summary>
        /// ACL情報つきのコンストラクタ
        /// </summary>
        /// <param name="name">名前付きパイプ名称. プリフィックスの '\\pipe\.\' は不要</param>
        /// <param name="callback">イベントコールバック</param>
        /// <param name="pipeSecurity">パイプのアクセス制御クラス</param>
        /// <param name="bufferSize">受信/送信バッファーサイズ</param>
        /// <param name="limitDataSize">データサイズ上限</param>
        /// <exception cref="IOException">すでに同名のパイプサーバーが存在する、他</exception>
        /// <exception cref="ArgumentException">パラメータが不正</exception>
        /// <exception cref="ArgumentOutOfRangeException">パラメータが不正</exception>
        [SupportedOSPlatform("windows")]
        public SimpleNamedPipeServer(
            string name
            , Action<IPipeEvent> callback
            , PipeSecurity? pipeSecurity
            , int bufferSize=TypicalbufferSize
            , int limitDataSize=MaxDataSize)
            : base(callback, bufferSize, limitDataSize, NamedPipeServerStreamAcl.Create(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, bufferSize, bufferSize, pipeSecurity))
        {
            //パイプサーバーストリームの設定
            pipeServer = (NamedPipeServerStream)Stream;
            //接続待ち開始
            conectionTask = WaitForConnectionAsync();
        }

        /// <summary>
        /// インスタンス破棄処理
        /// </summary>
        public override void Dispose()
        {
            if (OperatingSystem.IsWindows())
            {
                //すでに閉じている場合の例外は無視
                try
                {
                    //接続先がデータを読み取りきるまで待つ
                    pipeServer.WaitForPipeDrain();
                }
                catch { }
            }
            //基底呼び出し
            base.Dispose();

            //Closeイベント発行
            callback(new Closed(this));
        }

        /// <summary>
        /// 接続中のクライアントを切断する
        /// </summary>
        public void Disconnect()
        {
            //すでに閉じている場合の例外は無視
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    //接続先がデータを読み取りきるまで待つ
                    pipeServer.WaitForPipeDrain();
                }
            }
            catch { }
            try
            {
                //クライアントを切断
                pipeServer.Disconnect();
            }
            catch { }
        }

        /// <summary>
        /// 受信タスク終了時の実装
        /// </summary>
        protected override void OnTerminatedReceiver()
        {
            //クライアントを切断
            Disconnect();
            //切断イベント発行
            callback(new Disconnected(this));
            //次のクライアント接続を待つ
            conectionTask = WaitForConnectionAsync();
        }

        /// <summary>
        /// 次のクライアント接続待ち
        /// </summary>
        /// <returns>非同期オブジェクト</returns>
        private Task WaitForConnectionAsync()
        {
            if (!conectionTask.IsCompleted)
            {
                //すでに実行済み
                return conectionTask;
            }

            return pipeServer.WaitForConnectionAsync(ctsClose.Token).ContinueWith(t =>
            {
                //WaitForConnectionAsyncの後続処理
                if (t.IsCanceled)
                {
                    return;
                }
                if (t.IsFaulted)
                {
                    if(t.Exception is not null)
                    {
                        //捕捉していない例外を通知
                        callback.Invoke(new ExceptionTrapped(this, t.Exception));
                    }
                    return;
                }
                //データ受信タスク開始
                BeginReceiveAsync();
                //接続イベント発行
                callback.Invoke(new Connected(this));
            });
        }
    }
}