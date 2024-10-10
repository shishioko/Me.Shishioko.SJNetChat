using Me.Shishioko.SJNetChat;
using Me.Shishioko.SJNetChat.Extensions;
using System.Drawing;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace SJNCCLI
{

    public static class Program
    {
        private static string LocalName = "User";
        //public static void Main(string[] arguments)
        public static void Main(string[] arguments)
        {
            Stopwatch uptime = Stopwatch.StartNew();
            string encryption = "True";

            //if (arguments.Length < 2)
            //{
            //ShioConsole.WriteLine("Usage: sjnccli.exe <endpoint> <name> [-noencrypt]");
            //return;
            //}

            //string[] parameters = arguments.Skip(2).ToArray();
            Console.Write("Enter Username: ");
            string LocalName = Console.ReadLine();
            Console.Write("Enter Host URL: ");
            string HostUrl = Console.ReadLine();
            Console.Clear();
            SJNCNameExtension name = new(LocalName); // = arguments[1]
            string fullAddress = HostUrl;
            int fullAddressSplit = fullAddress.LastIndexOf(':');
            using SJNC sjnc = SJNC.Connect(new DnsEndPoint(fullAddress[..fullAddressSplit], ushort.Parse(fullAddress[(fullAddressSplit + 1)..])));
            SJNCAesExtension? aes = new();
            SJNCOnlineNameListExtension nameList = new(null);
            List<string> list = [];
            nameList.OnAddAsync += async (name) => {
                list.Add(name);
            };
            nameList.OnRemoveAsync += async (name) =>
            {
                list.Remove(name);
            };
            List<SJNCExtension> extensions = [name, nameList, aes];
            sjnc.Initialize(extensions);
            foreach (SJNCExtension extension in extensions.Where(extension => !extension.Common)) ShioConsole.WriteLine($"{ComputeColor(LocalName, 32)}Extension {ComputeColor(LocalName, 64)}{extension.GetType().Name} {ComputeColor(LocalName, 32)}was not loaded!");
            ThreadPool.QueueUserWorkItem((object? _) =>
            {
                while (true)
                {
                    string message = sjnc.Receive();
                    string sender = name.AttachedName ?? "User";

                    ShioConsole.WriteLine($"{ComputeColor(sender, 64)}{sender}{ComputeColor(sender, 128)}: {message}");
                }
            });
            while (true)
            {
                string input = ShioConsole.ReadLine(input => $"{ComputeColor(LocalName, 128)}{input}");
                if (input == "?uptime")
                {
                    ShioConsole.WriteLine($"Program Uptime: {uptime.Elapsed}");
                    continue;
                };
                if (input == "?online")
                {
                    string users = string.Join(", ", list);
                    ShioConsole.WriteLine($"Users online: {users}");
                    continue;
                };
                sjnc.Send(input);
            }
        }
        private static string ComputeColor(string input, int shift)
        {
            byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
            Color color = Color.FromArgb(hash[0] / 2 + shift, hash[1] / 2 + shift, hash[2] / 2 + shift);
            return $"\u001b[38;2;{color.R};{color.G};{color.B}m";
        }
    }
}
