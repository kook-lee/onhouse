using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

public class IssacWebCrypto
{
    public static byte[] Mgf1Sha1(byte[] seed, int length)
    {
        byte[] mask = new byte[length];
        var sha1 = new Sha1Digest();
        byte[] counter = new byte[4];
        byte[] hash = new byte[sha1.GetDigestSize()];

        int outPos = 0;
        uint count = 0;

        while (outPos < length)
        {
            counter[0] = (byte)(count >> 24);
            counter[1] = (byte)(count >> 16);
            counter[2] = (byte)(count >> 8);
            counter[3] = (byte)count;

            sha1.Reset();
            sha1.BlockUpdate(seed, 0, seed.Length);
            sha1.BlockUpdate(counter, 0, 4);
            sha1.DoFinal(hash, 0);

            int toCopy = Math.Min(hash.Length, length - outPos);
            Array.Copy(hash, 0, mask, outPos, toCopy);
            outPos += toCopy;
            count++;
        }

        return mask;
    }

    public static byte[] RsaOaepEncryptFixedSeed(byte[] message, RsaKeyParameters pubKey, byte[] seed)
    {
        int k = (pubKey.Modulus.BitLength + 7) / 8;
        int hLen = 20; // SHA-1

        var sha1 = new Sha1Digest();
        byte[] lHash = new byte[hLen];
        sha1.DoFinal(lHash, 0); // SHA-1 of empty string

        int dbLen = k - hLen - 1;
        byte[] db = new byte[dbLen];
        Array.Copy(lHash, 0, db, 0, hLen);

        int psLen = dbLen - hLen - 1 - message.Length;
        // db[hLen .. hLen + psLen - 1] is 0x00
        db[hLen + psLen] = 0x01;
        Array.Copy(message, 0, db, hLen + psLen + 1, message.Length);

        byte[] dbMask = Mgf1Sha1(seed, dbLen);
        byte[] maskedDB = new byte[dbLen];
        for (int i = 0; i < dbLen; i++) maskedDB[i] = (byte)(db[i] ^ dbMask[i]);

        byte[] seedMask = Mgf1Sha1(maskedDB, hLen);
        byte[] maskedSeed = new byte[hLen];
        for (int i = 0; i < hLen; i++) maskedSeed[i] = (byte)(seed[i] ^ seedMask[i]);

        byte[] em = new byte[k];
        em[0] = 0x00;
        Array.Copy(maskedSeed, 0, em, 1, hLen);
        Array.Copy(maskedDB, 0, em, 1 + hLen, dbLen);

        var mInt = new BigInteger(1, em);
        var cInt = mInt.ModPow(pubKey.Exponent, pubKey.Modulus);

        byte[] cBytes = cInt.ToByteArrayUnsigned();
        if (cBytes.Length < k)
        {
            byte[] padded = new byte[k];
            Array.Copy(cBytes, 0, padded, k - cBytes.Length, cBytes.Length);
            return padded;
        }
        return cBytes;
    }

    public static byte[] SeedCbcEncrypt(byte[] data, byte[] key, byte[] iv)
    {
        var engine = new SeedEngine();
        var cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(engine), new Pkcs7Padding());
        cipher.Init(true, new ParametersWithIV(new KeyParameter(key), iv));
        byte[] output = new byte[cipher.GetOutputSize(data.Length)];
        int len = cipher.ProcessBytes(data, 0, data.Length, output, 0);
        len += cipher.DoFinal(output, len);
        if (len < output.Length)
        {
            Array.Resize(ref output, len);
        }
        return output;
    }

    public static string HybridEncrypt(string message, string publicKeyB64)
    {
        byte[] keyBytes = Convert.FromBase64String(publicKeyB64);
        var asn1Obj = Asn1Object.FromByteArray(keyBytes);
        RsaKeyParameters pubKey;
        if (asn1Obj is Asn1Sequence kSeq)
        {
            if (kSeq.Count == 2 && kSeq[0] is DerInteger m && kSeq[1] is DerInteger e)
            {
                pubKey = new RsaKeyParameters(false, m.Value, e.Value);
            }
            else
            {
                pubKey = (RsaKeyParameters)PublicKeyFactory.CreateKey(keyBytes);
            }
        }
        else
        {
            pubKey = (RsaKeyParameters)PublicKeyFactory.CreateKey(keyBytes);
        }

        // 16 bytes random symmetric key
        byte[] symKey = new byte[16];
        RandomNumberGenerator.Fill(symKey);

        // 20 bytes zero seed for OAEP
        byte[] seed = new byte[20];

        // RSA OAEP encrypt symKey
        byte[] encKey = RsaOaepEncryptFixedSeed(symKey, pubKey, seed);

        // SEED CBC encrypt message (16 bytes zero IV)
        byte[] iv = new byte[16];
        byte[] msgBytes = Encoding.UTF8.GetBytes(message);
        byte[] encData = SeedCbcEncrypt(msgBytes, symKey, iv);

        // ASN1 SEQUENCE [ INTEGER(encKey), OCTETSTRING(encData) ]
        var seq = new DerSequence(
            new DerInteger(new BigInteger(1, encKey)),
            new DerOctetString(encData)
        );

        byte[] der = seq.GetDerEncoded();
        return Convert.ToBase64String(der);
    }
}
