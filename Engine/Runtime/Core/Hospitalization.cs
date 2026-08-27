#nullable enable

namespace SSNoir.Core
{
    /// <summary>一次已经完成数值结算、等待场景层送到诊所的倒下记录。</summary>
    public sealed class Hospitalization
    {
        public string Text { get; }

        public Hospitalization(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new System.ArgumentException("hospitalization text cannot be empty", nameof(text));
            Text = text;
        }
    }
}
