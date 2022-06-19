using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Dido.Utilities
{
    /// <summary>
    /// Provides methods for encrypting and decrypting data using AES ciphers.
    /// </summary>
    public static class AES
    {
        /// <summary>
        /// Encrypt the provided data using the provided key.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string Encrypt(string data, string key)
        {
            return Convert.ToBase64String(Encrypt(Encoding.UTF8.GetBytes(data), key));
        }

        /// <summary>
        /// Encrypt the provided data using the provided key.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static byte[] Encrypt(byte[] data, string key)
        {
            using (var input = new MemoryStream(data))
            using (var output = new MemoryStream())
            {
                Encrypt(input, output, Encoding.UTF8.GetBytes(key));
                return output.ToArray();
            }
        }

        /// <summary>
        /// Encrypt the provided input stream to the provided output stream using the provided key.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <param name="key"></param>
        public static void Encrypt(Stream input, Stream output, string key)
        {
            Encrypt(input, output, Encoding.UTF8.GetBytes(key));
        }

        /// <summary>
        /// Encrypt the provided input stream to the provided output stream using the provided key.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <param name="key"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void Encrypt(Stream input, Stream output, byte[] key)
        {
            if (key == null || key.Length <= 0)
            {
                throw new ArgumentNullException(nameof(key));
            }

            // compute the SHA256 hash of the key to use as the secret key since it is always 32 bytes
            byte[] keyhash;
            using (var sha = SHA256.Create())
            {
                keyhash = sha.ComputeHash(key);
            }

            using (var aes = Aes.Create())
            {
                aes.GenerateIV();

                aes.Key = keyhash;

                var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                // first output the initialization vector
                output.WriteByte((byte)aes.IV.Length);
                output.Write(aes.IV, 0, aes.IV.Length);

                // then output the encrypted data
                using (var csEncrypt = new CryptoStream(output, encryptor, CryptoStreamMode.Write))
                {
                    input.CopyTo(csEncrypt);
                }
            }
        }

        /// <summary>
        /// Decrypt the provided cipher data using the provided key.
        /// </summary>
        /// <param name="cipher"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string Decrypt(string cipher, string key)
        {
            return Encoding.UTF8.GetString(Decrypt(Convert.FromBase64String(cipher), key));
        }

        /// <summary>
        /// Decrypt the provided cipher data using the provided key.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static byte[] Decrypt(byte[] data, string key)
        {
            using (var input = new MemoryStream(data))
            using (var output = new MemoryStream())
            {
                Decrypt(input, output, Encoding.UTF8.GetBytes(key));
                return output.ToArray();
            }
        }

        /// <summary>
        /// Decrypt the provided input stream to the provided output stream using the provided key.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <param name="key"></param>
        public static void Decrypt(Stream input, Stream output, string key)
        {
            Decrypt(input, output, Encoding.UTF8.GetBytes(key));
        }

        /// <summary>
        /// Decrypt the provided input stream to the provided output stream using the provided key.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <param name="key"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void Decrypt(Stream input, Stream output, byte[] key)
        {
            if (key == null || key.Length <= 0)
            {
                throw new ArgumentNullException(nameof(key));
            }

            // compute the SHA256 hash of the key to use as the secret key since it is always 32 bytes
            byte[] keyhash;
            using (var sha = SHA256.Create())
            {
                keyhash = sha.ComputeHash(key);
            }

            using (var aes = Aes.Create())
            {
                // first extract the initialization vector
                var ivLength = input.ReadByte();
                var iv = new byte[ivLength];
                input.Read(iv, 0, ivLength);

                // then extract the decrypted data
                aes.Key = keyhash;
                aes.IV = iv;
                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using (var csDecrypt = new CryptoStream(input, decryptor, CryptoStreamMode.Read))
                {
                    csDecrypt.CopyTo(output);
                }
            }
        }
    }
}
