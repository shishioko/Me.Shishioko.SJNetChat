using Net.Myzuc.ShioLib;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Net;

namespace Me.Shishioko.SJNetChat
{
    /// <summary>
    /// A class representing an <see cref="SJNCExtension"/> to the <see cref="SJNetChat.SJNC"/> protocol.
    /// </summary>
    public abstract class SJNCExtension
    {
        /// <summary>
        /// <see langword="true"/> if the extension is mutually available, <see langword="false"/> otherwise.
        /// </summary>
        public bool Common { get; internal set; } = false;
        internal readonly string Name;
        internal SJNC? SJNC = null;
        internal bool Unlocked = false;
        internal ushort Channel = 0;
        internal readonly SemaphoreSlim Sync = new(1, 1);
        /// <summary>
        /// Creates a new <see cref="SJNCExtension"/> instance.
        /// </summary>
        /// <param name="name">The identifying name of the extension</param>
        public SJNCExtension(string name)
        {
            Name = name;
        }
        /// <summary>
        /// This method is called upon initialization of the mutual extension.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to write data to</param>
        /// <param name="server">Whether this <see cref="SJNetChat.SJNC"/> instance represents a server or client</param>
        /// <returns>A <see cref="Task"/> awaiting the execution of this method</returns>
        internal protected virtual Task<Stream> OnInitializeAsync(Stream stream, bool server)
        {
            return Task.FromResult(stream);
        }
        /// <summary>
        /// This method is called upon transmission of messages to attach additional data for the connected peer of this extension.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to write data to</param>
        /// <param name="message">The sent message</param>
        /// <returns>A <see cref="Task"/> awaiting the execution of this method</returns>
        internal protected virtual Task OnTextSendAsync(Stream stream, string message)
        {
            return Task.CompletedTask;
        }
        /// <summary>
        /// This method is called upon receival of messages to attach additional data for the connected peer of this extension.
        /// </summary>
        /// <param name="stream">The stream to write data to</param>
        /// <param name="message">The received message</param>
        /// <returns>A <see cref="Task"/> awaiting the execution of this method</returns>
        internal protected virtual Task OnTextReceiveAsync(Stream stream, string message)
        {
            return Task.CompletedTask;
        }
        /// <summary>
        /// This method is called upon receival of data from the connected peer of this extension.
        /// </summary>
        /// <param name="data">The received data</param>
        /// <returns>A <see cref="Task"/> awaiting the execution of this method</returns>
        internal protected virtual Task OnReceiveAsync(byte[] data)
        {
            return Task.CompletedTask;
        }
        /// <summary>
        /// Sends raw data to the connected peer of this extension asynchronously.
        /// </summary>
        /// <param name="data">The data to send.</param>
        /// <returns>A <see cref="Task"/> awaiting the execution of this method</returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ProtocolViolationException"></exception>
        /// <exception cref="AggregateException"></exception>
        protected async Task SendAsync(byte[] data)
        {
            if (SJNC.Channels is null) throw new InvalidOperationException("SJNC is not connected!");
            if (!Unlocked) throw new InvalidOperationException("Extension is not mutual!");
            if (data.Length > ushort.MaxValue) throw new ProtocolViolationException("Data exceeds maximum support size!");
            await SJNC.SyncOut.WaitAsync();
            try
            {
                await SJNC.Stream.WriteU16Async(Channel);
                await SJNC.Stream.WriteU8AAsync(data, SizePrefix.U16, ushort.MaxValue);
            }
            catch(Exception ex)
            {
                throw new AggregateException(ex);
            }
            finally
            {
                SJNC.SyncOut.Release();
            }
        }
    }
}
