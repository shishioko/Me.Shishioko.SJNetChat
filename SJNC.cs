using Net.Myzuc.ShioLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Me.Shishioko.SJNetChat
{
    /// <summary>
    /// A class representing a SJNC connection object.
    /// </summary>
    public sealed class SJNC : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Establishes a connection to an <see cref="EndPoint"/> asynchronously and creates a <see cref="SJNC"/> object.
        /// </summary>
        /// <param name="endpoint">The <see cref="EndPoint"/> to connect to</param>
        /// <returns>A <see cref="Task"/> awaiting the created <see cref="SJNC"/> object</returns>
        /// <exception cref="AggregateException"></exception>
        public static async Task<SJNC> ConnectAsync(EndPoint endpoint)
        {
            try
            {
                Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(endpoint);
                return new(new NetworkStream(socket, true), false, false);
            }
            catch(Exception ex)
            {
                throw new AggregateException(ex);
            }
        }
        /// <summary>
        /// Establishes a connection to an <see cref="EndPoint"/> synchronously and creates a <see cref="SJNC"/> object.
        /// Note that this method internally calls <see cref="SJNC.ConnectAsync(EndPoint)"/>
        /// </summary>
        /// <param name="endpoint">The <see cref="EndPoint"/> to connect to</param>
        /// <returns>The created <see cref="SJNC"/> object</returns>
        /// <exception cref="AggregateException"></exception>
        public static SJNC Connect(EndPoint endpoint)
        {
            return ConnectAsync(endpoint).Result;
        }
        internal Stream Stream;
        internal ushort Version = 0;
        internal SJNCExtension[]? Channels = null;
        internal readonly SemaphoreSlim SyncOut = new(1, 1);
        private readonly SemaphoreSlim SyncIn = new(1, 1);
        private readonly bool Server;
        private readonly bool KeepOpen;
        /// <summary>
        /// Creates a new <see cref="SJNC"/> object from an arbitrary underlying <see cref="System.IO.Stream"/>.
        /// </summary>
        /// <param name="stream">The underlying stream to use</param>
        /// <param name="server">Whether this instance represents the server or client</param>
        /// <param name="keepOpen">Whether to keep the stream open after disposal of the <see cref="SJNC"/> object</param>
        /// <returns>The created <see cref="SJNC"/> object</returns>
        public SJNC(Stream stream, bool server = false, bool keepOpen = true)
        {
            Stream = stream;
            Server = server;
            KeepOpen = keepOpen;
        }
        /// <summary>
        /// Initializes the SJNC protocol on the underlying connection asynchronously.
        /// </summary>
        /// <param name="extensions">An optional list of <see cref="SJNCExtension"/>s to attempt to load</param>
        /// <returns>A <see cref="Task"/> awaiting this method</returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="AggregateException"></exception>
        public async Task InitializeAsync(IEnumerable<SJNCExtension>? extensions = null)
        {
            if (Channels is not null) throw new InvalidOperationException("SJNC is already connected!");
            bool ownError = false;
            await SyncOut.WaitAsync();
            try
            {
                SJNCExtension[] extensionList = (extensions ?? []).ToArray();
                foreach (SJNCExtension extension in extensionList)
                {
                    await extension.Sync.WaitAsync();
                    if (extension.SJNC is not null)
                    {
                        ownError = true;
                        throw new InvalidOperationException("The extension is already initialized!");
                    }
                    extension.SJNC = this;
                    extension.Sync.Release();
                }
                Dictionary<string, SJNCExtension> extensionMap = [];
                await Stream.WriteU16Async(1);
                Version = await Stream.ReadU16Async();
                if (extensionList.Length >= ushort.MaxValue - 1)
                {
                    ownError = true;
                    throw new ProtocolViolationException("More than supported extensions!");
                }
                await Stream.WriteU16Async((ushort)extensionList.Length);
                for (int i = 0; i < extensionList.Length; i++)
                {
                    SJNCExtension extension = extensionList[i];
                    string extensionName = extension.Name;
                    extensionMap.Add(extensionName, extension);
                    await Stream.WriteStringAsync(extensionName, SizePrefix.U8, byte.MaxValue, Encoding.UTF8);
                }
                string[] remoteExtensions = new string[await Stream.ReadU16Async()];
                List<SJNCExtension> commonExtensions = [];
                List<SJNCExtension> initializedExtensions = [];
                for (int i = 0; i < remoteExtensions.Length; i++)
                {
                    string extensionName = await Stream.ReadStringAsync(SizePrefix.U8, byte.MaxValue, Encoding.UTF8);
                    if (!extensionMap.TryGetValue(extensionName, out SJNCExtension extension)) continue;
                    commonExtensions.Add(extension);
                }
                extensionList = [..commonExtensions];
                for (int i = 0; i < extensionList.Length; i++)
                {
                    SJNCExtension extension = extensionList[i];
                    if (!commonExtensions.Contains(extension)) continue;
                    extension.Unlocked = true;
                    Stream = await extension.OnInitializeAsync(Stream, Server);
                    extension.Common = true;
                    extension.Unlocked = false;
                    initializedExtensions.Add(extension);
                }
                Channels = [..initializedExtensions];
                for (int i = 0; i < Channels.Length; i++)
                {
                    Channels[i].Channel = (ushort)(i + 1);
                }
                for (int i = 0; i < Channels.Length; i++)
                {
                    Channels[i].Unlocked = true;
                }
            }
            catch(Exception ex)
            {
                if (ownError) throw;
                throw new AggregateException(ex);
            }
            finally
            {
                SyncOut.Release();
            }
        }
        /// <summary>
        /// Initializes the SJNC protocol on the underlying connection synchronously.
        /// Note that this method internally calls <see cref="SJNC.InitializeAsync(IEnumerable{SJNCExtension}?)"/>.
        /// </summary>
        /// <param name="extensions">An optional list of <see cref="SJNCExtension"/>s to attempt to load</param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="AggregateException"></exception>
        public void Initialize(IEnumerable<SJNCExtension>? extensions = null)
        {
            InitializeAsync(extensions).Wait();
        }
        /// <summary>
        /// Closes the SJNC connection asynchronously and releases all underlying objects.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (!KeepOpen) await Stream.DisposeAsync();
            SyncIn.Dispose();
            SyncOut.Dispose();
        }
        /// <summary>
        /// Closes the SJNC connection synchronously and releases all underlying objects.
        /// </summary>
        public void Dispose()
        {
            if (!KeepOpen) Stream.Dispose();
            SyncIn.Dispose();
            SyncOut.Dispose();
        }
        /// <summary>
        /// Writes a text message to the established SJNC connection asynchronously.
        /// </summary>
        /// <param name="message">The text message to send</param>
        /// <returns>A <see cref="Task"/> awaiting the execution of this method</returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="AggregateException"></exception>
        public async Task SendAsync(string message)
        {
            if (Channels is null) throw new InvalidOperationException("SJNC is not connected!");
            byte[] data = Encoding.UTF8.GetBytes(message); //TODO: fix so its chars and not bytes
            if (message.Length > byte.MaxValue) throw new ProtocolViolationException("Message exceeds 255 character limit!");
            await SyncOut.WaitAsync();
            try
            {
                await Stream.WriteU16Async(0);
                await Stream.WriteU8AAsync(data, SizePrefix.U16, ushort.MaxValue);
                if (Version >= 1)
                {
                    for (int i = 0; i < Channels.Length; i++)
                    {
                        await Channels[i].OnTextSendAsync(Stream, message);
                    }
                }
            }
            catch(Exception ex)
            {
                throw new AggregateException(ex);
            }
            finally
            {
                SyncOut.Release();
            }
        }
        /// <summary>
        /// Writes a text message to the established SJNC connection synchronously.
        /// Note that this method internally calls <see cref="SJNC.SendAsync(string)"/>.
        /// </summary>
        /// <param name="message">The text message to send</param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="AggregateException"></exception>
        public void Send(string message)
        {
            SendAsync(message).Wait();
        }
        /// <summary>
        /// Receives a text message from the established SJNC connection asynchronously.
        /// </summary>
        /// <returns>A <see cref="Task"/> awaiting the received text message</returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ProtocolViolationException"></exception>
        /// <exception cref="AggregateException"></exception>
        public async Task<string> ReceiveAsync()
        {
            if (Channels is null) throw new InvalidOperationException("SJNC is not connected!");
            bool ownError = false;
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
                        if (message.Length > byte.MaxValue)
                        {
                            ownError = true;
                            throw new ProtocolViolationException("Message exceeds maximum allow length");
                        }
                        if (Version >= 1)
                        {
                            for (int i = 0; i < Channels.Length; i++)
                            {
                                await Channels[i].OnTextReceiveAsync(Stream, message);
                            }
                        }
                        return message;
                    }
                    if (channel - 1 >= Channels.Length)
                    {
                        ownError = true;
                        throw new ProtocolViolationException("An unknown channel was addressed!");
                    }
                    await Channels[channel - 1].OnReceiveAsync(data);
                }
            }
            catch(Exception ex)
            {
                if (ownError) throw;
                throw new AggregateException(ex);
            }
            finally
            {
                SyncIn.Release();
            }
        }
        /// <summary>
        /// Receives a text message from the established SJNC connection synchronously.
        /// Note that this method internally calls <see cref="SJNC.ReceiveAsync()"/>.
        /// </summary>
        /// <returns>The received text message</returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ProtocolViolationException"></exception>
        /// <exception cref="AggregateException"></exception>
        public string Receive()
        {
            return ReceiveAsync().Result;
        }
    }
}
