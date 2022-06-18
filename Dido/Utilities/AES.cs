using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Dido.Utilities
{
    public static class AES
    {
        public static string Encrypt(string data, string key)
        {
            return Convert.ToBase64String(Encrypt(Encoding.UTF8.GetBytes(data), key));
        }

        public static string Decrypt(string cipher, string key)
        {
            return Encoding.UTF8.GetString(Decrypt(Convert.FromBase64String(cipher), key));
        }

        public static byte[] Encrypt(byte[] data, string key)
        {
            using (var input = new MemoryStream(data))
            using (var output = new MemoryStream())
            {
                Encrypt(input, output, Encoding.UTF8.GetBytes(key));
                return output.ToArray();
            }
        }

        public static void Encrypt(Stream input, Stream output, string key)
        {
            Encrypt(input, output, Encoding.UTF8.GetBytes(key));
        }

        public static void Encrypt(Stream input, Stream output, byte[] key)
        {
            if (key == null || key.Length <= 0)
            {
                throw new ArgumentNullException(nameof(key));
            }

            // use the sha256 hash of the key as the actual AES key so it is always 32 bytes
            // (this matches the expected key size and allows for a key of any length from the caller)
            byte[] keyhash;
            using (var sha = SHA256.Create())
            {
                keyhash = sha.ComputeHash(key);
            }

            //byte[] encrypted;

            //using (var aes = new AesCryptoServiceProvider())
            using (var aes = Aes.Create())
            {
                aes.GenerateIV();

                aes.Key = keyhash;

                var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                //using (var msEncrypt = new MemoryStream())
                {
                    // prepend the encrypted output with the initialization vector
                    output.WriteByte((byte)aes.IV.Length);
                    output.Write(aes.IV, 0, aes.IV.Length);
                    using (var csEncrypt = new CryptoStream(output, encryptor, CryptoStreamMode.Write))
                    {
                        input.CopyTo(csEncrypt);
                        //using (var swEncrypt = new BinaryWriter(csEncrypt))
                        //{
                        //    swEncrypt.Write(data);
                        //}
                    }
                    //encrypted = msEncrypt.ToArray();
                }
            }

            //return encrypted;
        }

        public static byte[] Decrypt(byte[] data, string key)
        {
            using (var input = new MemoryStream(data))
            using (var output = new MemoryStream())
            {
                Decrypt(input, output, Encoding.UTF8.GetBytes(key));
                return output.ToArray();
            }
        }

        public static void Decrypt(Stream input, Stream output, string key)
        {
            Decrypt(input, output, Encoding.UTF8.GetBytes(key));
        }

        public static void Decrypt(Stream input, Stream output, byte[] key)
        {
            if (key == null || key.Length <= 0)
            {
                throw new ArgumentNullException(nameof(key));
            }

            // use the sha256 hash of the key which is always 32 bytes (matching the expected key size)
            byte[] keyhash;
            using (var sha = SHA256.Create())
            {
                keyhash = sha.ComputeHash(key);
            }

            //byte[] value = null;

            //using (var aes = new AesCryptoServiceProvider())
            using (var aes = Aes.Create())
            {
                //using (var msDecrypt = new MemoryStream(cipher))
                {
                    // extract the prepended initialization vector
                    var ivLength = input.ReadByte();
                    var iv = new byte[ivLength];
                    input.Read(iv, 0, ivLength);

                    aes.Key = keyhash;
                    aes.IV = iv;

                    var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                    using (var csDecrypt = new CryptoStream(output, decryptor, CryptoStreamMode.Read))
                    //using (var srDecrypt = new BinaryReader(csDecrypt))
                    {
                        input.CopyTo(csDecrypt);
                        //value = srDecrypt.ReadBytes(cipher.Length - ivLength);
                    }
                }
            }

            //return value;
        }
    }

}
