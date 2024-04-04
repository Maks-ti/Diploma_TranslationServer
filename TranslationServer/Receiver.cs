using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using GraphTransferLibrary;
using Newtonsoft.Json.Linq;

namespace TranslationServer
{
    internal class Receiver
    {
        private readonly HttpListener _httpListener;
        private readonly ConcurrentQueue<WebSocketCommand> _queue;

        internal Receiver(ConcurrentQueue<WebSocketCommand> queue, string[] prefixes)
        {
            // сам сервер
            _httpListener = new HttpListener();

            // очередь задач обработчика вебсокетов
            _queue = queue;

            // префиксы которые слушаем
            foreach (string prefix in prefixes) 
            {
                _httpListener.Prefixes.Add(prefix);
            }
        }

        public async Task StartAsync(CancellationToken token)
        {
            _httpListener.Start();
            Console.WriteLine("Server started. Listening for requests...");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();

                    await Task.Run(() => ProcessRequest(context));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }

        // Метод для обработки JSON-RPC запроса и вызова соответствующей логики
        private async void ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                string requestBody;

                using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
                {
                    // чтение тела запроса
                    requestBody = await reader.ReadToEndAsync();
                }

                // Десериализация JSON-RPC запроса
                RpcRequest? rpcRequest = JsonConvert.DeserializeObject<RpcRequest>(requestBody);

                // Обработка запроса
                var result = ProcessRpcRequest(rpcRequest);

                // Формирование JSON-RPC ответа
                string responseJson = JsonConvert.SerializeObject(new RpcResponse
                {
                    Result = result,
                    Error = null,
                    JsonRpc = "2.0"
                });

                byte[] buffer = Encoding.UTF8.GetBytes(responseJson);
                response.ContentLength64 = buffer.Length;

                using (var output = response.OutputStream)
                {
                    await output.WriteAsync(buffer, 0, buffer.Length);
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing request: {ex.Message}");
                Console.WriteLine(ex);

                // Возвращаем ошибку в JSON-RPC формате

                string errorJson = JsonConvert.SerializeObject(new RpcResponse
                {
                    Result = null,
                    Error = new { message = "Internal Server Error" },
                    JsonRpc = "2.0"
                });
                
                byte[] errorBuffer = Encoding.UTF8.GetBytes(errorJson);
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.ContentLength64 = errorBuffer.Length;

                using (var output = response.OutputStream)
                {
                    await output.WriteAsync(errorBuffer, 0, errorBuffer.Length);
                }
            }
            finally
            {
                response.Close();
            }
        }

        private object ProcessRpcRequest(RpcRequest rpcRequest)
        {
            // добавляем в очередь задачу
            _queue?.Enqueue(new WebSocketCommand
            {
                Command = rpcRequest.Command,
                ManagerId = rpcRequest.ManagerId,
            });

            Console.WriteLine($"Пришёл следующий RpcRequest:\n{JsonConvert.SerializeObject(rpcRequest, Formatting.Indented)}\n");

            return null;
        }


    }
}



