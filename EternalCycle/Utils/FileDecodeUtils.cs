using System.Security.Cryptography;
using System.Text;

namespace EternalCycleServer
{
    public static class FileDecodeUtils
    {
        public static string DecodeToRawJson(string modpath, string pakPath, string fileSigPath, string masterSigPath, string publicKeyPem, string aesKeyHex, string ivHex)
        {
            var currectPak = Path.Combine(modpath, pakPath);
            var currectFileSig = Path.Combine(modpath, fileSigPath);
            var currectMasterSig = Path.Combine(modpath, masterSigPath);

            if (!File.Exists(currectPak))
                throw new FileNotFoundException($"找不到数据文件: {currectPak}");
            if (!File.Exists(currectFileSig))
                throw new FileNotFoundException($"找不到文件签名: {currectFileSig}");
            if (!File.Exists(currectMasterSig))
                throw new FileNotFoundException($"找不到密钥签名: {currectMasterSig}");

            string encryptedHex = File.ReadAllText(currectPak).Trim();

            string masterSigBase64 = File.ReadAllText(currectMasterSig).Trim();
            byte[] masterSigBytes = Convert.FromBase64String(masterSigBase64);
            byte[] masterMessageBytes = Encoding.UTF8.GetBytes(aesKeyHex + ivHex);

            string fileSigBase64 = File.ReadAllText(currectFileSig).Trim();
            byte[] fileSigBytes = Convert.FromBase64String(fileSigBase64);
            byte[] fileMessageBytes = Encoding.UTF8.GetBytes(encryptedHex);

            using (RSA rsa = RSA.Create())
            {
                rsa.ImportFromPem(publicKeyPem.ToCharArray());

                bool isMasterValid = rsa.VerifyData(masterMessageBytes, masterSigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                if (!isMasterValid)
                {
                    throw new UnauthorizedAccessException($"警告！密钥签名校验失败！");
                }

                bool isFileValid = rsa.VerifyData(fileMessageBytes, fileSigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                if (!isFileValid)
                {
                    throw new UnauthorizedAccessException($"警告！文件内容签名校验失败！");
                }
            }

            byte[] encryptedBytes = HexToBytes(encryptedHex);

            byte[] aesKey = HexToBytes(aesKeyHex);
            byte[] iv = HexToBytes(ivHex);
            byte[] decryptedBase64Bytes;

            using (Aes aes = Aes.Create())
            {
                aes.Key = aesKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                using (MemoryStream ms = new MemoryStream(encryptedBytes))
                using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (MemoryStream output = new MemoryStream())
                {
                    cs.CopyTo(output);
                    decryptedBase64Bytes = output.ToArray();
                }
            }

            string base64Str = Encoding.UTF8.GetString(decryptedBase64Bytes);

            byte[] hexStrBytes = Convert.FromBase64String(base64Str);
            string hexStr = Encoding.UTF8.GetString(hexStrBytes);

            byte[] rawJsonBytes = HexToBytes(hexStr);

            string rawJson = Encoding.UTF8.GetString(rawJsonBytes);

            Array.Clear(rawJsonBytes, 0, rawJsonBytes.Length);
            Array.Clear(aesKey, 0, aesKey.Length);

            return rawJson;
        }

        private static byte[] HexToBytes(string hex)
        {
            hex = hex.Replace(" ", "").Replace("\r", "").Replace("\n", "");

            if (hex.Length % 2 != 0) throw new ArgumentException("损坏的Hex。");

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }
    }
}