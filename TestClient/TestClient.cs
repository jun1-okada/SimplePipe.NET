using Abt.Comm.SimplePipe;
using System.Text;

//名前付きパイプ名称 '\\.\pipe\' プレフィックスは不要
const string PipeName = @"SimplePipeTest";

//文字列-バイト列相互変換
var encoding = new UnicodeEncoding(false, false);

//反復回数
const int Repeat = 1000;
int remain = Repeat;

try
{
    //受信完了イベント
    var receivedEvent = new ManualResetEvent(false);

    //クライアントインスタンス生成
    using var client = new SimpleNamedPipeClient(PipeName, (pipeEvent) =>
    {
        //コールバック処理
        switch (pipeEvent)
        {
            case Disconnected:
                //切断
                Console.WriteLine("disconnected");
                break;
            case Received received:
                //データ受信
                // 受信データを文字列化してコンソールへ表示
                var str = encoding.GetString(received.Data.Span);
                Console.WriteLine(str);
                if (0 == Interlocked.Decrement(ref remain))
                {
                    //送信したデータがすべてechoされたので終了
                    receivedEvent.Set();
                }
                break;
            case ExceptionTrapped ex:
                //未捕捉例外
                Console.WriteLine($"caught exception: {ex}");
                //継続はできないのでイベントをシグナルとする
                receivedEvent.Set();
                break;
        }
    });

    //送信処理をRepeat回反復する
    for (int i = 0; i < Repeat; ++i)
    {
        var str = $"HELLO WORLD! [{i}]";
        await client.WriteAsync(encoding.GetBytes(str));
    }
    //データ通知完了まち
    receivedEvent.WaitOne();
}
catch(Exception ex)
{
    Console.Error.WriteLine(ex.ToString());
}
