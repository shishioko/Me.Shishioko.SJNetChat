using Net.Myzuc.ShioLib;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading.Tasks;
using System.Net;

namespace Me.Shishioko.SJNetChat.Extensions
{
    public sealed class SJNCNameExtension : SJNCExtension
    {
        public string? Name { get; private set; }
        public string? AttachedName { get; private set; } = null;
        private bool Server = false;
        public SJNCNameExtension(string? name)
        {
            if (name is not null) Contract.Assert(name.Length <= 32);
            Name = name;
        }
        protected internal override string Identify()
        {
            return "SJNCName";
        }
        protected internal override async Task<Stream> OnInitializeAsync(Stream stream, bool server)
        {
            Contract.Assert(server != Name is not null);
            Server = server;
            if (server) Name = await stream.ReadStringAsync(SizePrefix.U8, byte.MaxValue);
            else await stream.WriteStringAsync(Name!, SizePrefix.U8, byte.MaxValue);
            if (Name.Length > 32) throw new ProtocolViolationException("Name longer than 32 characters!");
            return stream;
        }
        protected internal override async Task OnTextSendAsync(Stream stream, string message)
        {
            if (!Server) return;
            Contract.Assert(AttachedName.Length <= 32);
            await stream.WriteStringAsync(AttachedName!, SizePrefix.U8, byte.MaxValue);
        }
        protected internal override async Task OnTextReceiveAsync(Stream stream, string message)
        {
            if (Server) return;
            AttachedName = await stream.ReadStringAsync(SizePrefix.U8, byte.MaxValue);
            if (AttachedName.Length > 32) throw new ProtocolViolationException("Name longer than 32 characters!");
        }
        protected internal override Task OnReceiveAsync(byte[] data)
        {
            return Task.CompletedTask;
        }
    }
}
