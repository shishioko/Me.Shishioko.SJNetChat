using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net;
using Me.Shishioko.SJNetChat.Extensions;

namespace Me.Shishioko.SJNetChat.Test
{
    public static class ExampleServer
    {
        private static readonly ConcurrentDictionary<EndPoint, SJNC> Clients = [];
        public static async Task Main()
        {
            Console.WriteLine("Please input port to listen on.");
            ushort port;
            while (!ushort.TryParse(Console.ReadLine(), out port))
            {
                Console.WriteLine("Invalid port number specified!");
            }
            using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
            socket.Listen();
            Console.WriteLine($"Listening on {socket.LocalEndPoint}.");
            while (true)
            {
                Socket client = await socket.AcceptAsync();
                if (client.LocalEndPoint is null)
                {
                    client.Dispose();
                    continue;
                }
                _ = ServeAsync(client);
            }
        }
        private static async Task ServeAsync(Socket socket)
        {
            try
            {
                Console.WriteLine($"{socket.RemoteEndPoint} connected.");
                SJNC sjnc = SJNC.FromStream(new NetworkStream(socket, true), true);
                SJNCNameExtension name = new(null);
                await sjnc.InitializeAsync([name]);
                if (!Clients.TryAdd(socket.RemoteEndPoint!, sjnc)) return;
                while (true)
                {
                    string message = await sjnc.ReceiveAsync();
                    string formatted = $"{name.Name ?? socket.RemoteEndPoint?.ToString()}: {message}";
                    Console.WriteLine(formatted);
                    _ = Task.WhenAll(Clients.Values.Select(async (client) =>
                    {
                        try
                        {
                            await client.SendAsync(formatted);
                        }
                        catch (Exception)
                        {

                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex}");
            }
            finally
            {
                Clients.Remove(socket.LocalEndPoint!, out _);
                socket.Dispose();
            }
            Console.WriteLine($"{socket.RemoteEndPoint} disconnected.");
        }
    }
}
