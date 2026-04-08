using TMPro;

namespace ACEDS_name_tags
{
    public static class LegacyFpsDisplayTaggingSnippet
    {
        public static void ApplyTagText(TextMeshPro tag, VRRig rig, int fps)
        {
            if (tag == null || rig == null)
            {
                return;
            }

            string playerName = string.IsNullOrWhiteSpace(rig.playerNameVisible) ? "Player" : rig.playerNameVisible;
            tag.text = $"{playerName}\n{fps} FPS";
        }
    }
}
