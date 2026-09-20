namespace SSNoir
{
    /// <summary>只影响同一因果批内部的轻量 Beat 如何播放，不改变引擎结算顺序。</summary>
    public static class OpponentTurnSettings
    {
        public static bool Sequential { get; set; }
    }
}
