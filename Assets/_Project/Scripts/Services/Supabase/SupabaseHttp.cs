using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace LostPetNetwork.Services.Supabase
{
    public readonly struct HttpResponse
    {
        public readonly long StatusCode;
        public readonly string Body;
        public readonly bool NetworkError;

        public HttpResponse(long statusCode, string body, bool networkError)
        {
            StatusCode = statusCode;
            Body = body ?? string.Empty;
            NetworkError = networkError;
        }

        public bool IsSuccess => !NetworkError && StatusCode >= 200 && StatusCode < 300;
    }

    public readonly struct BytesResponse
    {
        public readonly long StatusCode;
        public readonly byte[] Data;
        public readonly bool NetworkError;

        public BytesResponse(long statusCode, byte[] data, bool networkError)
        {
            StatusCode = statusCode;
            Data = data;
            NetworkError = networkError;
        }

        public bool IsSuccess => !NetworkError && StatusCode >= 200 && StatusCode < 300;
    }

    [Serializable]
    internal class SupabaseErrorDto
    {
        public string code;
        public string error_code;
        public string msg;
        public string message;
        public string error;
        public string error_description;
        public string hint;
    }

    /// <summary>Thin UnityWebRequest wrapper that talks JSON to Supabase and never logs secrets.</summary>
    public static class SupabaseHttp
    {
        const int TimeoutSeconds = 15;

        public static Task<HttpResponse> SendAsync(string method, string url, string jsonBody = null,
            string accessToken = null, string preferHeader = null, bool singleObject = false, int timeoutSeconds = TimeoutSeconds)
        {
            var tcs = new TaskCompletionSource<HttpResponse>();

            var request = new UnityWebRequest(url, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = timeoutSeconds
            };
            if (jsonBody != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.SetRequestHeader("apikey", SupabaseConfig.PublishableKey);
            if (!string.IsNullOrEmpty(accessToken))
                request.SetRequestHeader("Authorization", "Bearer " + accessToken);
            if (!string.IsNullOrEmpty(preferHeader))
                request.SetRequestHeader("Prefer", preferHeader);
            if (singleObject)
                request.SetRequestHeader("Accept", "application/vnd.pgrst.object+json");

            var op = request.SendWebRequest();
            op.completed += _ =>
            {
                bool netError = request.result == UnityWebRequest.Result.ConnectionError
                                || request.result == UnityWebRequest.Result.DataProcessingError;
                tcs.TrySetResult(new HttpResponse(request.responseCode, request.downloadHandler?.text, netError));
                request.Dispose();
            };
            return tcs.Task;
        }

        /// <summary>Downloads a file (for example a report photo from Storage) with the user's access token.</summary>
        public static Task<BytesResponse> GetBytesAsync(string url, string accessToken)
        {
            var tcs = new TaskCompletionSource<BytesResponse>();
            var request = new UnityWebRequest(url, "GET") { downloadHandler = new DownloadHandlerBuffer(), timeout = TimeoutSeconds };
            request.SetRequestHeader("apikey", SupabaseConfig.PublishableKey);
            if (!string.IsNullOrEmpty(accessToken)) request.SetRequestHeader("Authorization", "Bearer " + accessToken);
            var op = request.SendWebRequest();
            op.completed += _ =>
            {
                bool netError = request.result == UnityWebRequest.Result.ConnectionError
                                || request.result == UnityWebRequest.Result.DataProcessingError;
                tcs.TrySetResult(new BytesResponse(request.responseCode, request.downloadHandler?.data, netError));
                request.Dispose();
            };
            return tcs.Task;
        }

        /// <summary>Uploads a file to Supabase Storage. Replaces an existing file at the same path.</summary>
        public static Task<HttpResponse> UploadBytesAsync(string url, byte[] data, string contentType, string accessToken)
        {
            var tcs = new TaskCompletionSource<HttpResponse>();
            var request = new UnityWebRequest(url, "POST")
            {
                uploadHandler = new UploadHandlerRaw(data),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 30
            };
            request.SetRequestHeader("Content-Type", contentType);
            request.SetRequestHeader("x-upsert", "true");
            request.SetRequestHeader("apikey", SupabaseConfig.PublishableKey);
            if (!string.IsNullOrEmpty(accessToken)) request.SetRequestHeader("Authorization", "Bearer " + accessToken);
            var op = request.SendWebRequest();
            op.completed += _ =>
            {
                bool netError = request.result == UnityWebRequest.Result.ConnectionError
                                || request.result == UnityWebRequest.Result.DataProcessingError;
                tcs.TrySetResult(new HttpResponse(request.responseCode, request.downloadHandler?.text, netError));
                request.Dispose();
            };
            return tcs.Task;
        }

        /// <summary>Turns a failed response into a short message that is safe to show to users.</summary>
        public static string FriendlyError(HttpResponse response)
        {
            if (response.NetworkError)
                return "Can't reach the server. Check your internet connection and try again.";

            SupabaseErrorDto dto = null;
            try { dto = JsonUtility.FromJson<SupabaseErrorDto>(response.Body); } catch { /* fall through */ }
            string code = dto?.error_code ?? dto?.code ?? string.Empty;

            switch (code)
            {
                case "invalid_credentials": return "Email or password is incorrect.";
                case "email_not_confirmed": return "Please confirm your email first. Check your inbox for the link.";
                case "user_already_exists":
                case "email_exists": return "An account with this email already exists. Try signing in instead.";
                case "weak_password": return "That password is too weak. Use at least 8 characters with a letter and a number.";
                case "validation_failed":
                case "email_address_invalid": return "Please enter a valid email address.";
                case "signup_disabled": return "New sign-ups are currently disabled.";
                case "over_email_send_rate_limit": return "Too many emails were requested. Please wait a few minutes and try again.";
                case "over_request_rate_limit": return "Too many attempts. Please wait a moment and try again.";
                case "otp_expired": return "That code is invalid or has expired. Request a new one.";
                case "same_password": return "Your new password must be different from the old one.";
                case "refresh_token_not_found":
                case "refresh_token_already_used":
                case "session_not_found":
                case "session_expired": return "Your session has expired. Please sign in again.";
            }

            if (response.StatusCode == 429) return "Too many attempts. Please wait a moment and try again.";
            if (response.StatusCode >= 500) return "The server had a problem. Please try again in a moment.";
            if (response.StatusCode == 401 || response.StatusCode == 403) return "Your session has expired. Please sign in again.";
            return "Something went wrong. Please try again.";
        }
    }
}
