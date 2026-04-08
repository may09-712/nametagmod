using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace ACEDS_name_tags
{
    [BepInPlugin("com.aceds.gorillatag.nametagfps", "ACEDS Gorilla Tag NameTag FPS", "1.0.0")]
    public sealed class NameTagFpsMod : BaseUnityPlugin
    {
        private const float TagScaleMultiplier = 10.0f;
        private const float TagFontSize = 20.0f;
        private static readonly FieldInfo RigFpsField = typeof(VRRig).GetField("fps", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly Dictionary<VRRig, TextMeshPro> activeTags = new Dictionary<VRRig, TextMeshPro>();
        private readonly HashSet<VRRig> seenRigs = new HashSet<VRRig>();
        private readonly List<VRRig> staleRigs = new List<VRRig>();
        private float nextStatusLogTime;

        private TMP_FontAsset sansFont;
        private float smoothedDeltaTime = 0.016f;

        private void Start()
        {
            sansFont = ResolveSansFont();
            Logger.LogInfo("ACEDS NameTag FPS mod loaded.");
        }

        private void Update()
        {
            smoothedDeltaTime += (Time.unscaledDeltaTime - smoothedDeltaTime) * 0.1f;
            int fps = smoothedDeltaTime > 0.0001f ? Mathf.RoundToInt(1f / smoothedDeltaTime) : 0;

            IReadOnlyList<VRRig> cachedRigs = VRRigCache.ActiveRigs;
            IEnumerable<VRRig> rigsToProcess = cachedRigs;
            if (cachedRigs == null || cachedRigs.Count == 0)
            {
                rigsToProcess = FindObjectsByType<VRRig>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }

            if (rigsToProcess == null)
            {
                CleanupAllTags();
                return;
            }

            seenRigs.Clear();

            foreach (VRRig rig in rigsToProcess)
            {
                if (rig == null)
                {
                    continue;
                }

                seenRigs.Add(rig);

                TextMeshPro tag;
                if (!activeTags.TryGetValue(rig, out tag) || tag == null)
                {
                    tag = CreateTagForRig(rig);
                    if (tag == null)
                    {
                        continue;
                    }

                    activeTags[rig] = tag;
                }

                UpdateTag(tag, rig, fps);
            }

            staleRigs.Clear();
            foreach (KeyValuePair<VRRig, TextMeshPro> pair in activeTags)
            {
                if (pair.Key == null || !seenRigs.Contains(pair.Key) || pair.Value == null)
                {
                    staleRigs.Add(pair.Key);
                }
            }

            foreach (VRRig staleRig in staleRigs)
            {
                TextMeshPro staleTag;
                if (activeTags.TryGetValue(staleRig, out staleTag) && staleTag != null)
                {
                    Destroy(staleTag.gameObject);
                }

                activeTags.Remove(staleRig);
            }

            if (Time.unscaledTime >= nextStatusLogTime)
            {
                Logger.LogInfo("ACEDS NameTag FPS active rigs: " + seenRigs.Count + ", tags: " + activeTags.Count);
                nextStatusLogTime = Time.unscaledTime + 15f;
            }
        }

        private void OnDestroy()
        {
            CleanupAllTags();
        }

        private TextMeshPro CreateTagForRig(VRRig rig)
        {
            TextMeshPro sourceTag = rig.playerText1 != null ? rig.playerText1 : rig.GetComponentInChildren<TextMeshPro>(true);
            TextMeshPro newTag;

            if (sourceTag != null)
            {
                newTag = Instantiate(sourceTag, rig.transform);
                newTag.transform.localScale = sourceTag.transform.localScale * TagScaleMultiplier;
            }
            else
            {
                GameObject tagObject = new GameObject("ACEDS_FpsNameTag");
                tagObject.transform.SetParent(rig.transform, false);
                tagObject.transform.localScale = Vector3.one * TagScaleMultiplier;

                newTag = tagObject.AddComponent<TextMeshPro>();
            }

            newTag.name = "ACEDS_FpsNameTag";
            newTag.gameObject.SetActive(true);
            newTag.enabled = true;

            MeshRenderer renderer = newTag.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.enabled = true;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            ConfigureTag(newTag);
            return newTag;
        }

        private void ConfigureTag(TextMeshPro tag)
        {
            if (tag == null)
            {
                return;
            }

            if (sansFont == null)
            {
                sansFont = ResolveSansFont();
            }

            if (sansFont != null)
            {
                tag.font = sansFont;
            }

            tag.color = Color.red;
            tag.alignment = TextAlignmentOptions.Center;
            tag.textWrappingMode = TextWrappingModes.NoWrap;
            tag.richText = false;
            tag.fontStyle = FontStyles.Normal;
            tag.outlineWidth = 0f;
            tag.enableAutoSizing = false;
            tag.fontSize = TagFontSize;
        }

        private void UpdateTag(TextMeshPro tag, VRRig rig, int localFps)
        {
            if (tag == null || rig == null)
            {
                return;
            }

            string playerName = string.IsNullOrWhiteSpace(rig.playerNameVisible) ? "PLAYER" : rig.playerNameVisible;
            int displayedFps = GetRigFps(rig, localFps);
            tag.text = playerName + "\n" + displayedFps + " FPS";
            tag.color = Color.red;
            tag.alpha = 1f;
            tag.gameObject.SetActive(true);
            tag.enabled = true;

            if (sansFont != null && tag.font != sansFont)
            {
                tag.font = sansFont;
            }

            Transform headTransform = rig.headConstraint != null
                ? rig.headConstraint
                : (rig.head != null ? rig.head.rigTarget : null);

            if (headTransform != null)
            {
                tag.transform.position = headTransform.position + new Vector3(0f, 0.35f, 0f);
            }
            else
            {
                tag.transform.position = rig.transform.position + new Vector3(0f, 1.8f, 0f);
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                tag.transform.LookAt(mainCamera.transform);
                tag.transform.Rotate(0f, 180f, 0f);
            }
        }

        private static int GetRigFps(VRRig rig, int localFallbackFps)
        {
            if (rig == null)
            {
                return 0;
            }

            if (RigFpsField != null)
            {
                object value = RigFpsField.GetValue(rig);
                if (value is int networkFps && networkFps > 0)
                {
                    return networkFps;
                }
            }

            if (rig.isMyPlayer)
            {
                return localFallbackFps;
            }

            return 0;
        }

        private void CleanupAllTags()
        {
            foreach (KeyValuePair<VRRig, TextMeshPro> pair in activeTags)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }
            }

            activeTags.Clear();
            staleRigs.Clear();
            seenRigs.Clear();
        }

        private static TMP_FontAsset ResolveSansFont()
        {
            TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (TMP_FontAsset font in fonts)
            {
                if (font == null || string.IsNullOrEmpty(font.name))
                {
                    continue;
                }

                string fontName = font.name.ToLowerInvariant();
                if (fontName.Contains("sans") || fontName.Contains("arial"))
                {
                    return font;
                }
            }

            return TMP_Settings.defaultFontAsset;
        }
    }
}
