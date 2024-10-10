using Me.Shishioko.SJNetChat;
using Me.Shishioko.SJNetChat.Extensions;
using System.Net;
using System.Net.NetworkInformation;
using System.Management;
namespace ConsoleApp1
{
    public static class ExampleClient
    {
        public static async Task Main()
        {
            SJNCNameExtension name = new("JBot");
            string? endpoint = "eu0.dev.myzuc.net:1338";
            string[] split = endpoint.Split(':', 2);
            SJNC sjnc = await SJNC.ConnectAsync(new DnsEndPoint(split[0], ushort.Parse(split[1])));
           

            SJNCOnlineNameListExtension nameList = new(null);
            List<string> list = [];
            nameList.OnAddAsync += async (name) => {
                list.Add(name);
            };
            nameList.OnRemoveAsync += async (name) =>
            {
                list.Remove(name);
            };
            await sjnc.InitializeAsync([name, nameList]);

            sjnc.SendAsync("Hello I'm JaegerBot please run !help for working commands");
            while (true)
            {
                var receivedMessage = await sjnc.ReceiveAsync();
                Console.WriteLine(name.AttachedName + ": " + receivedMessage);
                if (receivedMessage.Contains("!pong"))
                {
                    sjnc.SendAsync("ping");
                }
                if (receivedMessage.Contains("!yourmother"))
                {
                    sjnc.SendAsync("*TF2 SPY NOISES*");
                }
                if (receivedMessage.Contains("!users"))
                {
                    string users = string.Join(", ", list);
                    sjnc.SendAsync(users);
                }
                if (receivedMessage.Contains("!ping"))
                {
                    var ping = new Ping();
                    PingReply reply = await ping.SendPingAsync("eu0.dev.myzuc.net");
                    string v = $"RoundTrip time: {reply.RoundtripTime} ms";
                    string roundTripTimeMessage = v;
                    if (reply.Status == IPStatus.Success)
                    {
                        sjnc.SendAsync(roundTripTimeMessage);
                    };
                }
                if (receivedMessage == "!help")
                {
                    sjnc.SendAsync("\nping - Ping the homeserver\npong - ping\nclear - clears the chat\nusers - Displays currently connected users");
                }
                if (receivedMessage == "!kill")
                {
                    System.Environment.Exit(0);
                }
                if (receivedMessage == "!clear")
                {
                    sjnc.SendAsync("\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n");
                }
                if (receivedMessage == "!fetch")
                {
                    ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_Processor");
                    ManagementObjectCollection queryCollection = searcher.Get();
                    foreach (ManagementObject m in queryCollection)
                    {
                        string cpuInfo = "";
                        cpuInfo = $"{m["Name"]}";
                        await sjnc.SendAsync($"\nCPU: {cpuInfo}\n");
                    }
                };
            }
        }
    }
}
