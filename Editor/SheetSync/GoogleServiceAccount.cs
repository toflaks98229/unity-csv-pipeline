using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace CsvPipeline
{
    /// <summary>
    /// Gets a Google access token from a service account key so <b>private sheets</b> can be read.
    /// There is no browser login flow, so it also works in batch mode.
    /// </summary>
    public static class GoogleServiceAccount
    {
        /// <summary>Log prefix.</summary>
        private const string TAG = "[SheetAuth]";

        /// <summary>Scope required to download a sheet through the export endpoint.</summary>
        private const string Scope = "https://www.googleapis.com/auth/drive.readonly";

        /// <summary>Slack (in seconds) kept so a token is not used right up to its expiry.</summary>
        private const int ExpirySlackSeconds = 60;

        /// <summary>How long (in seconds) to wait for a token. Without a response the whole pull stops here.</summary>
        private const int TokenTimeoutSeconds = 30;

        /// <summary>The token already obtained.</summary>
        private static string _token;

        /// <summary>The time the obtained token expires.</summary>
        private static DateTime _tokenExpiresAt = DateTime.MinValue;

        /// <summary>Path of the key file the token was issued for. A changed key means a new token.</summary>
        private static string _tokenKeyPath;

        /// <summary>Result of the last check for whether the key exists.</summary>
        private static bool _keyPresent;

        /// <summary>The settings path that check used. A changed setting means another check.</summary>
        private static string _keyCheckedFor;

        /// <summary>The time the disk was last looked at.</summary>
        private static double _keyCheckedAt = double.NegativeInfinity;

        /// <summary>How long (in seconds) the disk check result is held.</summary>
        private const double KeyCheckCacheSeconds = 2.0;

        /// <summary>
        /// Whether a service account key is configured.
        /// <para>
        /// <b>The disk check result is held briefly.</b> A window reads this value while drawing, so
        /// looking for the file on every call keeps hitting the disk the whole time the mouse moves.
        /// </para>
        /// <para>
        /// But <b>it must not be held on the path alone.</b> It used to be, and then a deleted or moved
        /// key file still answered "configured" because the settings string had not changed. The window
        /// says authentication is alive while the pull goes out unauthenticated, so <b>private sheets fail
        /// silently</b>. Expiring by time keeps the drawing cost down and removes that lie.
        /// </para>
        /// </summary>
        public static bool IsConfigured
        {
            get
            {
                string configured = CsvPipelineSettings.Instance.ServiceAccountKeyPath ?? string.Empty;
                double now = EditorApplication.timeSinceStartup;

                if (configured == _keyCheckedFor && now - _keyCheckedAt < KeyCheckCacheSeconds) return _keyPresent;

                _keyCheckedFor = configured;
                _keyCheckedAt = now;
                _keyPresent = !string.IsNullOrEmpty(ResolveKeyPath());
                return _keyPresent;
            }
        }

        /// <summary>Drops the currently cached token. (Call after changing the key)</summary>
        public static void InvalidateToken()
        {
            _token = null;
            _tokenExpiresAt = DateTime.MinValue;
            _tokenKeyPath = null;
            _keyCheckedFor = null;
            _keyCheckedAt = double.NegativeInfinity;
        }

        /// <summary>
        /// Returns a valid access token. A cached one that is still alive is used as is.
        /// </summary>
        /// <returns>The access token, or null when nothing is configured or the request fails.</returns>
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

        /// <summary>The parts of the service account key JSON that are needed.</summary>
        [Serializable]
        private class ServiceAccountKey
        {
#pragma warning disable 649   // JsonUtility가 채웁니다.
            public string client_email;
            public string private_key;
            public string token_uri;
#pragma warning restore 649
        }

        /// <summary>The token response.</summary>
        [Serializable]
        private class TokenResponse
        {
#pragma warning disable 649
            public string access_token;
            public int expires_in;
#pragma warning restore 649
        }

        /// <summary>Absolute path of the configured key file. Null when unset or the file does not exist.</summary>
        /// <returns>The key file path, or null.</returns>
        private static string ResolveKeyPath()
        {
            string configured = CsvPipelineSettings.Instance.ServiceAccountKeyPath;
            if (string.IsNullOrWhiteSpace(configured)) return null;

            string full = Path.IsPathRooted(configured) ? configured : Path.GetFullPath(configured);
            return File.Exists(full) ? full : null;
        }

        /// <summary>Reads the key file and pulls out the entries that are needed.</summary>
        /// <param name="path">Path to the key file.</param>
        /// <returns>The key that was read, or null when the format does not match.</returns>
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
                Debug.LogError($"{TAG} Failed to read the service account key: {e.GetType().Name}");
                return null;
            }

            if (key == null || string.IsNullOrEmpty(key.client_email) || string.IsNullOrEmpty(key.private_key))
            {
                Debug.LogError(
                    $"{TAG} The service account key has no client_email or private_key.\n"
                    + "Check that this is the file downloaded from Google Cloud Console ▸ Service Accounts ▸ Keys ▸ JSON.");
                return null;
            }

            if (string.IsNullOrEmpty(key.token_uri)) key.token_uri = "https://oauth2.googleapis.com/token";
            return key;
        }

        // ====================================================================================================
        // JWT
        // ====================================================================================================

        /// <summary>Builds a signed JWT assertion.</summary>
        /// <param name="key">The service account key.</param>
        /// <returns>The assertion string, or null when signing fails.</returns>
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

        /// <summary>Creates an RS256 signature with a PKCS#8 PEM private key.</summary>
        /// <param name="data">Bytes to sign.</param>
        /// <param name="privateKeyPem">The private key in PEM form.</param>
        /// <returns>The signature bytes, or null on failure.</returns>
        internal static byte[] Sign(byte[] data, string privateKeyPem)
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
                Debug.LogError($"{TAG} The private key is not in PKCS#8 PEM form. (It must start with -----BEGIN PRIVATE KEY-----)");
                return null;
            }

            // 키를 직접 풀어 ImportParameters 로 넣습니다.
            //
            // ImportPkcs8PrivateKey 를 쓰지 않는 이유는 Unity 2022.3 의 Mono 에서 그것이 언제나
            // PlatformNotSupportedException 을 던지기 때문입니다 — RSA.Create() 가 돌려주는
            // RSACryptoServiceProvider 에 그 기능이 없습니다. 그래서 이 패키지가 선언한 최소 판에서
            // 비공개 시트 인증이 통째로 동작하지 않았습니다. ImportParameters 는 어디서나 됩니다.
            if (!Pkcs8Rsa.TryRead(der, out RSAParameters parameters, out string problem))
            {
                Debug.LogError($"{TAG} {problem}");
                return null;
            }

            try
            {
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.ImportParameters(parameters);
                    return rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
            }
            catch (Exception e)
            {
                // 키 내용은 절대 로그에 싣지 않습니다. 타입 이름과 메시지까지만 남깁니다.
                Debug.LogError(
                    $"{TAG} Failed to sign with the private key: {e.GetType().Name} — {e.Message}\n"
                    + "  The key file may be corrupted, or it may not be a service account key. "
                    + "Download it again from Google Cloud Console ▸ Service Accounts ▸ Keys ▸ JSON.");
                return null;
            }
        }

        /// <summary>Encodes as URL-safe Base64. (Padding removed)</summary>
        /// <param name="bytes">Bytes to encode.</param>
        /// <returns>The encoded string.</returns>
        private static string Base64Url(byte[] bytes)
            => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        // ====================================================================================================
        // 토큰 요청
        // ====================================================================================================

        /// <summary>Exchanges the assertion for a token.</summary>
        /// <param name="tokenUri">The token endpoint.</param>
        /// <param name="assertion">The signed JWT.</param>
        /// <returns>The token response, or null on failure.</returns>
        private static Task<TokenResponse> RequestTokenAsync(string tokenUri, string assertion)
        {
            var completion = new TaskCompletionSource<TokenResponse>();

            var form = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
                new MultipartFormDataSection("assertion", assertion)
            };

            UnityWebRequest request = UnityWebRequest.Post(tokenUri, form);
            request.timeout = TokenTimeoutSeconds;

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            operation.completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        // 응답 본문에 오류 사유가 들어 있습니다. 토큰이 아니므로 실어도 안전합니다.
                        Debug.LogError(
                            $"{TAG} Token request failed {request.responseCode} — {request.downloadHandler?.text}\n"
                            + "Check that the sheet is shared with the service account email and that the Google Sheets/Drive API is enabled.");
                        completion.SetResult(null);
                        return;
                    }

                    completion.SetResult(JsonUtility.FromJson<TokenResponse>(request.downloadHandler.text));
                }
                catch (Exception e)
                {
                    Debug.LogError($"{TAG} Failed to parse the token response: {e.GetType().Name}");
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
