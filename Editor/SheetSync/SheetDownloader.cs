using System;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace CsvPipeline
{
    /// <summary>한 번의 받기 결과입니다. 성공이면 <see cref="Text"/>가, 아니면 <see cref="Error"/>가 찹니다.</summary>
    public readonly struct SheetFetch
    {
        /// <summary>받기와 검증을 모두 통과했는지 여부입니다.</summary>
        public bool Ok { get; }

        /// <summary>정규화된 표 원문입니다. 실패했으면 null입니다.</summary>
        public string Text { get; }

        /// <summary>실패 사유입니다. 성공했으면 null입니다.</summary>
        public string Error { get; }

        /// <summary>권한이 없어 로그인 페이지를 받은 경우인지 여부입니다.</summary>
        public bool IsAccessDenied { get; }

        /// <summary>결과를 만듭니다.</summary>
        /// <param name="ok">통과 여부입니다.</param>
        /// <param name="text">받은 원문입니다.</param>
        /// <param name="error">실패 사유입니다.</param>
        /// <param name="accessDenied">권한 문제인지 여부입니다.</param>
        private SheetFetch(bool ok, string text, string error, bool accessDenied)
        {
            Ok = ok;
            Text = text;
            Error = error;
            IsAccessDenied = accessDenied;
        }

        /// <summary>성공 결과를 만듭니다.</summary>
        /// <param name="text">정규화된 원문입니다.</param>
        /// <returns>성공 결과입니다.</returns>
        public static SheetFetch Success(string text) => new SheetFetch(true, text, null, false);

        /// <summary>실패 결과를 만듭니다.</summary>
        /// <param name="error">실패 사유입니다.</param>
        /// <param name="accessDenied">권한 문제인지 여부입니다.</param>
        /// <returns>실패 결과입니다.</returns>
        public static SheetFetch Failure(string error, bool accessDenied = false)
            => new SheetFetch(false, null, error, accessDenied);
    }

    /// <summary>
    /// 시트 본문을 받아 오고, <b>표가 맞는지까지 확인해</b> 돌려줍니다.
    /// 받기·인증·검증이 여기 모여 있어 부르는 쪽은 성공/실패만 다루면 됩니다.
    /// </summary>
    public static class SheetDownloader
    {
        /// <summary>
        /// 설정이 가리키는 탭을 받아 검증합니다.
        /// 시트가 공개돼 있지 않으면 구글은 오류가 아니라 <b>로그인 HTML을 200으로</b> 돌려줍니다.
        /// 이것을 통과시키면 표가 HTML로 덮이고 파이프라인이 에셋을 망가뜨리므로 여기서 막습니다.
        /// </summary>
        /// <param name="url">받아올 주소입니다.</param>
        /// <returns>정규화까지 마친 결과입니다.</returns>
        public static async Task<SheetFetch> FetchAsync(string url)
        {
            string body;
            try
            {
                body = await GetTextAsync(url);
            }
            catch (Exception e)
            {
                return SheetFetch.Failure($"받기 실패: {e.Message}");
            }

            if (SheetDiff.LooksLikeHtml(body))
            {
                return SheetFetch.Failure("CSV 대신 HTML이 왔습니다. 시트에 접근할 권한이 없다는 뜻입니다.", true);
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return SheetFetch.Failure("시트가 비어 있습니다. (탭이 비어 있는지 확인)");
            }

            return SheetFetch.Success(SheetDiff.Normalize(body));
        }

        /// <summary>
        /// URL의 본문을 문자열로 받아옵니다.
        /// 서비스 계정 키가 설정돼 있으면 액세스 토큰을 붙여 <b>비공개 시트</b>도 읽습니다.
        /// </summary>
        /// <param name="url">받아올 주소입니다.</param>
        /// <returns>응답 본문입니다.</returns>
        public static async Task<string> GetTextAsync(string url)
        {
            string token = GoogleServiceAccount.IsConfigured
                ? await GoogleServiceAccount.GetAccessTokenAsync()
                : null;

            return await GetTextAsync(url, token);
        }

        /// <summary>URL의 본문을 받아옵니다. 토큰이 있으면 Authorization 헤더에 실습니다.</summary>
        /// <param name="url">받아올 주소입니다.</param>
        /// <param name="accessToken">쓸 액세스 토큰입니다. null이면 붙이지 않습니다.</param>
        /// <returns>응답 본문입니다.</returns>
        public static Task<string> GetTextAsync(string url, string accessToken)
        {
            var completion = new TaskCompletionSource<string>();
            UnityWebRequest request = UnityWebRequest.Get(url);

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

        /// <summary>권한 실패에 붙일 안내 문구입니다.</summary>
        /// <returns>안내 문구입니다.</returns>
        public static string AccessDeniedHint()
        {
            return "  공개 시트로 쓰려면: 공유 → 링크가 있는 모든 사용자 → 뷰어\n"
                 + "  비공개로 두려면: Project Settings ▸ CSV Pipeline 에 서비스 계정 키를 지정하고,\n"
                 + "  그 계정 이메일에게 시트를 공유하십시오."
                 + (GoogleServiceAccount.IsConfigured
                     ? "\n  (키는 설정돼 있습니다. 시트 공유 대상을 확인하십시오)"
                     : string.Empty);
        }
    }
}
