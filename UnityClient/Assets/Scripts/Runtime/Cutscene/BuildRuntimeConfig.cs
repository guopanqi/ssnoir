#nullable enable
using System;
using UnityEngine;

namespace SSNoir
{
    /// <summary>由 staging 资源准备器生成的构建期交付配置；主工程没有配置时默认读取本地视频。</summary>
    internal static class BuildRuntimeConfig
    {
        [Serializable]
        private sealed class Data
        {
            public bool cutsceneVideosDisabled = false;
            public string cutsceneVideoBaseUrl = string.Empty;
        }

        private static Data? _data;

        public static bool CutsceneVideosDisabled => Load().cutsceneVideosDisabled;

        public static string ResolveCutsceneUrl(string fileName)
        {
            Data data = Load();
            if (string.IsNullOrEmpty(data.cutsceneVideoBaseUrl))
                return Application.streamingAssetsPath + "/Cutscenes/" + fileName;

            return data.cutsceneVideoBaseUrl.TrimEnd('/')
                + "/Cutscenes/"
                + Uri.EscapeDataString(fileName);
        }

        private static Data Load()
        {
            if (_data != null)
                return _data;

            TextAsset asset = Resources.Load<TextAsset>("BuildRuntimeConfig");
            if (asset == null)
                return _data = new Data();

            _data = JsonUtility.FromJson<Data>(asset.text)
                ?? throw new InvalidOperationException("BuildRuntimeConfig.json 格式无效。");
            if (_data.cutsceneVideosDisabled && !string.IsNullOrEmpty(_data.cutsceneVideoBaseUrl))
                throw new InvalidOperationException("构建配置不能同时禁用视频并声明远程视频地址。");
            return _data;
        }
    }
}
