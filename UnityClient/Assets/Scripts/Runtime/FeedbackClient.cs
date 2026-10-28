#nullable enable
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace SSNoir
{
    /// <summary>一次提交一个稳定 ID；失败重试沿用同一份载荷，不在客户端保存服务端凭据。</summary>
    public sealed class FeedbackClient : IDisposable
    {
        public const int MaxTextLength = 2000;

        [Serializable]
        public sealed class Submission
        {
            public string id = Guid.NewGuid().ToString("N");
            public string text = string.Empty;
            public string version = string.Empty;
            public string location = string.Empty;
            public bool inScenario;
            public int worldDay;
        }

        [Serializable]
        private sealed class Configuration { public string endpoint = string.Empty; }
        [Serializable]
        private sealed class Receipt { public bool ok = false; public string id = string.Empty; }

        private readonly string _endpoint;
        private UnityWebRequest? _request;
        public bool IsSubmitting { get; private set; }

        public FeedbackClient()
        {
            var asset = Resources.Load<TextAsset>("FeedbackConfig");
            if (asset == null) throw new InvalidOperationException("Missing Resources/FeedbackConfig.json");
            var config = JsonUtility.FromJson<Configuration>(asset.text);
            if (config == null || !Uri.TryCreate(config.endpoint, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("Feedback endpoint must be an absolute HTTPS URL");
            _endpoint = config.endpoint;
        }

        public IEnumerator Submit(Submission submission, Action<bool, long> completed)
        {
            if (IsSubmitting) throw new InvalidOperationException("Feedback submission already in progress");
            IsSubmitting = true;
            using var request = new UnityWebRequest(_endpoint, UnityWebRequest.kHttpVerbPOST);
            _request = request;
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(submission)));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 20;
            yield return request.SendWebRequest();

            bool saved = false;
            if (request.result == UnityWebRequest.Result.Success && request.responseCode == 200)
            {
                try
                {
                    var receipt = JsonUtility.FromJson<Receipt>(request.downloadHandler.text);
                    saved = receipt != null && receipt.ok && receipt.id == submission.id;
                }
                catch (ArgumentException) { /* 无有效入库回执，按失败处理并保留原文。 */ }
            }
            _request = null;
            IsSubmitting = false;
            completed(saved, request.responseCode);
        }

        public void Dispose()
        {
            _request?.Abort();
            _request?.Dispose();
            _request = null;
            IsSubmitting = false;
        }
    }
}
