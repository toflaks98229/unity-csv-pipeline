using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 비공개 시트 인증의 <b>유일한 암호 연산</b>을 검사합니다.
    /// <para>
    /// 이 자리는 검사가 0건이었고, 그래서 <c>RSA.ImportPkcs8PrivateKey</c>가 Unity 2022.3 의 Mono
    /// 에서 <c>PlatformNotSupportedException</c>을 던진다는 사실이 <b>드러날 곳이 없었습니다.</b>
    /// 유료 차별화 기능 하나가 선언한 최소 판에서 통째로 동작하지 않고 있었습니다.
    /// </para>
    /// <para>
    /// 키는 <b>검사가 돌 때 만듭니다.</b> 개인키를 저장소에 넣지 않으려는 것이기도 하고, 매번 다른
    /// 키로 도는 편이 우연히 맞는 경우를 걸러 내기 때문이기도 합니다.
    /// </para>
    /// </summary>
    public sealed class Pkcs8RsaTests
    {
        /// <summary>검사용 키 한 벌입니다. 만드는 데 시간이 걸려 한 번만 만듭니다.</summary>
        private static RSAParameters _full;
        private static string _pem;

        /// <summary>키를 만들고 PKCS#8 PEM 으로 적어 둡니다.</summary>
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                _full = rsa.ExportParameters(true);
            }
            _pem = "-----BEGIN PRIVATE KEY-----\n"
                 + Convert.ToBase64String(Pkcs8(_full))
                 + "\n-----END PRIVATE KEY-----\n";
        }

        // ====================================================================================================

        /// <summary>실제 PKCS#8 바이트에서 매개변수가 그대로 나옵니다.</summary>
        [Test]
        public void PKCS8을_읽어_매개변수를_그대로_돌려준다()
        {
            Assert.IsTrue(Pkcs8Rsa.TryRead(Pkcs8(_full), out RSAParameters read, out string problem), problem);

            CollectionAssert.AreEqual(_full.Modulus, read.Modulus, "Modulus");
            CollectionAssert.AreEqual(_full.Exponent, read.Exponent, "Exponent");
            CollectionAssert.AreEqual(_full.D, read.D, "D");
            CollectionAssert.AreEqual(_full.P, read.P, "P");
            CollectionAssert.AreEqual(_full.Q, read.Q, "Q");
            CollectionAssert.AreEqual(_full.DP, read.DP, "DP");
            CollectionAssert.AreEqual(_full.DQ, read.DQ, "DQ");
            CollectionAssert.AreEqual(_full.InverseQ, read.InverseQ, "InverseQ");
        }

        /// <summary>
        /// <b>이 검사가 B2 의 답입니다.</b> 서명이 나오고, 그 서명이 공개키로 검증됩니다.
        /// 통과한다면 이 에디터에서 비공개 시트 인증이 실제로 동작합니다.
        /// </summary>
        [Test]
        public void PEM_개인키로_만든_RS256_서명이_검증된다()
        {
            byte[] data = Encoding.UTF8.GetBytes("eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.payload");

            byte[] signature = GoogleServiceAccount.Sign(data, _pem);

            Assert.IsNotNull(signature, "서명하지 못했습니다 — 이 런타임에서 비공개 시트 인증이 동작하지 않습니다.");
            Assert.AreEqual(256, signature.Length, "2048비트 키의 RS256 서명은 256바이트입니다.");

            using (var verifier = new RSACryptoServiceProvider())
            {
                verifier.ImportParameters(new RSAParameters { Modulus = _full.Modulus, Exponent = _full.Exponent });
                Assert.IsTrue(
                    verifier.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
                    "서명이 나왔지만 공개키로 검증되지 않습니다.");
            }
        }

        /// <summary>구글이 JSON 에 담아 주는 <c>\n</c> 이스케이프 형태도 그대로 읽습니다.</summary>
        [Test]
        public void JSON_이스케이프된_줄바꿈도_읽는다()
        {
            string escaped = _pem.Replace("\n", "\\n");
            byte[] data = Encoding.UTF8.GetBytes("payload");

            Assert.IsNotNull(GoogleServiceAccount.Sign(data, escaped),
                             "서비스 계정 JSON 의 private_key 는 줄바꿈이 \\n 으로 들어 있습니다.");
        }

        /// <summary>PEM 이 아니면 무엇이 잘못됐는지 말하고 멈춥니다.</summary>
        [Test]
        public void PEM이_아니면_이유를_말하고_멈춘다()
        {
            LogAssert.Expect(LogType.Error, new Regex("PKCS#8 PEM"));
            Assert.IsNull(GoogleServiceAccount.Sign(Encoding.UTF8.GetBytes("x"), "이건 키가 아닙니다"));
        }

        /// <summary>바이트가 잘렸으면 조용히 이상한 키를 만들지 않고 거절합니다.</summary>
        [Test]
        public void 잘린_키는_거절한다()
        {
            byte[] full = Pkcs8(_full);
            var cut = new byte[full.Length / 2];
            Buffer.BlockCopy(full, 0, cut, 0, cut.Length);

            Assert.IsFalse(Pkcs8Rsa.TryRead(cut, out _, out string problem));
            Assert.IsNotNull(problem);
        }

        /// <summary>빈 입력도 예외 없이 거절합니다.</summary>
        [Test]
        public void 빈_키는_거절한다()
        {
            Assert.IsFalse(Pkcs8Rsa.TryRead(new byte[0], out _, out string problem));
            Assert.IsNotNull(problem);
        }

        /// <summary>RSA 가 아닌 알고리즘 OID 는 무엇이 잘못됐는지 말합니다.</summary>
        [Test]
        public void RSA가_아니면_그렇다고_말한다()
        {
            // AlgorithmIdentifier 의 OID 만 ed25519(1.3.101.112)로 바꾼 껍데기를 만듭니다.
            byte[] der = Sequence(
                Integer(new byte[] { 0 }),
                Sequence(Tag(0x06, new byte[] { 0x2B, 0x65, 0x70 })),
                Tag(0x04, Sequence(Integer(new byte[] { 0 }))));

            Assert.IsFalse(Pkcs8Rsa.TryRead(der, out _, out string problem));
            StringAssert.Contains("RSA", problem);
        }

        // ====================================================================================================
        // 검사용 DER 작성기 — 진짜 PKCS#8 바이트를 만들어 넣기 위한 것입니다.
        // ====================================================================================================

        /// <summary>매개변수를 암호화되지 않은 PKCS#8 DER 로 적습니다.</summary>
        /// <param name="p">적을 RSA 매개변수입니다.</param>
        /// <returns>PKCS#8 바이트입니다.</returns>
        private static byte[] Pkcs8(RSAParameters p)
        {
            byte[] rsaKey = Sequence(
                Integer(new byte[] { 0 }),
                Integer(p.Modulus),
                Integer(p.Exponent),
                Integer(p.D),
                Integer(p.P),
                Integer(p.Q),
                Integer(p.DP),
                Integer(p.DQ),
                Integer(p.InverseQ));

            byte[] algorithm = Sequence(
                Tag(0x06, new byte[] { 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01 }),   // rsaEncryption
                Tag(0x05, new byte[0]));                                                          // NULL

            return Sequence(Integer(new byte[] { 0 }), algorithm, Tag(0x04, rsaKey));
        }

        /// <summary>내용들을 SEQUENCE 로 감쌉니다.</summary>
        /// <param name="parts">담을 내용들입니다.</param>
        /// <returns>DER 바이트입니다.</returns>
        private static byte[] Sequence(params byte[][] parts)
        {
            var body = new List<byte>();
            foreach (byte[] part in parts) body.AddRange(part);
            return Tag(0x30, body.ToArray());
        }

        /// <summary>부호 없는 크기를 DER INTEGER 로 적습니다. 최상위 비트가 서 있으면 0을 앞에 붙입니다.</summary>
        /// <param name="magnitude">적을 크기 바이트열입니다.</param>
        /// <returns>DER 바이트입니다.</returns>
        private static byte[] Integer(byte[] magnitude)
        {
            int start = 0;
            while (start < magnitude.Length - 1 && magnitude[start] == 0) start++;

            var trimmed = new byte[magnitude.Length - start];
            Buffer.BlockCopy(magnitude, start, trimmed, 0, trimmed.Length);

            if ((trimmed[0] & 0x80) == 0) return Tag(0x02, trimmed);

            var positive = new byte[trimmed.Length + 1];
            Buffer.BlockCopy(trimmed, 0, positive, 1, trimmed.Length);
            return Tag(0x02, positive);
        }

        /// <summary>태그와 길이를 붙입니다.</summary>
        /// <param name="tag">붙일 태그입니다.</param>
        /// <param name="content">내용입니다.</param>
        /// <returns>DER 바이트입니다.</returns>
        private static byte[] Tag(byte tag, byte[] content)
        {
            var result = new List<byte> { tag };

            if (content.Length < 0x80)
            {
                result.Add((byte)content.Length);
            }
            else
            {
                var length = new List<byte>();
                int n = content.Length;
                while (n > 0) { length.Insert(0, (byte)(n & 0xFF)); n >>= 8; }

                result.Add((byte)(0x80 | length.Count));
                result.AddRange(length);
            }

            result.AddRange(content);
            return result.ToArray();
        }
    }
}
