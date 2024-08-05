using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Me.Shishioko.SJNetChat
{
    public abstract class SJNCExtension
    {
        internal SJNC? SJNC = null;
        internal bool Unlocked = false;
        internal ushort Channel = 0;
        internal readonly SemaphoreSlim Sync = new(1, 1);
        public SJNCExtension()
        {

        }
        internal protected abstract string Identify();
        internal protected abstract Task<Stream> OnInitializeAsync(Stream stream, bool server);
        internal protected abstract Task OnReceiveAsync(byte[] data);
        protected Task SendAsync(byte[] data)
        {
            Contract.Assert(Unlocked);
            return SJNC!.SendAsync(Channel, data);
        }
    }
}
