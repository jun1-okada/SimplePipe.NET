using Abt.Comm.SimplePipe;
using System.Text;

//名前付きパイプ名称 '\\.\pipe\' プレフィックスは不要
const string PipeName = @"SimplePipeTest";

//文字列-バイト列相互変換
var encoding = new UnicodeEncoding(false, false);

try
{
    //サーバーインスタンス生成
    using var server = new SimpleNamedPipeServer(PipeName, (pipeEvent) =>
    {
        //コールバック処理
        switch (pipeEvent)
        {
            case Connected:
                //クライアント接続（サーバーのみのイベント）
                Console.WriteLine("connected");
                break;
            case Disconnected:
                //クライアント切断
                Console.WriteLine("disconnected");
                break;
            case Received received:
                //データ受信
                // 受信した文字列に "echo: "を付加して返信する
                var str = encoding.GetString(received.Data.Span);
                Console.WriteLine(str);
                pipeEvent.Pipe.WriteAsync(encoding.GetBytes($"echo: {str}")).Wait();
                break;
            case Closed:
                //パイプクローズ（サーバーのみのイベント）
                Console.WriteLine("closed");
                break;
            case ExceptionTrapped ex:
                //未捕捉例外
                Console.WriteLine($"caught exception: {ex}");
                break;
        }
    });

    //Qキーが押下されるまでサーバーを実行
    Console.WriteLine("Press 'Q' to exit");
    while (true)
    {
        if (Console.ReadKey(false).Key == ConsoleKey.Q)
        {
            break;
        }
    }
}
catch(Exception ex)
{
    Console.Error.WriteLine(ex.ToString());
}
