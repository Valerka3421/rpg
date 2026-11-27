using System;
using System.Security.Cryptography;

namespace Aethelgard.Net;

// Экспорт/импорт SubjectPublicKeyInfo (SPKI) для ECDH P-256 вручную через
// ASN.1 DER: ECDiffieHellmanPublicKey.ExportSubjectPublicKeyInfo/ImportSubjectPublicKeyInfo
// появились в net5+, а в Unity-профиле их нет. Ручной формат гарантирует
// одинаковое представление ключа в MonoGame- и Unity-клиентах (совместимость MP).
public static class EcdhInterop
{
    private static readonly byte[] OidEcPublicKey = { 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x02, 0x01 };      // 1.2.840.10045.2.1
    private static readonly byte[] OidP256 = { 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x03, 0x01, 0x07 };       // 1.2.840.10045.3.1.7

    /// <summary>SPKI-экспорт публичного ключа ECDH (uncompressed point).</summary>
    public static byte[] ExportSpki(ECDiffieHellman dh)
    {
        var q = dh.ExportParameters(false).Q;
        byte[] point = new byte[1 + q.X.Length + q.Y.Length];
        point[0] = 0x04;  // uncompressed
        Array.Copy(q.X, 0, point, 1, q.X.Length);
        Array.Copy(q.Y, 0, point, 1 + q.X.Length, q.Y.Length);

        var alg = Seq(DerOid(OidEcPublicKey), DerOid(OidP256));
        return Seq(alg, DerTag(0x03, Concat(new byte[] { 0x00 }, point)));
    }

    /// <summary>SPKI-импорт: парсит DER и загружает Q в dh.</summary>
    public static void ImportSpki(ECDiffieHellman dh, byte[] spki)
    {
        int pos = 0;
        ReadSeq(spki, ref pos);                       // SubjectPublicKeyInfo
        ReadSeq(spki, ref pos);                       // AlgorithmIdentifier
        ReadOid(spki, ref pos);                       // ecPublicKey
        ReadOid(spki, ref pos);                       // P-256
        var bits = ReadTag(spki, ref pos, 0x03);      // BIT STRING
        if (bits.Length < 65 || bits[0] != 0x00 || bits[1] != 0x04)
            throw new InvalidCastException("Ожидается uncompressed EC-точка P-256");

        int half = (bits.Length - 2) / 2;
        dh.ImportParameters(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = Copy(bits, 2, half), Y = Copy(bits, 2 + half, half) },
        });
    }

    private static byte[] Copy(byte[] src, int off, int len)
    {
        var r = new byte[len];
        Array.Copy(src, off, r, 0, len);
        return r;
    }

    private static byte[] Concat(byte[] a, byte[] b)
    {
        var r = new byte[a.Length + b.Length];
        Array.Copy(a, 0, r, 0, a.Length);
        Array.Copy(b, 0, r, a.Length, b.Length);
        return r;
    }

    private static byte[] DerOid(byte[] oid) => DerTag(0x06, oid);
    private static byte[] DerTag(byte tag, byte[] body)
    {
        int len = body.Length;
        if (len < 128) return Concat(new[] { tag, (byte)len }, body);
        int n = 1;
        int v = len;
        while (v >= 128) { v >>= 8; n++; }
        var head = new byte[2 + n];
        head[0] = tag;
        head[1] = (byte)(0x80 | n);
        for (int i = 0; i < n; i++) head[2 + i] = (byte)(len >> (8 * (n - 1 - i)));
        return Concat(head, body);
    }

    private static byte[] Seq(params byte[][] items) => DerTag(0x30, ConcatAll(items));

    private static byte[] ConcatAll(byte[][] items)
    {
        int total = 0;
        foreach (var it in items) total += it.Length;
        var r = new byte[total];
        int o = 0;
        foreach (var it in items) { Array.Copy(it, 0, r, o, it.Length); o += it.Length; }
        return r;
    }

    /// <summary>SEQ: пропускает только заголовок — вложенные элементы читаются
    /// следующими ReadTag/ReadOid внутри тела.</summary>
    private static void ReadSeq(byte[] d, ref int pos)
    {
        if (d[pos++] != 0x30) throw new InvalidCastException("Ожидался DER-тег 0x30 (SEQUENCE)");
        int len = d[pos++];
        if (len > 127)
        {
            int n = len & 0x7F;
            len = 0;
            for (int i = 0; i < n; i++) len = (len << 8) | d[pos++];
        }
        _ = len;  // тело ограничивает дальнейшее чтение структурно
    }

    private static byte[] ReadTag(byte[] d, ref int pos, byte tag)
    {
        if (d[pos++] != tag) throw new InvalidCastException($"Ожидался DER-тег 0x{tag:X2}");
        int len = d[pos++];
        if (len > 127)
        {
            int n = len & 0x7F;
            len = 0;
            for (int i = 0; i < n; i++) len = (len << 8) | d[pos++];
        }
        var r = new byte[len];
        Array.Copy(d, pos, r, 0, len);
        pos += len;
        return r;
    }

    private static byte[] ReadOid(byte[] d, ref int pos) => ReadTag(d, ref pos, 0x06);
}
