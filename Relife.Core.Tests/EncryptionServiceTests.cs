using Relife.Core.Services;

namespace Relife.Core.Tests;

/// <summary>
/// Tests for EncryptionService
/// </summary>
public class EncryptionServiceTests
{
    private readonly IEncryptionService _encryptionService;

    public EncryptionServiceTests()
    {
        _encryptionService = new EncryptionService();
    }

    [Fact]
    public void Encrypt_ValidData_ShouldProduceDifferentOutputEachTime()
    {
        // Arrange
        var data = System.Text.Encoding.UTF8.GetBytes("Test data for encryption");
        var key = "SecureKey123";

        // Act
        var encrypted1 = _encryptionService.Encrypt(data, key);
        var encrypted2 = _encryptionService.Encrypt(data, key);

        // Assert - Different IVs should produce different ciphertext
        Assert.NotEqual(encrypted1, encrypted2);
    }

    [Fact]
    public void Decrypt_EncryptedData_ShouldReturnOriginalData()
    {
        // Arrange
        var originalData = "This is sensitive data that needs protection!";
        var originalBytes = System.Text.Encoding.UTF8.GetBytes(originalData);
        var key = "SuperSecureKey456!@#";

        // Act
        var encrypted = _encryptionService.Encrypt(originalBytes, key);
        var decrypted = _encryptionService.Decrypt(encrypted, key);
        var decryptedText = System.Text.Encoding.UTF8.GetString(decrypted);

        // Assert
        Assert.Equal(originalData, decryptedText);
    }

    [Fact]
    public void Decrypt_WrongKey_ShouldThrowException()
    {
        // Arrange
        var data = System.Text.Encoding.UTF8.GetBytes("Secret data");
        var correctKey = "CorrectKey123";
        var wrongKey = "WrongKey456";

        var encrypted = _encryptionService.Encrypt(data, correctKey);

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => _encryptionService.Decrypt(encrypted, wrongKey));
    }

    [Fact]
    public void Decrypt_CorruptedData_ShouldThrowException()
    {
        // Arrange
        var data = System.Text.Encoding.UTF8.GetBytes("Test data");
        var key = "TestKey123";

        var encrypted = _encryptionService.Encrypt(data, key);

        // Corrupt the encrypted data
        encrypted[20] ^= 0xFF; // Flip bits

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => _encryptionService.Decrypt(encrypted, key));
    }

    [Fact]
    public void Decrypt_TooShortData_ShouldThrowCryptographicException()
    {
        // Arrange
        var shortData = new byte[10]; // Less than IV size (16 bytes)
        var key = "TestKey";

        // Act & Assert
        var exception = Assert.Throws<System.Security.Cryptography.CryptographicException>(
            () => _encryptionService.Decrypt(shortData, key));
        
        Assert.Contains("too short", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Encrypt_LargeData_ShouldWork()
    {
        // Arrange
        var largeData = new byte[1024 * 100]; // 100 KB
        new Random().NextBytes(largeData);
        var key = "LargeDataKey";

        // Act
        var encrypted = _encryptionService.Encrypt(largeData, key);
        var decrypted = _encryptionService.Decrypt(encrypted, key);

        // Assert
        Assert.Equal(largeData, decrypted);
    }

    [Fact]
    public void Encrypt_EmptyData_ShouldWork()
    {
        // Arrange
        var emptyData = Array.Empty<byte>();
        var key = "EmptyDataKey";

        // Act
        var encrypted = _encryptionService.Encrypt(emptyData, key);
        var decrypted = _encryptionService.Decrypt(encrypted, key);

        // Assert - Empty data should decrypt back to empty
        Assert.Empty(decrypted);
    }

    [Fact]
    public void Encrypt_SameKeyDifferentData_ShouldProduceDifferentCiphertext()
    {
        // Arrange
        var data1 = System.Text.Encoding.UTF8.GetBytes("Data 1");
        var data2 = System.Text.Encoding.UTF8.GetBytes("Data 2");
        var key = "SharedKey";

        // Act
        var encrypted1 = _encryptionService.Encrypt(data1, key);
        var encrypted2 = _encryptionService.Encrypt(data2, key);

        // Assert
        Assert.NotEqual(encrypted1, encrypted2);
    }
}
