
using TranslationServer;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Fleck;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using System.Collections;

class Program
{
    // очередь задач для обработчика вебсокетов
    static ConcurrentQueue<WebSocketCommand> _queue = new();
    // список выполненных задач (история сообщений)
    static List<WebSocketCommand> history = new();

    // cancelation token
    static CancellationTokenSource cancellationTokenSource = new();

    // all connections
    static List<IWebSocketConnection> allConnections = new();

    // задача слушателя json-rpc сообщений
    static Task _receiverTask;

    // задача обработчика очереди
    static Task _queueHandlerTask;


    static async Task Main(string[] args)
    {
        // слушатель
        string[] urls = { "http://localhost:9090/" };
        Receiver receiver = new(_queue, urls);

        // апускаем в отдельном потоке
        _receiverTask = Task.Run(async () => await receiver.StartAsync(cancellationTokenSource.Token));

        /// /// /// webSocketServer
        // server
        WebSocketServer server = new("ws://0.0.0.0:8181");
        // запускаем сервер
        server.Start(socket =>
        {
            socket.OnOpen = () => {
                Console.WriteLine("Open!");
                
                allConnections.Add(socket);
                InitializeSocket(socket);
            };
            socket.OnClose = () => {
                Console.WriteLine("Close!");
                
                allConnections.Remove(socket);
            };
            socket.OnMessage = message => {
                Console.WriteLine(message);

            };
        });

        _queueHandlerTask = Task.Run(async () => await ProcessMessages(cancellationTokenSource.Token));

        Console.WriteLine("Server started on ws://localhost:8181");
        Console.ReadLine(); // Держим консоль открытой
        
        cancellationTokenSource.Cancel();

        _receiverTask.Wait();
        _queueHandlerTask.Wait();
        return;
    }

    static void InitializeSocket(IWebSocketConnection socket)
    {
        // отправляем все сообщения из истории
        history.ForEach(msg =>
            socket.Send(JsonConvert.SerializeObject(msg))
            );
    }

    static async Task ProcessMessages(CancellationToken token)
    {
        bool hasElement;
        do
        {
            hasElement = _queue.TryDequeue(out WebSocketCommand command);
            if (hasElement)
            {
                BroadcastMessage(JsonConvert.SerializeObject(command));

                history.Add(command);
            }
            else
            {
                await Task.Delay(100);
            }
        }
        while (!token.IsCancellationRequested || hasElement);
    }

    static void BroadcastMessage(string message)
    {
        allConnections.ForEach(socket =>
        {
            if (socket.IsAvailable)
            {
                socket.Send(message);
            }
        });
    }


}
