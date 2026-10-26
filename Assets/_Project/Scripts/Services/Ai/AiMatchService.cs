using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Supabase;
using UnityEngine;

namespace LostPetNetwork.Services.Ai
{
    public class AiMatch
    {
        public string ReportId;
        public int Score;
        public string Reason;
    }

    public class AiMatchResult
    {
        public bool Ok;
        public List<AiMatch> Matches = new List<AiMatch>();
        /// <summary>Short code from the server: not_configured, rate_limited, no_photo, not_found, upstream...</summary>
        public string ErrorCode;
        public string Message;
        public bool NotConfigured => ErrorCode == "not_configured";
    }

    [Serializable]
    class MatchItemDto
    {
        public string id;
        public int score;
        public string reason;
    }

    [Serializable]
    class MatchResponseDto
    {
        public MatchItemDto[] matches;
        public string error;
        public string message;
    }

    /// <summary>
    /// Asks the "match-pets" Supabase Edge Function which found (or lost) reports may be the same animal.
    /// The Gemini key never reaches the app: the function holds it and the app only sends the report id.
    /// </summary>
    public static class AiMatchService
    {
        public const string FunctionUrl = SupabaseConfig.Url + "/functions/v1/match-pets";

        public static async Task<AiMatchResult> FindAsync(string reportId)
        {
            string token = await AuthService.Instance.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token)) return Fail("unauthorized", "Please sign in again.");

            string body = new JsonBody().Str("report_id", reportId).ToString();
            var resp = await SupabaseHttp.SendAsync("POST", FunctionUrl, body, token, timeoutSeconds: 120);
            return Parse(resp.StatusCode, resp.Body, resp.NetworkError);
        }

        /// <summary>Turns the function's answer into a result (pure logic, covered by tests).</summary>
        public static AiMatchResult Parse(long status, string body, bool networkError)
        {
            if (networkError) return Fail("network", "Can't reach the server. Check your internet connection and try again.");

            MatchResponseDto dto = null;
            try { dto = JsonUtility.FromJson<MatchResponseDto>(body); }
            catch (ArgumentException) { /* handled below */ }

            if (status >= 200 && status < 300 && dto != null)
            {
                var result = new AiMatchResult { Ok = true };
                if (dto.matches != null)
                    foreach (var m in dto.matches)
                        if (!string.IsNullOrEmpty(m.id))
                            result.Matches.Add(new AiMatch { ReportId = m.id, Score = Mathf.Clamp(m.score, 0, 100), Reason = m.reason });
                result.Matches.Sort((a, b) => b.Score.CompareTo(a.Score));
                return result;
            }

            string code = !string.IsNullOrEmpty(dto?.error) ? dto.error : status == 404 ? "not_configured" : "upstream";
            string message = !string.IsNullOrWhiteSpace(dto?.message) ? dto.message : Describe(code, status);
            return Fail(code, message);
        }

        static string Describe(string code, long status)
        {
            switch (code)
            {
                case "not_configured": return "AI matching isn't set up yet.";
                case "rate_limited": return "You've used the AI helper a lot. Try again in a little while.";
                case "unauthorized": return "Please sign in again.";
                default: return status >= 500 ? "The AI helper had a problem. Please try again." : "Something went wrong. Please try again.";
            }
        }

        static AiMatchResult Fail(string code, string message) => new AiMatchResult { Ok = false, ErrorCode = code, Message = message };
    }
}
