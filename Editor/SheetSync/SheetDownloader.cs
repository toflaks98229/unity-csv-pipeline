using System;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace CsvPipeline
{
    /// <summary>The result of one pull. On success <see cref="Text"/> is filled, otherwise <see cref="Error"/> is.</summary>
    public readonly struct SheetFetch
    {
        /// <summary>Whether both the pull and the checks passed.</summary>
        public bool Ok { get; }

        /// <summary>The normalized table text. Null on failure.</summary>
        public string Text { get; }

        /// <summary>The reason for the failure. Null on success.</summary>
        public string Error { get; }

        /// <summary>Whether a login page came back because there is no access.</summary>
        public bool IsAccessDenied { get; }

        /// <summary>Builds a result.</summary>
        /// <param name="ok">Whether it passed.</param>
        /// <param name="text">The text that was pulled.</param>
        /// <param name="error">The reason for the failure.</param>
        /// <param name="accessDenied">Whether this is an access problem.</param>
        private SheetFetch(bool ok, string text, string error, bool accessDenied)
        {
            Ok = ok;
            Text = text;
            Error = error;
            IsAccessDenied = accessDenied;
        }

        /// <summary>Builds a success result.</summary>
        /// <param name="text">The normalized text.</param>
        /// <returns>The success result.</returns>
        public static SheetFetch Success(string text) => new SheetFetch(true, text, null, false);

        /// <summary>Builds a failure result.</summary>
        /// <param name="error">The reason for the failure.</param>
        /// <param name="accessDenied">Whether this is an access problem.</param>
        /// <returns>The failure result.</returns>
        public static SheetFetch Failure(string error, bool accessDenied = false)
            => new SheetFetch(false, null, error, accessDenied);
    }

    /// <summary>
    /// Pulls the sheet body and returns it, <b>after checking that it really is a table</b>.
    /// Pulling, authentication, and checking all live here, so callers only deal with success or failure.
    /// </summary>
    public static class SheetDownloader
    {
        /// <summary>How long (in seconds) to wait for one pull. Generous enough for a large table, but not unlimited.</summary>
        private const int TimeoutSeconds = 60;

        /// <summary>
        /// Pulls the tab the settings point at and checks it.
        /// When the sheet is not public, Google does not return an error — it returns <b>login HTML with a 200</b>.
        /// Letting that through overwrites the table with HTML and the pipeline then wrecks the assets, so it is stopped here.
        /// </summary>
        /// <param name="url">Address to pull from.</param>
        /// <returns>The result, normalization included.</returns>
        public static async Task<SheetFetch> FetchAsync(string url)
        {
            string body;
            try
            {
                body = await GetTextAsync(url);
            }
            catch (Exception e)
            {
                return SheetFetch.Failure($"Pull failed: {e.Message}");
            }

            if (SheetDiff.LooksLikeHtml(body))
            {
                return SheetFetch.Failure("HTML came back instead of CSV. That means there is no permission to access the sheet.", true);
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return SheetFetch.Failure("The sheet is empty. (Check whether the tab is empty)");
            }

            return SheetFetch.Success(SheetDiff.Normalize(body));
        }

        /// <summary>
        /// Pulls the body of a URL as a string.
        /// When a service account key is configured, it attaches an access token and reads <b>private sheets</b> too.
        /// </summary>
        /// <param name="url">Address to pull from.</param>
        /// <returns>The response body.</returns>
        public static async Task<string> GetTextAsync(string url)
        {
            string token = GoogleServiceAccount.IsConfigured
                ? await GoogleServiceAccount.GetAccessTokenAsync()
                : null;

            return await GetTextAsync(url, token);
        }

        /// <summary>Pulls the body of a URL. A token, when present, rides in the Authorization header.</summary>
        /// <param name="url">Address to pull from.</param>
        /// <param name="accessToken">The access token to use. Nothing is attached when null.</param>
        /// <returns>The response body.</returns>
        public static Task<string> GetTextAsync(string url, string accessToken)
        {
            var completion = new TaskCompletionSource<string>();
            UnityWebRequest request = UnityWebRequest.Get(url);

            // 시한을 걸지 않으면 응답이 오지 않는 동안 이 작업이 끝나지 않습니다. 그러면 동기화가
            // '도는 중'으로 남아 다음 받기가 통째로 막히고, 에디터를 다시 켜기 전에는 풀리지 않습니다.
            request.timeout = TimeoutSeconds;

            if (!string.IsNullOrEmpty(accessToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + accessToken);
            }

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        completion.SetException(new Exception($"{request.responseCode} {request.error}"));
                    }
                    else
                    {
                        completion.SetResult(request.downloadHandler.text);
                    }
                }
                finally
                {
                    request.Dispose();
                }
            };

            return completion.Task;
        }

        /// <summary>The hint to attach to an access failure.</summary>
        /// <returns>The hint text.</returns>
        public static string AccessDeniedHint()
        {
            return "  To use it as a public sheet: Share → Anyone with the link → Viewer\n"
                 + "  To keep it private: set a service account key in Project Settings ▸ CSV Pipeline,\n"
                 + "  and share the sheet with that account's email."
                 + (GoogleServiceAccount.IsConfigured
                     ? "\n  (The key is configured. Check who the sheet is shared with)"
                     : string.Empty);
        }
    }
}
