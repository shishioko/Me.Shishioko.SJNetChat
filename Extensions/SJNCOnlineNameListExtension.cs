using Net.Myzuc.ShioLib;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Me.Shishioko.SJNetChat.Extensions
{
    public sealed class SJNCOnlineNameListExtension : SJNCExtension
    {
        private readonly List<string> InternalList = [];
        public IReadOnlyList<string> List => InternalList.AsReadOnly();
        private readonly SemaphoreSlim ListSync = new(1, 1);
        private readonly bool Server;
        public SJNCOnlineNameListExtension(IEnumerable<string>? list = null)
        {
            Server = list is not null;
            if (list is not null) InternalList = list.ToList();
        }
        public Task Add(string name)
        {
            Contract.Assert(Server);
            Contract.Assert(name.Length <= 32);
            using MemoryStream packetOut = new();
            packetOut.WriteBool(true);
            packetOut.WriteString(name, SizePrefix.U8, byte.MaxValue, Encoding.UTF8);
            return SendAsync(packetOut.ToArray());
        }
        public Task Remove(string name)
        {
            Contract.Assert(Server);
            Contract.Assert(name.Length <= 32);
            using MemoryStream packetOut = new();
            packetOut.WriteBool(false);
            packetOut.WriteString(name, SizePrefix.U8, byte.MaxValue, Encoding.UTF8);
            return SendAsync(packetOut.ToArray());
        }
        protected internal override string Identify()
        {
            return "SJNCOnlineNameList";
        }
        protected internal override async Task<Stream> OnInitializeAsync(Stream stream, bool server)
        {
            Contract.Assert(Server == server);
            await ListSync.WaitAsync();
            try
            {
                if (Server)
                {
                    foreach(string name in InternalList)
                    {
                        Contract.Assert(name.Length <= 32);
                        await stream.WriteBoolAsync(true);
                        await stream.WriteStringAsync(name, SizePrefix.U8, byte.MaxValue, Encoding.UTF8);
                    }
                    await stream.WriteBoolAsync(false);
                }
                else
                {
                    while (await stream.ReadBoolAsync())
                    {
                        string name = await stream.ReadStringAsync(SizePrefix.U8, byte.MaxValue, Encoding.UTF8);
                        if (name.Length > 32) throw new ProtocolViolationException("Name longer than 32 characters!");
                        InternalList.Add(name);
                    }
                }
            }
            finally
            {
                ListSync.Release();
            }
            return stream;
        }
        protected internal override Task OnTextSendAsync(Stream stream, string message)
        {
            return Task.CompletedTask;
        }
        protected internal override Task OnTextReceiveAsync(Stream stream, string message)
        {
            return Task.CompletedTask;
        }
        protected internal override async Task OnReceiveAsync(byte[] data)
        {
            if (Server) throw new ProtocolViolationException();
            using MemoryStream packetIn = new(data);
            bool action = packetIn.ReadBool();
            string name = packetIn.ReadString(SizePrefix.U8, byte.MaxValue, Encoding.UTF8);
            await ListSync.WaitAsync();
            try
            {
                if (action) InternalList.Add(name);
                else InternalList.Remove(name);
            }
            finally
            {
                ListSync.Release();
            }
        }
    }
}
