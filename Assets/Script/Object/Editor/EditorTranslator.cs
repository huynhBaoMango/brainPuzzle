using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

/// <summary>
/// Editor-only async translation helper. Hits the public Google Translate
/// <c>gtx</c> endpoint (same backend as translate.google.com, no API key
/// required). Intended for the Level Config window to auto-fill the EN
/// column from VN authoring.
///
/// Usage:
/// <code>
/// EditorTranslator.TranslateAsync("Thử lại", "vi", "en", (result, err) => {
///     if (err != null) Debug.LogWarning(err);
///     else entry.en = result;
/// });
/// </code>
///
/// <para>
/// If Google ever blocks the public endpoint, swap <see cref="BuildUrl"/>
/// to point at the Cloud Translation API and add a key header.
/// </para>
/// </summary>
public static class EditorTranslator
{
    private const string Endpoint =
        "https://translate.googleapis.com/translate_a/single";

    private class PendingReq
    {
        public UnityWebRequest www;
        public Action<string, string> onDone;
        public string originalText;
    }

    private static readonly List<PendingReq> pending = new List<PendingReq>();
    private static bool updateHooked;

    /// <summary>
    /// Fire-and-forget translation. <paramref name="onDone"/> is invoked on
    /// the main thread with either (translatedText, null) on success or
    /// (null, errorMessage) on failure.
    /// </summary>
    public static void TranslateAsync(string text, string from, string to,
                                      Action<string, string> onDone)
    {
        if (string.IsNullOrEmpty(text))
        {
            onDone?.Invoke(string.Empty, null);
            return;
        }

        UnityWebRequest www = UnityWebRequest.Get(BuildUrl(text, from, to));
        // Google returns 403 on the default Unity UA; any common UA works.
        www.SetRequestHeader("User-Agent",
            "Mozilla/5.0 (compatible; UnityEditor)");
        www.timeout = 15;
        www.SendWebRequest();

        pending.Add(new PendingReq
        {
            www = www,
            onDone = onDone,
            originalText = text,
        });

        if (!updateHooked)
        {
            EditorApplication.update += Poll;
            updateHooked = true;
        }
    }

    private static string BuildUrl(string text, string from, string to)
    {
        string q = UnityWebRequest.EscapeURL(text);
        return $"{Endpoint}?client=gtx&sl={from}&tl={to}&dt=t&q={q}";
    }

    private static void Poll()
    {
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            PendingReq req = pending[i];
            if (!req.www.isDone) continue;

            try
            {
#if UNITY_2020_1_OR_NEWER
                bool ok = req.www.result == UnityWebRequest.Result.Success;
#else
                bool ok = !req.www.isHttpError && !req.www.isNetworkError;
#endif
                if (!ok)
                {
                    req.onDone?.Invoke(null,
                        $"Translate failed: {req.www.error} " +
                        $"(http {req.www.responseCode})");
                }
                else
                {
                    string body = req.www.downloadHandler.text;
                    string translated = ExtractTranslation(body);
                    if (translated == null)
                        req.onDone?.Invoke(null,
                            "Translate failed: could not parse response " +
                            "(Google schema may have changed).");
                    else
                        req.onDone?.Invoke(translated, null);
                }
            }
            catch (Exception e)
            {
                req.onDone?.Invoke(null, $"Translate exception: {e.Message}");
            }
            finally
            {
                req.www.Dispose();
                pending.RemoveAt(i);
            }
        }

        if (pending.Count == 0 && updateHooked)
        {
            EditorApplication.update -= Poll;
            updateHooked = false;
        }
    }

    /// <summary>
    /// Response is a nested array; sentences live in root[0] as an array of
    /// [translated, original, ...] tuples. We concatenate translated pieces
    /// so long inputs split across multiple segments come back whole.
    /// </summary>
    private static string ExtractTranslation(string body)
    {
        JArray root = JArray.Parse(body);
        if (root.Count == 0 || root[0].Type != JTokenType.Array) return null;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (JToken seg in (JArray)root[0])
        {
            if (seg.Type != JTokenType.Array) continue;
            JArray a = (JArray)seg;
            if (a.Count == 0) continue;
            sb.Append(a[0]?.ToString());
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

    /// <summary>Number of in-flight translation requests.</summary>
    public static int PendingCount => pending.Count;
}
