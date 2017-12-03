namespace FixApCore
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Text;

    public class RsaEncryptor
    {
        private List<byte> publicKey;

        private List<byte> exponent;

        public RsaEncryptor(string publicKey, string exponent)
        {
            this.publicKey = GetBytesFromHex(publicKey);
            this.exponent = GetBytesFromHex(exponent);
        }

        public string EncryptData(string data)
        {
            try
            {
                var rsa = new RSACryptoServiceProvider();
                var rsaKeyInfo = new RSAParameters
                {
                    Modulus = publicKey.ToArray(),
                    Exponent = exponent.ToArray()
                };

                rsa.ImportParameters(rsaKeyInfo);

                var dataBytes = ASCIIEncoding.ASCII.GetBytes(data);
                var encryptedBytes = rsa.Encrypt(dataBytes, false);
                var encryptedValue = BitConverter.ToString(encryptedBytes).Replace("-", "").ToLower();
                return encryptedValue;
            }
            catch (CryptographicException e)
            {
                Console.WriteLine(e.Message);
            }

            return null;
        }

        private static List<byte> GetBytesFromHex(string input)
        {
            var result = new List<byte>();
            for (int i = 0; i < input.Length; i += 2)
            {
                var pair = input.Substring(i, 2);
                result.Add(Convert.ToByte(pair, 16));
            }

            return result;
        }
    }
}
