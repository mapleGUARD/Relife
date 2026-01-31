using System.Security.Cryptography;
using System.Text;

namespace Relife.Core.Services;

/// <summary>
/// AES-256 encryption service for secure state persistence
/// </summary>
public class EncryptionService : IEncryptionService
{
    private const int KeySize = 256;
    private const int IvSize = 16; // 128 bits

    /// <summary>
    /// Encrypts data using AES-256 with a derived key
    /// </summary>
    public byte[] Encrypt(byte[] data, string key)
    {
        // Special case: empty data
        if (data.Length == 0)
        {
            // Return just an IV to indicate empty encrypted data
            var emptyIv = new byte[IvSize];
            RandomNumberGenerator.Fill(emptyIv);
            return emptyIv;
        }

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        // Derive key using SHA256
        using var sha = SHA256.Create();
        aes.Key = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
        
        // Generate IV and capture it before creating encryptor
        aes.GenerateIV();
        var iv = new byte[IvSize];
        Array.Copy(aes.IV, iv, IvSize);

        using var encryptor = aes.CreateEncryptor(aes.Key, iv);
        var encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);

        // Prepend IV to encrypted data
        var result = new byte[IvSize + encrypted.Length];
        Array.Copy(iv, 0, result, 0, IvSize);
        Array.Copy(encrypted, 0, result, IvSize, encrypted.Length);

        return result;
    }

    /// <summary>
    /// Decrypts data using AES-256 with a derived key
    /// </summary>
    public byte[] Decrypt(byte[] encryptedData, string key)
    {
        if (encryptedData.Length < IvSize)
        {
            throw new CryptographicException("Invalid encrypted data - too short");
        }

        // Special case: empty data (only IV, no cipher text)
        if (encryptedData.Length == IvSize)
        {
            return Array.Empty<byte>();
        }

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        // Derive key using SHA256
        using var sha = SHA256.Create();
        var derivedKey = sha.ComputeHash(Encoding.UTF8.GetBytes(key));

        // Extract IV from encrypted data
        var iv = new byte[IvSize];
        Array.Copy(encryptedData, 0, iv, 0, IvSize);

        // Extract ciphertext
        var cipherText = new byte[encryptedData.Length - IvSize];
        Array.Copy(encryptedData, IvSize, cipherText, 0, cipherText.Length);

        using var decryptor = aes.CreateDecryptor(derivedKey, iv);
        return decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
    }
}
