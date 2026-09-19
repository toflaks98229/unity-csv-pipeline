using System;
using System.Security.Cryptography;

namespace CsvPipeline
{
    /// <summary>
    /// Unpacks PKCS#8 private key bytes into <see cref="RSAParameters"/>.
    /// <para>
    /// <b>There is a reason this is written by hand.</b> .NET has <c>RSA.ImportPkcs8PrivateKey</c>, but
    /// on the Mono runtime of Unity 2022.3 it throws <c>PlatformNotSupportedException</c>
    /// (<c>RSA.Create()</c> returns an <c>RSACryptoServiceProvider</c>, and that implementation does not
    /// have this feature). That is why <b>private sheet authentication did not work at all on the minimum
    /// version</b> this package declares.
    /// </para>
    /// <para>
    /// <c>RSA.ImportParameters</c> works everywhere. So the key is unpacked by hand and fed into that instead.
    /// No third-party library is pulled in because this package has zero dependencies — breaking that promise
    /// for one piece of authentication costs the buyer more.
    /// </para>
    /// <para>
    /// Only RSA keys inside <b>unencrypted</b> PKCS#8 (<c>-----BEGIN PRIVATE KEY-----</c>) are handled.
    /// That is the form Google hands out as a service account key.
    /// </para>
    /// </summary>
    internal static class Pkcs8Rsa
    {
        /// <summary>DER encoding of the rsaEncryption OID (1.2.840.113549.1.1.1).</summary>
        private static readonly byte[] RsaOid = { 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01 };

        /// <summary>
        /// Pulls the RSA private key parameters out of PKCS#8 bytes.
        /// </summary>
        /// <param name="pkcs8">DER bytes from base64-decoding the PEM body.</param>
        /// <param name="parameters">Receives the parameters that were read.</param>
        /// <param name="problem">Receives the reason it could not be read. Null on success.</param>
        /// <returns>True when it was read.</returns>
        public static bool TryRead(byte[] pkcs8, out RSAParameters parameters, out string problem)
        {
            parameters = default;
            problem = null;

            if (pkcs8 == null || pkcs8.Length == 0)
            {
                problem = "The private key is empty.";
                return false;
            }

            try
            {
                var reader = new DerReader(pkcs8);

                // PrivateKeyInfo ::= SEQUENCE { version, privateKeyAlgorithm, privateKey }
                DerReader info = reader.ReadSequence();

                byte[] version = info.ReadInteger();
                if (version.Length != 1 || version[0] != 0)
                {
                    problem = "The PKCS#8 version is not 0. That format is not supported.";
                    return false;
                }

                // AlgorithmIdentifier ::= SEQUENCE { algorithm OID, parameters }
                DerReader algorithm = info.ReadSequence();
                byte[] oid = algorithm.ReadObjectIdentifier();
                if (!SameBytes(oid, RsaOid))
                {
                    problem = "This is not an RSA key. Google service account keys are RSA — "
                            + "check whether the setting points at a different kind of key file.";
                    return false;
                }

                // privateKey OCTET STRING 안에 RSAPrivateKey 가 다시 들어 있습니다.
                byte[] inner = info.ReadOctetString();
                var key = new DerReader(inner).ReadSequence();

                byte[] keyVersion = key.ReadInteger();
                if (keyVersion.Length != 1 || keyVersion[0] != 0)
                {
                    problem = "Multi-prime RSA keys are not supported.";
                    return false;
                }

                byte[] modulus = key.ReadInteger();
                byte[] exponent = key.ReadInteger();
                byte[] d = key.ReadInteger();
                byte[] p = key.ReadInteger();
                byte[] q = key.ReadInteger();
                byte[] dp = key.ReadInteger();
                byte[] dq = key.ReadInteger();
                byte[] inverseQ = key.ReadInteger();

                // RSAParameters 는 길이가 어긋나면 CryptographicException 을 냅니다.
                // DER 정수는 앞의 0을 떼고 오므로 여기서 다시 채워 줍니다.
                int half = (modulus.Length + 1) / 2;

                parameters = new RSAParameters
                {
                    Modulus  = Pad(modulus, modulus.Length),
                    Exponent = exponent,
                    D        = Pad(d, modulus.Length),
                    P        = Pad(p, half),
                    Q        = Pad(q, half),
                    DP       = Pad(dp, half),
                    DQ       = Pad(dq, half),
                    InverseQ = Pad(inverseQ, half),
                };
                return true;
            }
            catch (Exception e) when (e is InvalidOperationException || e is IndexOutOfRangeException
                                   || e is ArgumentException || e is OverflowException)
            {
                problem = $"Failed to parse the private key: {e.GetType().Name}. "
                        + "The file may be truncated, or it may not be in -----BEGIN PRIVATE KEY----- form.";
                return false;
            }
        }

        /// <summary>Left-pads with zeros to the exact length.</summary>
        /// <param name="value">Byte array to pad.</param>
        /// <param name="length">Length to match.</param>
        /// <returns>The byte array at the matched length.</returns>
        private static byte[] Pad(byte[] value, int length)
        {
            if (value.Length == length) return value;
            if (value.Length > length) throw new ArgumentException("The value is longer than the expected length.");

            var padded = new byte[length];
            Buffer.BlockCopy(value, 0, padded, length - value.Length, value.Length);
            return padded;
        }

        /// <summary>Checks whether two byte arrays are the same.</summary>
        /// <param name="a">First byte array.</param>
        /// <param name="b">Second byte array.</param>
        /// <returns>True when they are the same.</returns>
        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        /// <summary>
        /// A minimal reader that walks DER from the front. It handles only as much as this file uses.
        /// </summary>
        private sealed class DerReader
        {
            private readonly byte[] _data;
            private int _at;
            private readonly int _end;

            /// <summary>Creates a reader over the whole byte array.</summary>
            /// <param name="data">Byte array to read.</param>
            public DerReader(byte[] data) : this(data, 0, data.Length) { }

            /// <summary>Creates a reader over only part of the byte array.</summary>
            /// <param name="data">Byte array to read.</param>
            /// <param name="at">Start position.</param>
            /// <param name="end">End position.</param>
            private DerReader(byte[] data, int at, int end)
            {
                _data = data;
                _at = at;
                _end = end;
            }

            /// <summary>Opens a SEQUENCE and returns a reader over its contents.</summary>
            /// <returns>A reader that sees only the contents.</returns>
            public DerReader ReadSequence() => ReadTagged(0x30);

            /// <summary>Returns the contents of an OCTET STRING.</summary>
            /// <returns>The content bytes.</returns>
            public byte[] ReadOctetString() => ReadContent(0x04);

            /// <summary>Returns the contents of an OBJECT IDENTIFIER.</summary>
            /// <returns>The content bytes.</returns>
            public byte[] ReadObjectIdentifier() => ReadContent(0x06);

            /// <summary>Returns an INTEGER <b>with leading zeros stripped</b>.</summary>
            /// <returns>The unsigned magnitude bytes.</returns>
            public byte[] ReadInteger()
            {
                byte[] raw = ReadContent(0x02);

                // DER 정수는 최상위 비트가 1이면 앞에 0x00 을 붙여 양수임을 밝힙니다.
                // RSAParameters 는 부호 없는 크기를 기대하므로 그것을 떼어 냅니다.
                int start = 0;
                while (start < raw.Length - 1 && raw[start] == 0x00) start++;

                if (start == 0) return raw;

                var trimmed = new byte[raw.Length - start];
                Buffer.BlockCopy(raw, start, trimmed, 0, trimmed.Length);
                return trimmed;
            }

            /// <summary>Reads the expected tag and returns a reader that sees only its contents.</summary>
            /// <param name="tag">The expected tag.</param>
            /// <returns>A reader over the contents.</returns>
            private DerReader ReadTagged(byte tag)
            {
                int length = OpenTag(tag);
                var inner = new DerReader(_data, _at, _at + length);
                _at += length;
                return inner;
            }

            /// <summary>Reads the expected tag and returns a copy of its contents.</summary>
            /// <param name="tag">The expected tag.</param>
            /// <returns>The content bytes.</returns>
            private byte[] ReadContent(byte tag)
            {
                int length = OpenTag(tag);
                var content = new byte[length];
                Buffer.BlockCopy(_data, _at, content, 0, length);
                _at += length;
                return content;
            }

            /// <summary>Reads the tag and the length, and returns the length of the contents.</summary>
            /// <param name="tag">The expected tag.</param>
            /// <returns>The length of the contents.</returns>
            private int OpenTag(byte tag)
            {
                if (_at >= _end) throw new InvalidOperationException("The DER is shorter than expected.");
                if (_data[_at] != tag)
                {
                    throw new InvalidOperationException(
                        $"The DER tag differs. Expected: 0x{tag:X2}, actual: 0x{_data[_at]:X2}");
                }
                _at++;

                int length = _data[_at++];

                // 0x80 이 서 있으면 뒤따르는 바이트 수만큼이 길이입니다. (긴 형식)
                if ((length & 0x80) != 0)
                {
                    int count = length & 0x7F;
                    if (count == 0 || count > 4) throw new InvalidOperationException("This DER length form cannot be handled.");

                    length = 0;
                    for (int i = 0; i < count; i++) length = (length << 8) | _data[_at++];
                }

                if (length < 0 || _at + length > _end) throw new InvalidOperationException("The DER length runs past the range.");
                return length;
            }
        }
    }
}
