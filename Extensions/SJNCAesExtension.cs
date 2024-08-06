using Net.Myzuc.ShioLib;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Me.Shishioko.SJNetChat.Extensions
{
    public sealed class SJNCAesExtension : SJNCExtension
    {
        public SJNCAesExtension() : base("SJNCAes")
        {

        }
        protected internal override async Task<Stream> OnInitializeAsync(Stream stream, bool server)
        {
            using RSA rsa = RSA.Create();
            rsa.KeySize = 1024;
            byte[] secret;
            if (server)
            {
                rsa.ImportRSAPublicKey(await stream.ReadU8AAsync(SizePrefix.U16, ushort.MaxValue), out _);
                secret = RandomNumberGenerator.GetBytes(128 >> 3);
                await stream.WriteU8AAsync(rsa.Encrypt(secret, RSAEncryptionPadding.Pkcs1), SizePrefix.U16, ushort.MaxValue);
            }
            else
            {
                await stream.WriteU8AAsync(rsa.ExportRSAPublicKey(), SizePrefix.U16, ushort.MaxValue);
                secret = rsa.Decrypt(await stream.ReadU8AAsync(SizePrefix.U16, ushort.MaxValue), RSAEncryptionPadding.Pkcs1);
            }
            return new ShioAesCfbStream(stream, secret, secret, false);
        }
    }
}
