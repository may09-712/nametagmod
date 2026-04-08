using System.Collections.Generic;
using BepInEx;
using TMPro;
using UnityEngine;

[BepInPlugin("com.example.gorillatag.nametagfps", "NameTag FPS/HZ Mod", "1.0.0")]
public class NameTagFpsMod : BaseUnityPlugin
{
    private readonly Dictionary<VRRig, TextMeshPro> cachedTags = new Dictionary<VRRig, TextMeshPro>();
    private float deltaTime;

    private void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        int fps = Mathf.RoundToInt(1f / deltaTime);
        int hz = Screen.currentResolution.refreshRate;

        if (GorillaParent.instance == null || GorillaParent.instance.vrrigs == null)
            return;

        foreach (VRRig rig in GorillaParent.instance.vrrigs)
        {
            if (rig == null)
                continue;

            TextMeshPro tag;
            if (!cachedTags.TryGetValue(rig, out tag) || tag == null)
            {
                tag = rig.GetComponentInChildren<TextMeshPro>(true);
                if (tag == null)
                    continue;

                cachedTags[rig] = tag;
            }

            string playerName = string.IsNullOrEmpty(rig.playerNameVisible) ? "Player" : rig.playerNameVisible;
            tag.text = string.Format("{0}\n{1} FPS | {2} Hz", playerName, fps, hz);
        }
    }
}
