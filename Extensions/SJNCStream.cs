using Net.Myzuc.ShioLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Me.Shishioko.SJNetChat.Extensions
{
    public sealed class SJNCStream : SJNCExtension
    {
        private Dictionary<Guid, (Stream user, Stream app)> Streams = [];
        private readonly SemaphoreSlim StreamSync = new(1, 1);
        private readonly short BufferSize;
        public Func<Guid, Stream, Task> OnStream = (Guid guid, Stream stream) => Task.CompletedTask;
        public SJNCStream(string instance, short bufferSize = short.MaxValue) : base($"SJNCStream:{instance}")
        {
            BufferSize = bufferSize;
        }
        public async Task<Stream> StartStreamAsync(Guid guid, short bufferSize = short.MaxValue)
        {
            if (bufferSize < 1) throw new ArgumentException();
            await StreamSync.WaitAsync();
            try
            {
                if (Streams.ContainsKey(guid)) throw new ArgumentException();
                (Stream user, Stream app) = ChannelStream.CreatePair();
                Streams.Add(guid, (user, app));
                _ = StartSendingAsync(guid, (user, app), bufferSize);
                return user;
            }
            finally
            {
                StreamSync.Release();
            }
        }
        private async Task StartSendingAsync(Guid guid, (Stream user, Stream app) stream, short bufferSize)
        {
            try
            {
                try
                {
                    byte[] buffer = new byte[bufferSize];
                    while (true)
                    {
                        int read = await stream.app.ReadAsync(buffer);
                        if (read <= 0) break;
                        using MemoryStream packet = new();
                        packet.WriteGuid(guid);
                        packet.Write(buffer, 0, read);
                        await SendAsync(packet.ToArray());
                    }
                }
                finally
                {
                    using MemoryStream packet = new();
                    packet.WriteGuid(guid);
                    await SendAsync(packet.ToArray());
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                await StreamSync.WaitAsync();
                if (Streams.Remove(guid))
                {
                    stream.app.Dispose();
                    stream.user.Dispose();
                }
                StreamSync.Release();
            }
        }
        protected internal override async Task<Stream> OnInitializeAsync(Stream stream, bool server)
        {
            return stream;
        }
        protected internal override async Task OnReceiveAsync(byte[] data)
        {
            await StreamSync.WaitAsync();
            try
            {
                using MemoryStream packet = new(data);
                Guid guid = packet.ReadGuid();
                byte[] buffer = packet.ReadU8A((int)(packet.Length - packet.Position));
                if (!Streams.TryGetValue(guid, out (Stream user, Stream app) stream))
                {
                    if (buffer.Length <= 0) return;
                    stream = ChannelStream.CreatePair();
                    Streams.Add(guid, stream);
                    _ = StartSendingAsync(guid, stream, BufferSize);
                    await OnStream(guid, stream.user);
                }
                if (buffer.Length <= 0)
                {
                    if (!Streams.Remove(guid)) return;
                    stream.app.Dispose();
                    stream.user.Dispose();
                    return;
                }
                await stream.app.WriteAsync(buffer);
            }
            finally
            {
                StreamSync.Release();
            }
        }
    }
}
