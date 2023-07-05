using System;
using System.IO.Pipes;

namespace Abt.Comm.SimplePipe
{
    /// <summary>
    /// 名前付きパイプ クライアント
    /// </summary>
    public class SimpleNamedPipeClient : SimpleNamedPipeBase
    {
        /// <summary>
        /// .NET名前付きパイプクライアントストリームインスタンス
        /// </summary>
        private readonly NamedPipeClientStream pipeClient;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="name">名前付きパイプ名称. プリフィックスの '\\pipe\.\' は不要</param>
        /// <param name="callback">イベントコールバック</param>
        /// <param name="bufferSize">受信/送信バッファーサイズ</param>
        /// <param name="limitDataSize">データサイズ上限</param>
        /// <param name="connectTimeout">接続タイムアウト[ミリ秒]</param>
        /// <exception cref="TimeoutException">接続タイムアウト</exception>
        /// <exception cref="ArgumentException">パラメータが不正</exception>
        /// <exception cref="ArgumentOutOfRangeException">パラメータが不正</exception>
        public SimpleNamedPipeClient(string name, Action<IPipeEvent> callback, int bufferSize = TypicalbufferSize, int limitDataSize = MaxDataSize, int connectTimeout=50)
            : base(callback,bufferSize, limitDataSize, new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            //パイプストリームをクライアント用に設定
            pipeClient = (NamedPipeClientStream)Stream;
            //接続処理
            pipeClient.Connect(connectTimeout);
            //データ受信タスク開始
            BeginReceiveAsync();
        }

        /// <summary>
        /// 受信タスク終了時の実装
        /// </summary>
        protected override void OnTerminatedReceiver()
        {
            //切断イベント発行
            callback.Invoke(new Disconnected(this));
            //終了時の例外は無視
            try
            {
                //クライアントは切断でインスタンスは利用不可となる
                Dispose();
            }
            catch { }
        }
    }
}
