using Net.Myzuc.ShioLib;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Me.Shishioko.SJNetChat
{
    public abstract class SJNCExtension
    {
        public bool Common { get; internal set; } = false;
        internal SJNC? SJNC = null;
        internal bool Unlocked = false;
        internal ushort Channel = 0;
        internal readonly SemaphoreSlim Sync = new(1, 1);
        public SJNCExtension()
        {

        }
        internal protected abstract string Identify();
        internal protected abstract Task<Stream> OnInitializeAsync(Stream stream, bool server);
        internal protected abstract Task OnTextSendAsync(Stream stream, string message);
        internal protected abstract Task OnTextReceiveAsync(Stream stream, string message);
        internal protected abstract Task OnReceiveAsync(byte[] data);
        protected async Task SendAsync(byte[] data)
        {
            Contract.Assert(Unlocked);
            Contract.Assert(SJNC.Channels is not null);
            await SJNC.SyncOut.WaitAsync();
            try
            {
                await SJNC.Stream.WriteU16Async(Channel);
                await SJNC.Stream.WriteU8AAsync(data, SizePrefix.U16, ushort.MaxValue);
            }
            finally
            {
                SJNC.SyncOut.Release();
            }
        }
    }
}
