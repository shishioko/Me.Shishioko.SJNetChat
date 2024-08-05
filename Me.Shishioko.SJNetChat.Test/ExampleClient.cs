using Me.Shishioko.SJNetChat.Extensions;
using System.Net;

namespace Me.Shishioko.SJNetChat.Test
{
    public static class ExampleClient
    {
        public static async Task Main()
        {
            Console.WriteLine("Please input a name to use.");
            SJNCNameExtension name = new(Console.ReadLine());
            Console.WriteLine("Please input hostname to connect to.");
            string? endpoint = Console.ReadLine();
            if (endpoint is null) return;
            string[] split = endpoint.Split(':', 2);
            SJNC sjnc = await SJNC.ConnectAsync(new DnsEndPoint(split[0], ushort.Parse(split[1])));
            await sjnc.InitializeAsync([name]);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                while (true)
                {
                    string? message = Console.ReadLine();
                    if (message is null) continue;
                    sjnc.SendAsync(message);
                }
            });
            while (true)
            {
                Console.WriteLine($"{await sjnc.ReceiveAsync()}");
            }
        }
    }
}
