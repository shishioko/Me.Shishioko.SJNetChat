using Net.Myzuc.ShioLib;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Me.Shishioko.SJNetChat
{
    public sealed class SJNC : IDisposable
    {
        public static SJNC FromStream(Stream stream, bool server, bool keepOpen = true)
        {
            return new(stream, server, keepOpen);
        }
        public static async Task<SJNC> ConnectAsync(EndPoint endpoint)
        {
            Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(endpoint);
            return new(new NetworkStream(socket, true), false, false);
        }
        public static SJNC Connect(IPEndPoint endpoint)
        {
            Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(endpoint);
            return new(new NetworkStream(socket, true), false, false);
        }
        internal Stream Stream;
        internal SJNCExtension[]? Channels = null;
        private readonly SemaphoreSlim SyncOut = new(1, 1);
        private readonly SemaphoreSlim SyncIn = new(1, 1);
        private readonly bool Server;
        private readonly bool KeepOpen;
        public SJNC(Stream stream, bool server, bool keepOpen)
        {
            Stream = stream;
            Server = server;
            KeepOpen = keepOpen;
        }
        public async Task InitializeAsync(IEnumerable<SJNCExtension>? extensions = null)
        {
            await SyncOut.WaitAsync();
            Contract.Assert(Channels is null);
            SJNCExtension[] extensionList = (extensions ?? []).ToArray();
            foreach (SJNCExtension extension in extensionList)
            {
                await extension.Sync.WaitAsync();
                Contract.Assert(extension.SJNC is null);
                extension.SJNC = this;
                extension.Sync.Release();
            }
            Dictionary<string, SJNCExtension> extensionMap = [];
            await Stream.WriteU16Async(0);
            ushort remoteVersion = await Stream.ReadU16Async();
            Contract.Assert(extensionList.Length <= 65534);
            await Stream.WriteU16Async((ushort)extensionList.Length);
            for (int i = 0; i < extensionList.Length; i++)
            {
                SJNCExtension extension = extensionList[i];
                string extensionName = extension.Identify();
                extensionMap.Add(extensionName, extension);
                await Stream.WriteStringAsync(extensionName, SizePrefix.U8, byte.MaxValue, Encoding.UTF8);
            }
            string[] remoteExtensions = new string[await Stream.ReadU16Async()];
            List<SJNCExtension> commonExtensions = [];
            List<SJNCExtension> initializedExtensions = [];
            for (int i = 0; i < remoteExtensions.Length; i++)
            {
                string extensionName = await Stream.ReadStringAsync(SizePrefix.U8, byte.MaxValue, Encoding.UTF8);
                if (!extensionMap.TryGetValue(extensionName, out var extension)) continue;
                commonExtensions.Add(extension);
                if (Server) continue;
                extension.Unlocked = true;
                Stream = await extension.OnInitializeAsync(Stream, false);
                extension.Unlocked = false;
                initializedExtensions.Add(extension);
            }
            if (Server)
            {
                for (int i = 0; i < extensionList.Length; i++)
                {
                    SJNCExtension extension = extensionList[i];
                    if (!commonExtensions.Contains(extension)) continue;
                    extension.Unlocked = true;
                    Stream = await extension.OnInitializeAsync(Stream, true);
                    extension.Unlocked = false;
                    initializedExtensions.Add(extension);
                }
            }
            Channels = [..initializedExtensions];
            for (int i = 0; i < Channels.Length; i++)
            {
                Channels[i].Channel = (ushort)(i + 1);
            }
            SyncOut.Release();
        }
        public void Dispose()
        {
            if (!KeepOpen) Stream.Dispose();
            SyncIn.Dispose();
            SyncOut.Dispose();
        }
        public Task SendAsync(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            if (data.Length > byte.MaxValue) throw new ArgumentException("Message exceeds 256 byte limit!");
            return SendAsync(0, data);
        }
        public void Send(string message)
        {
            SendAsync(message).Wait();
        }
        public async Task<string> ReceiveAsync()
        {
            Contract.Assert(Channels is not null);
            await SyncIn.WaitAsync();
            try
            {
                while (true)
                {
                    ushort channel = await Stream.ReadU16Async();
                    byte[] data = await Stream.ReadU8AAsync(SizePrefix.U16, ushort.MaxValue);
                    if (channel == 0)
                    {
                        string message = Encoding.UTF8.GetString(data);
                        if (message.Length > byte.MaxValue) throw new ProtocolViolationException();
                        return message;
                    }
                    if (channel >= Channels.Length) throw new ProtocolViolationException();
                    await Channels[channel].OnReceiveAsync(data);
                }
            }
            finally
            {
                SyncIn.Release();
            }
        }
        internal async Task SendAsync(ushort channel, byte[] data)
        {
            Contract.Assert(Channels is not null);
            await SyncOut.WaitAsync();
            try
            {
                await Stream.WriteU16Async(channel);
                await Stream.WriteU8AAsync(data, SizePrefix.U16, ushort.MaxValue);
            }
            finally
            {
                SyncOut.Release();
            }
        }
    }
}
