// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System;
using System.Buffers.Binary;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MJCZone.DapperMatic.AspNetCore.Security;

/// <summary>
/// Utility methods for encryption and decryption using AES-GCM.
/// Supports both raw 256-bit keys (Base64) and passphrase-derived keys (PBKDF2).
/// Encrypted payloads include necessary metadata (mode, salt, nonce, tag).
/// </summary>
public static class CryptoUtils
{
    private const int KeySize = 32; // 256-bit
    private const int NonceSize = 12; // GCM standard
    private const int TagSize = 16; // 128-bit tag (recommended)
    private const int Pbkdf2Iterations = 100_000;

    /// <summary>
    /// Generates a random 256-bit key, returned as Base64.
    /// Store this securely (e.g., in a secrets manager or KMS).
    /// </summary>
    /// <returns>A Base64-encoded 256-bit encryption key.</returns>
    public static string GenerateEncryptionKey()
    {
        var key = new byte[KeySize];
        RandomNumberGenerator.Fill(key);
        return Convert.ToBase64String(key);
    }

    /// <summary>
    /// Encrypts UTF-8 text and returns ciphertext as Base64.
    /// </summary>
    /// <param name="plaintext">The plain text to encrypt.</param>
    /// <param name="encryptionKey">The encryption key (Base64 32-byte or passphrase).</param>
    /// <returns>The encrypted text as Base64.</returns>
    public static string EncryptToBase64(string plaintext, string encryptionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        if (string.IsNullOrWhiteSpace(encryptionKey))
        {
            throw new ArgumentException("Required", nameof(encryptionKey));
        }
        return Convert.ToBase64String(Encrypt(Encoding.UTF8.GetBytes(plaintext), encryptionKey));
    }

    /// <summary>
    /// Decrypts Base64 ciphertext into UTF-8 text.
    /// </summary>
    /// <param name="ciphertextBase64">The encrypted text as Base64.</param>
    /// <param name="encryptionKey">The encryption key (Base64 32-byte or passphrase).</param>
    /// <returns>The decrypted plain text.</returns>
    public static string DecryptFromBase64(string ciphertextBase64, string encryptionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ciphertextBase64);
        if (string.IsNullOrWhiteSpace(encryptionKey))
        {
            throw new ArgumentException("Required", nameof(encryptionKey));
        }
        return Encoding.UTF8.GetString(Decrypt(Convert.FromBase64String(ciphertextBase64), encryptionKey));
    }

    /// <summary>
    /// Encrypts raw bytes and returns ciphertext bytes.
    /// </summary>
    /// <param name="plaintext">The plain text bytes to encrypt.</param>
    /// <param name="encryptionKey">The encryption key (Base64 32-byte or passphrase).</param>
    /// <returns>The encrypted bytes.</returns>
    public static byte[] Encrypt(byte[] plaintext, string encryptionKey)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        if (string.IsNullOrWhiteSpace(encryptionKey))
        {
            throw new ArgumentException("Required", nameof(encryptionKey));
        }

        var usingRawKey = TryDecodeBase64Key(encryptionKey, out var rawKey) && rawKey.Length == KeySize;
        byte[] key,
            salt = [];

        if (usingRawKey)
        {
            key = rawKey;
        }
        else
        {
            salt = new byte[16];
            RandomNumberGenerator.Fill(salt);
            key = DeriveKeyFromPassphrase(encryptionKey, salt);
        }

        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var tag = new byte[TagSize];
        var ciphertext = new byte[plaintext.Length];
        var gcm = new AesGcm(key, TagSize);
        try
        {
            gcm.Encrypt(nonce, plaintext, ciphertext, tag, associatedData: null);
        }
        finally
        {
            gcm.Dispose();
            CryptographicOperations.ZeroMemory(key);
        }

        // payload: [mode(1)][saltLen(1)][salt?][nonce(12)][tag(16)][ciphertext]
        var mode = usingRawKey ? (byte)0 : (byte)1;
        var saltLen = (byte)(usingRawKey ? 0 : salt.Length);
        var result = new byte[2 + saltLen + NonceSize + TagSize + ciphertext.Length];

        var o = 0;
        result[o++] = mode;
        result[o++] = saltLen;
        if (saltLen > 0)
        {
            Buffer.BlockCopy(salt, 0, result, o, saltLen);
            o += saltLen;
        }
        Buffer.BlockCopy(nonce, 0, result, o, NonceSize);
        o += NonceSize;
        Buffer.BlockCopy(tag, 0, result, o, TagSize);
        o += TagSize;
        Buffer.BlockCopy(ciphertext, 0, result, o, ciphertext.Length);

        return result;
    }

    /// <summary>
    /// Decrypts raw ciphertext bytes produced by <see cref="Encrypt"/>.
    /// </summary>
    /// <param name="payload">The encrypted bytes.</param>
    /// <param name="encryptionKey">The encryption key (Base64 32-byte or passphrase).</param>
    /// <returns>The decrypted plain text bytes.</returns>
    public static byte[] Decrypt(byte[] payload, string encryptionKey)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (string.IsNullOrWhiteSpace(encryptionKey))
        {
            throw new ArgumentException("Required", nameof(encryptionKey));
        }

        if (payload.Length < 2 + NonceSize + TagSize)
        {
            throw new ArgumentException("Ciphertext too short.", nameof(payload));
        }

        var o = 0;
        var mode = payload[o++]; // 0 = raw key, 1 = passphrase
        var saltLen = payload[o++];
        if (payload.Length < 2 + saltLen + NonceSize + TagSize)
        {
            throw new ArgumentException("Header invalid.", nameof(payload));
        }

        byte[] salt = Array.Empty<byte>();
        if (saltLen > 0)
        {
            salt = new byte[saltLen];
            Buffer.BlockCopy(payload, o, salt, 0, saltLen);
            o += saltLen;
        }

        var nonce = new byte[NonceSize];
        Buffer.BlockCopy(payload, o, nonce, 0, NonceSize);
        o += NonceSize;

        var tag = new byte[TagSize];
        Buffer.BlockCopy(payload, o, tag, 0, TagSize);
        o += TagSize;

        var ciphertext = new byte[payload.Length - o];
        Buffer.BlockCopy(payload, o, ciphertext, 0, ciphertext.Length);

        byte[] key;
        if (mode == 0)
        {
            if (!TryDecodeBase64Key(encryptionKey, out var rawKey) || rawKey.Length != KeySize)
            {
                throw new CryptographicException("Invalid raw key (expect Base64-encoded 32 bytes).");
            }

            key = rawKey;
        }
        else if (mode == 1)
        {
            if (salt.Length == 0)
            {
                throw new CryptographicException("Missing salt.");
            }

            key = DeriveKeyFromPassphrase(encryptionKey, salt);
        }
        else
        {
            throw new CryptographicException("Unknown key mode.");
        }

        var plaintext = new byte[ciphertext.Length];
        var gcm2 = new AesGcm(key, TagSize);
        try
        {
            gcm2.Decrypt(nonce, ciphertext, tag, plaintext, associatedData: null);
            return plaintext;
        }
        finally
        {
            gcm2.Dispose();
            CryptographicOperations.ZeroMemory(key);
        }
    }

    // ---- helpers ----

    private static bool TryDecodeBase64Key(string s, out byte[] key)
    {
        try
        {
            key = Convert.FromBase64String(s);
            return true;
        }
        catch
        {
            key = Array.Empty<byte>();
            return false;
        }
    }

    private static byte[] DeriveKeyFromPassphrase(string passphrase, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(passphrase, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySize);
    }
}
