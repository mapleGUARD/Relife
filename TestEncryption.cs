using System;
using System.Security.Cryptography;
using System.Text;

// Standalone test to verify encryption logic
var data = Encoding.UTF8.GetBytes("This is sensitive data that needs protection!");
var key = "SuperSecureKey456!@#";

Console.WriteLine($"Original: {Encoding.UTF8.GetString(data)}");
Console.WriteLine($"Original bytes: {BitConverter.ToString(data)}");

// Encrypt
using var aesEnc = Aes.Create();
aesEnc.KeySize = 256;
aesEnc.Mode = CipherMode.CBC;
aesEnc.Padding = PaddingMode.PKCS7;

using var sha1 = SHA256.Create();
aesEnc.Key = sha1.ComputeHash(Encoding.UTF8.GetBytes(key));
aesEnc.GenerateIV();

Console.WriteLine($"IV: {BitConverter.ToString(aesEnc.IV)}");

using var encryptor = aesEnc.CreateEncryptor();
var encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);

// Prepend IV
var result = new byte[16 + encrypted.Length];
Buffer.BlockCopy(aesEnc.IV, 0, result, 0, 16);
Buffer.BlockCopy(encrypted, 0, result, 16, encrypted.Length);

Console.WriteLine($"Encrypted (with IV): {BitConverter.ToString(result)}");

// Decrypt
using var aesDec = Aes.Create();
aesDec.KeySize = 256;
aesDec.Mode = CipherMode.CBC;
aesDec.Padding = PaddingMode.PKCS7;

using var sha2 = SHA256.Create();
aesDec.Key = sha2.ComputeHash(Encoding.UTF8.GetBytes(key));

// Extract IV
aesDec.IV = new byte[16];
Buffer.BlockCopy(result, 0, aesDec.IV, 0, 16);

Console.WriteLine($"Extracted IV: {BitConverter.ToString(aesDec.IV)}");

using var decryptor = aesDec.CreateDecryptor();
var cipherText = new byte[result.Length - 16];
Buffer.BlockCopy(result, 16, cipherText, 0, cipherText.Length);

var decrypted = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);

Console.WriteLine($"Decrypted: {Encoding.UTF8.GetString(decrypted)}");
Console.WriteLine($"Decrypted bytes: {BitConverter.ToString(decrypted)}");
Console.WriteLine($"Match: {Encoding.UTF8.GetString(data) == Encoding.UTF8.GetString(decrypted)}");
