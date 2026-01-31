namespace Relife.Core.Services;

/// <summary>
/// Encryption/Decryption service interface
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts data using AES-256
    /// </summary>
    byte[] Encrypt(byte[] data, string key);

    /// <summary>
    /// Decrypts data using AES-256
    /// </summary>
    byte[] Decrypt(byte[] encryptedData, string key);
}
