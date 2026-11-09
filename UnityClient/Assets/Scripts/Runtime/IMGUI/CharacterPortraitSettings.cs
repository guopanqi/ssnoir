using UnityEngine;
namespace SSNoir.IMGUI
{
    [CreateAssetMenu(menuName = "SSNoir/人物视觉参数")]
    public sealed class CharacterPortraitSettings : ScriptableObject
    {
        [Range(0.2f, 1.4f)] public float Brightness = 1f;
        [Range(0.5f, 1.6f)] public float Contrast = 1f;
        [Range(0f, 1.6f)] public float Saturation = 1f;
        [Range(-1f, 1f)] public float Temperature;
        [Range(0f, 0.5f)] public float BottomFade;
        public void ResetLook() { Brightness = Contrast = Saturation = 1f; Temperature = BottomFade = 0f; }
    }

}
