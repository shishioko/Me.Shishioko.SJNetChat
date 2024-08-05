using Net.Myzuc.ShioLib;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading.Tasks;

namespace Me.Shishioko.SJNetChat.Extensions
{
    public sealed class SJNCNameExtension : SJNCExtension
    {
        public string? Name { get; private set; }
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
            if (server) Name = await stream.ReadStringAsync(SizePrefix.U8, 32);
            else await stream.WriteStringAsync(Name!, SizePrefix.U8, 32);
            return stream;
        }
        protected internal override Task OnReceiveAsync(byte[] data)
        {
            return Task.CompletedTask;
        }
    }
}
