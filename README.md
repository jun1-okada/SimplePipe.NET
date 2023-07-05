# SimplePipe.NET
.NET6用の名前付きパイプを利用するシンプルなライブラリ。ネイティブ(C++)版の [SimplePipe](https://github.com/jun1-okada/SimplePipe) と相互接続可能。

同一Windowsマシン内でプロセス間通信を行うことを想定してる。ネイティブ(C++)版との併用で、ネイティブで開発したアプリ、サービスやUMDFと通信を行うことができる。

# 特徴
 - ローカルマシン内のアプリ間での1対1の通信を想定した実装。
 - 送信と受信イベントが1:1で対応する。
 - 非同期処理専用。

# 使い方
`SimplePipe.NET.dll` またはプロジェクトを参照する。

サーバーとクライアントは `SimpleNamedPipeServer` と `SimpleNamedPipeClient` の組み合わせか、ネイティブ(C++)版の`SimpleNamedPipeServer<BUF_SIZE,LIMIT>` , `SimpleNamedPipeClient<BUF_SIZE,LIMIT>` と組み合わせて利用する。

データ長保証のためヘッダー情報を付加しており、上記の組み合わせでないと正常に動作しない。

送信/受信バッファーサイズと1度に送信可能なデータサイズはコンストラクタの引数で指定する。省略した場合は推奨値を設定する。

送信データサイズ上限は、.NETのバイト配列のサイズ制限の `2,147,483,591`バイトまで設定可能。[Array クラス#注釈](https://learn.microsoft.com/ja-jp/dotnet/api/system.array?redirectedfrom=MSDN&view=net-6.0#remarks)

送信/受信バッファーサイズと送信データサイズ上限は、サーバーとクライアントで異なった値を指定しても動作するが、どちらかの送信データサイズ上限を超えるデータを受信した場合は継続不能なエラーとなる。

## サーバー
`SimpleNamedPipeServer` でサーバーインスタンスを生成する。

### コンストラクタ
```cs
public SimpleNamedPipeServer(
    string name
    , Action<IPipeEvent> callback
    , int bufferSize = TypicalbufferSize
    , int limitDataSize = MaxDataSize)
```

- `name`: パイプ名称を指定する。 プレフィックスの `\\.\pipe\` は省略して指定する。
- `callback`: イベントコールバック
- `bufferSize`: バッファーサイズ
- `limitDataSize`: データサイズ上限

エラー時には以下の例外を送出する
 - `IOException`: すでに同名のサーバーが存在する場合、など
 - `ArgumentException`: パラメータが不正
 - `ArgumentOutOfRangeException`: パラメータが不正

インスタンス生成直後は、クライアント接続の待機状態となる。

イベントコールバックの注意点として、`Received` イベントの `Received.Data` はそのコールバック中でのみ有効であり、コールバック終了後は内容が保証されない。__コールバック外で利用する場合は必ずコピーをすること__。

コンストラクタのコード例
```cs
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
```

#### パイプセキュリティの設定
以下のコンストラクタを利用することでパイプセキュリティを設定可能となる。

```cs
public SimpleNamedPipeServer(
    string name
    , Action<IPipeEvent> callback
    , PipeSecurity? pipeSecurity       //パイプセキュリティ
    , int bufferSize=TypicalbufferSize
    , int limitDataSize=MaxDataSize)
```

### WriteAsync
データ送信には `WriteAsync` を利用する。

```cs
public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct=default)
```
- `data`: 送信データ
- `ct`: キャンセルトークン

送信データサイズはコンストラクタで指定した `limitDataSize` より大きいと `ArgumentOutOfRangeException` 例外を送出する。

また、接続対象の`limitDataSize` を超えていた場合は、切断される。

送信処理はスレッドセーフである。複数スレッドで同時に送信した場合の順番は不定である。

### Disconnect
接続中のクライアントを切断する場合は、`Disconnect`を利用する。

切断後は次のクライアント接続のための待機状態となる。

### Dispose
`IDisposable.Dispose` のオーバーライドである。

名前付きパイプサーバーを破棄する。接続中のクライアントは切断する。

## クライアント
`SimpleNamedPipeClient` でクライアントインスタンスを生成する。

### コンストラクタ
```cs
public SimpleNamedPipeClient(
    string name, 
    Action<IPipeEvent> callback, 
    int bufferSize = TypicalbufferSize, 
    int limitDataSize = MaxDataSize, 
    int connectTimeout=50)
```
- `name`: パイプ名称を指定する。 プレフィックスの `\\.\pipe\` は省略して指定する。
- `callback`: イベントコールバック
- `bufferSize`: バッファーサイズ
- `limitDataSize`: データサイズ上限
- `connectTimeout`: 接続待ちタイムアウト[ミリ秒]

エラー時には以下の例外を送出する
 - `TimeoutException`: 接続タイムアウト。`connectTimeout` が経過しても接続できなかった場合。
 - `ArgumentException`: パラメータが不正
 - `ArgumentOutOfRangeException`: パラメータが不正

### WriteAsync
データ送信には `WriteAsync` を利用する。利用方法はサーバーと同様。

### Dispose
`IDisposable.Dispose` のオーバーライドである。

名前付きクライアントを破棄する。サーバーとの接続は切断する。

# サンプル
## サーバーのサンプルコード
```cs
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
```

## クライアントのサンプルコード
``` cs
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
```