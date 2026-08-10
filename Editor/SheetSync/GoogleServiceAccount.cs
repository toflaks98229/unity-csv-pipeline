using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace CsvPipeline
{
    /// <summary>
    /// 서비스 계정 키로 구글 액세스 토큰을 받아 <b>비공개 시트</b>를 읽을 수 있게 합니다.
    /// 브라우저 로그인 흐름이 없어 배치 모드에서도 동작합니다.
    /// </summary>
    public static class GoogleServiceAccount
    {
        /// <summary>로그 접두사입니다.</summary>
        private const string TAG = "[SheetAuth]";

        /// <summary>export 엔드포인트로 시트를 내려받는 데 필요한 권한입니다.</summary>
        private const string Scope = "https://www.googleapis.com/auth/drive.readonly";

        /// <summary>토큰을 만료 직전까지 쓰지 않도록 남겨 두는 여유 시간(초)입니다.</summary>
        private const int ExpirySlackSeconds = 60;

        /// <summary>받아 둔 토큰입니다.</summary>
        private static string _token;

        /// <summary>받아 둔 토큰이 만료되는 시각입니다.</summary>
        private static DateTime _tokenExpiresAt = DateTime.MinValue;

        /// <summary>토큰을 발급받은 키 파일 경로입니다. 키가 바뀌면 다시 받습니다.</summary>
        private static string _tokenKeyPath;

        /// <summary>키가 있는지 마지막으로 확인한 결과입니다.</summary>
        private static bool _keyPresent;

        /// <summary>그 확인에 쓴 설정 경로입니다. 설정이 바뀌면 다시 확인합니다.</summary>
        private static string _keyCheckedFor;

        /// <summary>
        /// 서비스 계정 키가 설정돼 있는지 여부입니다.
        /// <b>디스크 확인 결과를 들고 있습니다.</b> 이 값을 그리기에서 읽는 화면이 있어,
        /// 부를 때마다 파일을 찾으면 마우스를 움직이는 동안 계속 디스크를 두드립니다.
        /// 설정의 경로가 바뀌면 그때 다시 봅니다.
        /// </summary>
        public static bool IsConfigured
        {
            get
            {
                string configured = CsvPipelineSettings.Instance.ServiceAccountKeyPath ?? string.Empty;
                if (configured == _keyCheckedFor) return _keyPresent;

                _keyCheckedFor = configured;
                _keyPresent = !string.IsNullOrEmpty(ResolveKeyPath());
                return _keyPresent;
            }
        }

        /// <summary>지금 캐시된 토큰을 버립니다. (키를 바꾼 뒤 호출)</summary>
        public static void InvalidateToken()
        {
            _token = null;
            _tokenExpiresAt = DateTime.MinValue;
            _tokenKeyPath = null;
            _keyCheckedFor = null;
        }

        /// <summary>
        /// 유효한 액세스 토큰을 돌려줍니다. 캐시된 것이 살아 있으면 그대로 씁니다.
        /// </summary>
        /// <returns>액세스 토큰이거나, 설정이 없거나 실패하면 null입니다.</returns>
        public static async Task<string> GetAccessTokenAsync()
        {
            string keyPath = ResolveKeyPath();
            if (string.IsNullOrEmpty(keyPath)) return null;

            bool valid = _token != null
                      && _tokenKeyPath == keyPath
                      && DateTime.UtcNow < _tokenExpiresAt.AddSeconds(-ExpirySlackSeconds);
            if (valid) return _token;

            ServiceAccountKey key = LoadKey(keyPath);
            if (key == null) return null;

            string assertion = BuildAssertion(key);
            if (assertion == null) return null;

            TokenResponse response = await RequestTokenAsync(key.token_uri, assertion);
            if (response == null || string.IsNullOrEmpty(response.access_token)) return null;

            _token = response.access_token;
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(response.expires_in > 0 ? response.expires_in : 3600);
            _tokenKeyPath = keyPath;
            return _token;
        }

        // ====================================================================================================
        // 키 파일
        // ====================================================================================================

        /// <summary>서비스 계정 키 JSON의 필요한 부분입니다.</summary>
        [Serializable]
        private class ServiceAccountKey
        {
#pragma warning disable 649   // JsonUtility가 채웁니다.
            public string client_email;
            public string private_key;
            public string token_uri;
#pragma warning restore 649
        }

        /// <summary>토큰 응답입니다.</summary>
        [Serializable]
        private class TokenResponse
        {
#pragma warning disable 649
            public string access_token;
            public int expires_in;
#pragma warning restore 649
        }

        /// <summary>설정된 키 파일의 절대 경로입니다. 없거나 파일이 존재하지 않으면 null입니다.</summary>
        /// <returns>키 파일 경로이거나 null입니다.</returns>
        private static string ResolveKeyPath()
        {
            string configured = CsvPipelineSettings.Instance.ServiceAccountKeyPath;
            if (string.IsNullOrWhiteSpace(configured)) return null;

            string full = Path.IsPathRooted(configured) ? configured : Path.GetFullPath(configured);
            return File.Exists(full) ? full : null;
        }

        /// <summary>키 파일을 읽어 필요한 항목을 꺼냅니다.</summary>
        /// <param name="path">키 파일 경로입니다.</param>
        /// <returns>읽어 낸 키이거나, 형식이 맞지 않으면 null입니다.</returns>
        private static ServiceAccountKey LoadKey(string path)
        {
            ServiceAccountKey key;
            try
            {
                key = JsonUtility.FromJson<ServiceAccountKey>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                // 키 내용은 절대 로그에 싣지 않습니다.
                Debug.LogError($"{TAG} 서비스 계정 키를 읽지 못했습니다: {e.GetType().Name}");
                return null;
            }

            if (key == null || string.IsNullOrEmpty(key.client_email) || string.IsNullOrEmpty(key.private_key))
            {
                Debug.LogError(
                    $"{TAG} 서비스 계정 키에 client_email 또는 private_key가 없습니다.\n"
                    + "Google Cloud Console ▸ 서비스 계정 ▸ 키 ▸ JSON 으로 받은 파일이 맞는지 확인하십시오.");
                return null;
            }

            if (string.IsNullOrEmpty(key.token_uri)) key.token_uri = "https://oauth2.googleapis.com/token";
            return key;
        }

        // ====================================================================================================
        // JWT
        // ====================================================================================================

        /// <summary>서명된 JWT 어서션을 만듭니다.</summary>
        /// <param name="key">서비스 계정 키입니다.</param>
        /// <returns>어서션 문자열이거나, 서명에 실패하면 null입니다.</returns>
        private static string BuildAssertion(ServiceAccountKey key)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            string header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));
            string claims = Base64Url(Encoding.UTF8.GetBytes(
                "{\"iss\":\"" + key.client_email + "\""
                + ",\"scope\":\"" + Scope + "\""
                + ",\"aud\":\"" + key.token_uri + "\""
                + ",\"exp\":" + (now + 3600)
                + ",\"iat\":" + now + "}"));

            string payload = header + "." + claims;

            byte[] signature = Sign(Encoding.UTF8.GetBytes(payload), key.private_key);
            return signature == null ? null : payload + "." + Base64Url(signature);
        }

        /// <summary>PKCS#8 PEM 개인키로 RS256 서명을 만듭니다.</summary>
        /// <param name="data">서명할 바이트입니다.</param>
        /// <param name="privateKeyPem">PEM 형식의 개인키입니다.</param>
        /// <returns>서명 바이트이거나, 실패하면 null입니다.</returns>
        private static byte[] Sign(byte[] data, string privateKeyPem)
        {
            byte[] der;
            try
            {
                string body = privateKeyPem
                    .Replace("-----BEGIN PRIVATE KEY-----", string.Empty)
                    .Replace("-----END PRIVATE KEY-----", string.Empty)
                    .Replace("\\n", string.Empty)
                    .Replace("\r", string.Empty)
                    .Replace("\n", string.Empty)
                    .Trim();
                der = Convert.FromBase64String(body);
            }
            catch (FormatException)
            {
                Debug.LogError($"{TAG} 개인키가 PKCS#8 PEM 형식이 아닙니다. (-----BEGIN PRIVATE KEY----- 로 시작해야 합니다)");
                return null;
            }

            try
            {
                using (RSA rsa = RSA.Create())
                {
                    rsa.ImportPkcs8PrivateKey(der, out _);
                    return rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
            }
            catch (Exception e)
            {
                // 런타임이 PKCS#8 가져오기를 지원하지 않으면 여기서 걸립니다. 원인을 분명히 알립니다.
                Debug.LogError(
                    $"{TAG} 개인키로 서명하지 못했습니다: {e.GetType().Name} — {e.Message}\n"
                    + "이 에디터의 런타임이 RSA PKCS#8 가져오기를 지원하지 않을 수 있습니다. "
                    + "그런 경우 비공개 시트 대신 '링크가 있는 모든 사용자 · 뷰어' 공개를 쓰십시오.");
                return null;
            }
        }

        /// <summary>URL 안전 Base64로 인코딩합니다. (패딩 제거)</summary>
        /// <param name="bytes">인코딩할 바이트입니다.</param>
        /// <returns>인코딩된 문자열입니다.</returns>
        private static string Base64Url(byte[] bytes)
            => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        // ====================================================================================================
        // 토큰 요청
        // ====================================================================================================

        /// <summary>어서션을 토큰으로 교환합니다.</summary>
        /// <param name="tokenUri">토큰 엔드포인트입니다.</param>
        /// <param name="assertion">서명된 JWT입니다.</param>
        /// <returns>토큰 응답이거나, 실패하면 null입니다.</returns>
        private static Task<TokenResponse> RequestTokenAsync(string tokenUri, string assertion)
        {
            var completion = new TaskCompletionSource<TokenResponse>();

            var form = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
                new MultipartFormDataSection("assertion", assertion)
            };

            UnityWebRequest request = UnityWebRequest.Post(tokenUri, form);
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            operation.completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        // 응답 본문에 오류 사유가 들어 있습니다. 토큰이 아니므로 실어도 안전합니다.
                        Debug.LogError(
                            $"{TAG} 토큰 발급 실패 {request.responseCode} — {request.downloadHandler?.text}\n"
                            + "시트를 서비스 계정 이메일과 공유했는지, Google Sheets/Drive API가 켜져 있는지 확인하십시오.");
                        completion.SetResult(null);
                        return;
                    }

                    completion.SetResult(JsonUtility.FromJson<TokenResponse>(request.downloadHandler.text));
                }
                catch (Exception e)
                {
                    Debug.LogError($"{TAG} 토큰 응답을 해석하지 못했습니다: {e.GetType().Name}");
                    completion.SetResult(null);
                }
                finally
                {
                    request.Dispose();
                }
            };

            return completion.Task;
        }
    }
}
