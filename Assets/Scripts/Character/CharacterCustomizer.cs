// Character piece selector for Polygon Fantasy Hero Characters.
// Use this instead of CharacterRandomizer when you want to pick specific pieces rather than randomize.
// Requires the same hierarchy as CharacterRandomizer (Male_Head_All_Elements, All_01_Hair, etc.).

using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DungeonGame.Character
{
    public class CharacterCustomizer : MonoBehaviour
    {
        public enum HeadwearTab
        {
            HairStyle,
            Hat,              // HeadCoverings_Base_Hair (compatible with hair)
            Mask,             // HeadCoverings_No_FacialHair (not compatible with facial hair)
            Helmet,           // HeadCoverings_No_Hair (not compatible with hair)
            HelmetAttachment, // All_02_Head_Attachment (goes with helmets)
            HelmetHead        // Male/Female_Head_No_Elements (helmet heads, not compatible with anything else)
        }

        [Header("Gender")]
        [Tooltip("Determines which body/head pieces are used (Male or Female).")]
        public PsychoticLab.Gender gender = PsychoticLab.Gender.Male;

        [Header("Head (with facial elements)")]
        [Tooltip("Use head with eyebrows/facial hair slots. If false, uses headNoElements instead.")]
        public bool useHeadWithElements = true;

        [Tooltip("Index into headAllElements or headNoElements. 0 = first option.")]
        public int headIndex;

        [Tooltip("Index into eyebrows. 0 = first.")]
        public int eyebrowIndex;

        [Tooltip("Index into facial hair. 0 = typically no beard. Male only.")]
        public int facialHairIndex;

        [Header("Headwear (Tabs)")]
        [Tooltip("Which headwear tab is active. Keeps hair/helmet/hat indices separate and predictable.")]
        public HeadwearTab headwearTab = HeadwearTab.HairStyle;

        [Tooltip("Toggle facial hair on/off (male). When off, uses No_FacialHair hair list.")]
        public bool facialHairEnabled = true;

        [Tooltip("Hair style index (All_01_Hair). -1 = none.")]
        public int hairStyleIndex = 0;

        [Tooltip("Hat index (HeadCoverings_Base_Hair). -1 = none.")]
        public int hatIndex = -1;

        [Tooltip("Mask index (HeadCoverings_No_FacialHair). -1 = none.")]
        public int maskIndex = -1;

        [Tooltip("Helmet index (HeadCoverings_No_Hair). -1 = none.")]
        public int helmetIndex = -1;

        [Tooltip("Helmet attachment index (All_02_Head_Attachment). -1 = none.")]
        public int helmetAttachmentIndex = -1;

        [Tooltip("Helmet-head index (Male/Female_Head_No_Elements). -1 = none.")]
        public int helmetHeadIndex = -1;

        [Tooltip("Index into elf ear (used when helmet tab is active and race = Elf).")]
        public int elfEarIndex;

        [Header("Race")]
        public PsychoticLab.Race race = PsychoticLab.Race.Human;

        [Header("Body")]
        public int torsoIndex = 1;
        public int armUpperRightIndex;
        public int armUpperLeftIndex;
        public int armLowerRightIndex;
        public int armLowerLeftIndex;
        public int handRightIndex;
        public int handLeftIndex;
        public int hipsIndex = 1;
        public int legRightIndex;
        public int legLeftIndex;

        [Header("Attachments")]
        public int chestAttachmentIndex = -1;
        public int backAttachmentIndex = -1;
        public int shoulderRightIndex = -1;
        public int shoulderLeftIndex = -1;
        public int elbowRightIndex = -1;
        public int elbowLeftIndex = -1;
        public int hipsAttachmentIndex = -1;
        public int kneeRightIndex = -1;
        public int kneeLeftIndex = -1;

        [Header("Colors (optional)")]
        [Tooltip("Leave null to keep existing material colors.")]
        public Material materialOverride;

        public Color primaryColor = new Color(0.2862745f, 0.4f, 0.4941177f);
        public Color secondaryColor = new Color(0.7019608f, 0.6235294f, 0.4666667f);
        public Color skinColor = new Color(1f, 0.8000001f, 0.682353f);
        public Color hairColor = new Color(0.3098039f, 0.254902f, 0.1764706f);
        [Tooltip("Apply colors from above to the character material.")]
        public bool applyColors;

        [Header("Material")]
        [Tooltip("Assign if colors need applying. Auto-found from SkinnedMeshRenderer if null.")]
        public Material mat;

        List<GameObject> enabledObjects = new List<GameObject>();
        PsychoticLab.CharacterObjectGroups male;
        PsychoticLab.CharacterObjectGroups female;
        PsychoticLab.CharacterObjectListsAllGender allGender;

        void Start()
        {
            BuildLists();

            if (male == null || male.headAllElements == null || male.headAllElements.Count == 0)
            {
                Debug.LogWarning($"CharacterCustomizer: No Polygon Fantasy character structure found on {gameObject.name}. Disabling.");
                enabled = false;
                return;
            }

            ApplySelection();
        }

        /// <summary>Call to refresh the character with current selection. Works at runtime.</summary>
        public void ApplySelection()
        {
            if (enabledObjects.Count > 0)
            {
                foreach (var go in enabledObjects)
                {
                    if (go != null) go.SetActive(false);
                }
                enabledObjects.Clear();
            }

            var cog = gender == PsychoticLab.Gender.Male ? male : female;
            // Masks are not compatible with facial hair.
            bool hasFacialHair = gender == PsychoticLab.Gender.Male && facialHairEnabled && headwearTab != HeadwearTab.Mask;

            // Helmet heads are special: they replace the whole head and are not compatible with hair/facial hair/other headwear.
            if (headwearTab == HeadwearTab.HelmetHead)
            {
                ActivateSafe(cog.headNoElements, helmetHeadIndex >= 0 ? helmetHeadIndex : headIndex);
            }
            else
            {
                if (useHeadWithElements)
                {
                    ActivateSafe(cog.headAllElements, headIndex);
                    ActivateSafe(cog.eyebrow, eyebrowIndex);
                    if (hasFacialHair)
                        ActivateSafe(cog.facialHair, facialHairIndex);
                }
                else
                {
                    ActivateSafe(cog.headNoElements, headIndex);
                }

                ApplyHeadwear();
            }

            ActivateSafe(cog.torso, torsoIndex);
            ActivateSafe(cog.arm_Upper_Right, armUpperRightIndex);
            ActivateSafe(cog.arm_Upper_Left, armUpperLeftIndex);
            ActivateSafe(cog.arm_Lower_Right, armLowerRightIndex);
            ActivateSafe(cog.arm_Lower_Left, armLowerLeftIndex);
            ActivateSafe(cog.hand_Right, handRightIndex);
            ActivateSafe(cog.hand_Left, handLeftIndex);
            ActivateSafe(cog.hips, hipsIndex);
            ActivateSafe(cog.leg_Right, legRightIndex);
            ActivateSafe(cog.leg_Left, legLeftIndex);

            ActivateSafe(allGender.chest_Attachment, chestAttachmentIndex);
            ActivateSafe(allGender.back_Attachment, backAttachmentIndex);
            ActivateSafe(allGender.shoulder_Attachment_Right, shoulderRightIndex);
            ActivateSafe(allGender.shoulder_Attachment_Left, shoulderLeftIndex);
            ActivateSafe(allGender.elbow_Attachment_Right, elbowRightIndex);
            ActivateSafe(allGender.elbow_Attachment_Left, elbowLeftIndex);
            ActivateSafe(allGender.hips_Attachment, hipsAttachmentIndex);
            ActivateSafe(allGender.knee_Attachement_Right, kneeRightIndex);
            ActivateSafe(allGender.knee_Attachement_Left, kneeLeftIndex);

            if (applyColors)
                ApplyMaterialColors();
        }

        private void ApplyHeadwear()
        {
            // Asset semantics (per user):
            // - All_01_Hair: hairstyles
            // - HeadCoverings_Base_Hair: hats (compatible with hair)
            // - HeadCoverings_No_FacialHair: masks (not compatible with facial hair)
            // - HeadCoverings_No_Hair: helmets (not compatible with hair)
            // - All_02_Head_Attachment: helmet attachments (go with helmets)

            switch (headwearTab)
            {
                case HeadwearTab.HairStyle:
                    ActivateSafe(allGender.all_Hair, hairStyleIndex);
                    break;

                case HeadwearTab.Helmet:
                    ActivateSafe(allGender.headCoverings_No_Hair, helmetIndex);
                    if (race == PsychoticLab.Race.Elf)
                        ActivateSafe(allGender.elf_Ear, elfEarIndex);
                    break;

                case HeadwearTab.Hat:
                    ActivateSafe(allGender.all_Hair, hairStyleIndex);
                    ActivateSafe(allGender.headCoverings_Base_Hair, hatIndex);
                    break;

                case HeadwearTab.Mask:
                    ActivateSafe(allGender.all_Hair, hairStyleIndex);
                    ActivateSafe(allGender.headCoverings_No_FacialHair, maskIndex);
                    break;

                case HeadwearTab.HelmetAttachment:
                    ActivateSafe(allGender.headCoverings_No_Hair, helmetIndex);
                    if (race == PsychoticLab.Race.Elf)
                        ActivateSafe(allGender.elf_Ear, elfEarIndex);
                    ActivateSafe(allGender.all_Head_Attachment, helmetAttachmentIndex);
                    break;

                case HeadwearTab.HelmetHead:
                    // handled earlier
                    break;
            }
        }

        void ActivateSafe(List<GameObject> list, int index)
        {
            if (list == null || list.Count == 0) return;
            if (index < 0) return;
            int i = Mathf.Clamp(index, 0, list.Count - 1);
            var go = list[i];
            if (go != null)
            {
                go.SetActive(true);
                enabledObjects.Add(go);
                if (!mat && go.TryGetComponent<SkinnedMeshRenderer>(out var smr) && smr.sharedMaterial != null)
                    mat = smr.sharedMaterial;
            }
        }

        void ApplyMaterialColors()
        {
            var m = materialOverride != null ? materialOverride : mat;
            if (m == null) return;
            if (m.HasProperty("_Color_Primary")) m.SetColor("_Color_Primary", primaryColor);
            if (m.HasProperty("_Color_Secondary")) m.SetColor("_Color_Secondary", secondaryColor);
            if (m.HasProperty("_Color_Skin")) m.SetColor("_Color_Skin", skinColor);
            if (m.HasProperty("_Color_Hair")) m.SetColor("_Color_Hair", hairColor);
        }

        void BuildLists()
        {
            male = new PsychoticLab.CharacterObjectGroups();
            female = new PsychoticLab.CharacterObjectGroups();
            allGender = new PsychoticLab.CharacterObjectListsAllGender();
            InitGroups(male);
            InitGroups(female);
            InitAllGender(allGender);

            BuildList(male.headAllElements, "Male_Head_All_Elements");
            BuildList(male.headNoElements, "Male_Head_No_Elements");
            BuildList(male.eyebrow, "Male_01_Eyebrows");
            BuildList(male.facialHair, "Male_02_FacialHair");
            BuildList(male.torso, "Male_03_Torso");
            BuildList(male.arm_Upper_Right, "Male_04_Arm_Upper_Right");
            BuildList(male.arm_Upper_Left, "Male_05_Arm_Upper_Left");
            BuildList(male.arm_Lower_Right, "Male_06_Arm_Lower_Right");
            BuildList(male.arm_Lower_Left, "Male_07_Arm_Lower_Left");
            BuildList(male.hand_Right, "Male_08_Hand_Right");
            BuildList(male.hand_Left, "Male_09_Hand_Left");
            BuildList(male.hips, "Male_10_Hips");
            BuildList(male.leg_Right, "Male_11_Leg_Right");
            BuildList(male.leg_Left, "Male_12_Leg_Left");

            BuildList(female.headAllElements, "Female_Head_All_Elements");
            BuildList(female.headNoElements, "Female_Head_No_Elements");
            BuildList(female.eyebrow, "Female_01_Eyebrows");
            BuildList(female.facialHair, "Female_02_FacialHair");
            BuildList(female.torso, "Female_03_Torso");
            BuildList(female.arm_Upper_Right, "Female_04_Arm_Upper_Right");
            BuildList(female.arm_Upper_Left, "Female_05_Arm_Upper_Left");
            BuildList(female.arm_Lower_Right, "Female_06_Arm_Lower_Right");
            BuildList(female.arm_Lower_Left, "Female_07_Arm_Lower_Left");
            BuildList(female.hand_Right, "Female_08_Hand_Right");
            BuildList(female.hand_Left, "Female_09_Hand_Left");
            BuildList(female.hips, "Female_10_Hips");
            BuildList(female.leg_Right, "Female_11_Leg_Right");
            BuildList(female.leg_Left, "Female_12_Leg_Left");

            BuildList(allGender.all_Hair, "All_01_Hair");
            BuildList(allGender.headCoverings_Base_Hair, "HeadCoverings_Base_Hair");
            BuildList(allGender.headCoverings_No_FacialHair, "HeadCoverings_No_FacialHair");
            BuildList(allGender.headCoverings_No_Hair, "HeadCoverings_No_Hair");
            BuildList(allGender.all_Head_Attachment, "All_02_Head_Attachment");
            BuildList(allGender.chest_Attachment, "All_03_Chest_Attachment");
            BuildList(allGender.back_Attachment, "All_04_Back_Attachment");
            BuildList(allGender.shoulder_Attachment_Right, "All_05_Shoulder_Attachment_Right");
            BuildList(allGender.shoulder_Attachment_Left, "All_06_Shoulder_Attachment_Left");
            BuildList(allGender.elbow_Attachment_Right, "All_07_Elbow_Attachment_Right");
            BuildList(allGender.elbow_Attachment_Left, "All_08_Elbow_Attachment_Left");
            BuildList(allGender.hips_Attachment, "All_09_Hips_Attachment");
            BuildList(allGender.knee_Attachement_Right, "All_10_Knee_Attachement_Right");
            BuildList(allGender.knee_Attachement_Left, "All_11_Knee_Attachement_Left");
            BuildList(allGender.elf_Ear, "Elf_Ear");
        }

        static void InitGroups(PsychoticLab.CharacterObjectGroups g)
        {
            g.headAllElements ??= new List<GameObject>();
            g.headNoElements ??= new List<GameObject>();
            g.eyebrow ??= new List<GameObject>();
            g.facialHair ??= new List<GameObject>();
            g.torso ??= new List<GameObject>();
            g.arm_Upper_Right ??= new List<GameObject>();
            g.arm_Upper_Left ??= new List<GameObject>();
            g.arm_Lower_Right ??= new List<GameObject>();
            g.arm_Lower_Left ??= new List<GameObject>();
            g.hand_Right ??= new List<GameObject>();
            g.hand_Left ??= new List<GameObject>();
            g.hips ??= new List<GameObject>();
            g.leg_Right ??= new List<GameObject>();
            g.leg_Left ??= new List<GameObject>();
        }

        static void InitAllGender(PsychoticLab.CharacterObjectListsAllGender a)
        {
            a.headCoverings_Base_Hair ??= new List<GameObject>();
            a.headCoverings_No_FacialHair ??= new List<GameObject>();
            a.headCoverings_No_Hair ??= new List<GameObject>();
            a.all_Hair ??= new List<GameObject>();
            a.all_Head_Attachment ??= new List<GameObject>();
            a.chest_Attachment ??= new List<GameObject>();
            a.back_Attachment ??= new List<GameObject>();
            a.shoulder_Attachment_Right ??= new List<GameObject>();
            a.shoulder_Attachment_Left ??= new List<GameObject>();
            a.elbow_Attachment_Right ??= new List<GameObject>();
            a.elbow_Attachment_Left ??= new List<GameObject>();
            a.hips_Attachment ??= new List<GameObject>();
            a.knee_Attachement_Right ??= new List<GameObject>();
            a.knee_Attachement_Left ??= new List<GameObject>();
            a.elf_Ear ??= new List<GameObject>();
        }

        void BuildList(List<GameObject> targetList, string characterPart)
        {
            if (targetList == null) return;
            targetList.Clear();

            foreach (Transform t in gameObject.GetComponentsInChildren<Transform>())
            {
                if (t.gameObject.name == characterPart)
                {
                    for (int i = 0; i < t.childCount; i++)
                    {
                        var go = t.GetChild(i).gameObject;
                        // IMPORTANT: Building lists should not change runtime visibility.
                        // Visibility is controlled by ApplySelection(). During play, the inspector may
                        // rebuild lists when selecting the prefab/instance, which would otherwise
                        // deactivate the whole character.
                        if (!Application.isPlaying)
                            go.SetActive(false);
                        targetList.Add(go);
                    }
                    return;
                }
            }
        }

        #if UNITY_EDITOR
        // Editor-only helpers so the inspector can show proper sliders (-1..count-1)
        // without relying on reflection or duplicating the hierarchy scan logic.
        public void EditorRebuildLists()
        {
            BuildLists();
        }

        public int EditorGetCountHairStyles()
        {
            BuildLists();
            return allGender?.all_Hair?.Count ?? 0;
        }

        public int EditorGetCountHats()
        {
            BuildLists();
            return allGender?.headCoverings_Base_Hair?.Count ?? 0;
        }

        public int EditorGetCountMasks()
        {
            BuildLists();
            return allGender?.headCoverings_No_FacialHair?.Count ?? 0;
        }

        public int EditorGetCountHelmets()
        {
            BuildLists();
            return allGender?.headCoverings_No_Hair?.Count ?? 0;
        }

        public int EditorGetCountHelmetAttachments()
        {
            BuildLists();
            return allGender?.all_Head_Attachment?.Count ?? 0;
        }

        public int EditorGetCountElfEars()
        {
            BuildLists();
            return allGender?.elf_Ear?.Count ?? 0;
        }

        public int EditorGetCountHeadsAllElements()
        {
            BuildLists();
            var cog = gender == PsychoticLab.Gender.Male ? male : female;
            return cog?.headAllElements?.Count ?? 0;
        }

        public int EditorGetCountHeadsNoElements()
        {
            BuildLists();
            var cog = gender == PsychoticLab.Gender.Male ? male : female;
            return cog?.headNoElements?.Count ?? 0;
        }

        public int EditorGetCountEyebrows()
        {
            BuildLists();
            var cog = gender == PsychoticLab.Gender.Male ? male : female;
            return cog?.eyebrow?.Count ?? 0;
        }

        public int EditorGetCountFacialHair()
        {
            BuildLists();
            var cog = gender == PsychoticLab.Gender.Male ? male : female;
            return cog?.facialHair?.Count ?? 0;
        }

        public int EditorGetCountTorso()
        {
            BuildLists();
            var cog = gender == PsychoticLab.Gender.Male ? male : female;
            return cog?.torso?.Count ?? 0;
        }

        public int EditorGetCountArmUpperRight()
        {
            BuildLists();
            var cog = gender == PsychoticLab.Gender.Male ? male : female;
            return cog?.arm_Upper_Right?.Count ?? 0;
        }

        public int EditorGetCountArmUpperLeft()
        {
            BuildLists();
            var cog = gender == PsychoticLab.Gender.Male ? male : female;
            return cog?.arm_Upper_Left?.Count ?? 0;
        }

        public int EditorGetCountArmLowerRight()
        {
            BuildLists();
            var cog = gender == PsychoticLab.Gender.Male ? male : female;
            return cog?.arm_Lower_Right?.Count ?? 0;
        }

        public int EditorGetCountArmLowerLeft()
        {
            BuildLists();
            var cog = gender == PsychoticLab.Gender.Male ? male : female;
            return cog?.arm_Lower_Left?.Count ?? 0;
        }

        public int EditorGetCountHandRight()
        {
            BuildLists();
            var cog = gender == PsychoticLab.Gender.Male ? male : female;
            return cog?.hand_Right?.Count ?? 0;
        }

        public int EditorGetCountHandLeft()
        {
            BuildLists();
            var cog = gender == PsychoticLab.Gender.Male ? male : female;
            return cog?.hand_Left?.Count ?? 0;
        }

        public int EditorGetCountHips()
        {
            BuildLists();
            var cog = gender == PsychoticLab.Gender.Male ? male : female;
            return cog?.hips?.Count ?? 0;
        }

        public int EditorGetCountLegRight()
        {
            BuildLists();
            var cog = gender == PsychoticLab.Gender.Male ? male : female;
            return cog?.leg_Right?.Count ?? 0;
        }

        public int EditorGetCountLegLeft()
        {
            BuildLists();
            var cog = gender == PsychoticLab.Gender.Male ? male : female;
            return cog?.leg_Left?.Count ?? 0;
        }

        public int EditorGetCountChestAttachment()
        {
            BuildLists();
            return allGender?.chest_Attachment?.Count ?? 0;
        }

        public int EditorGetCountBackAttachment()
        {
            BuildLists();
            return allGender?.back_Attachment?.Count ?? 0;
        }

        public int EditorGetCountShoulderRightAttachment()
        {
            BuildLists();
            return allGender?.shoulder_Attachment_Right?.Count ?? 0;
        }

        public int EditorGetCountShoulderLeftAttachment()
        {
            BuildLists();
            return allGender?.shoulder_Attachment_Left?.Count ?? 0;
        }

        public int EditorGetCountElbowRightAttachment()
        {
            BuildLists();
            return allGender?.elbow_Attachment_Right?.Count ?? 0;
        }

        public int EditorGetCountElbowLeftAttachment()
        {
            BuildLists();
            return allGender?.elbow_Attachment_Left?.Count ?? 0;
        }

        public int EditorGetCountHipsAttachment()
        {
            BuildLists();
            return allGender?.hips_Attachment?.Count ?? 0;
        }

        public int EditorGetCountKneeRightAttachment()
        {
            BuildLists();
            return allGender?.knee_Attachement_Right?.Count ?? 0;
        }

        public int EditorGetCountKneeLeftAttachment()
        {
            BuildLists();
            return allGender?.knee_Attachement_Left?.Count ?? 0;
        }
        #endif

        // Note: do not “guess” semantics by name. Lists in this asset pack are already semantically grouped:
        // - All_01_Hair = hairstyles
        // - HeadCoverings_Base_Hair = hats compatible with hair
        // - HeadCoverings_No_FacialHair = masks
        // - HeadCoverings_No_Hair = helmets
        // - All_02_Head_Attachment = helmet attachments
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(CharacterCustomizer))]
    public class CharacterCustomizerEditor : Editor
    {
        static bool _showBody = true;
        static bool _showAttachments = true;

        public override void OnInspectorGUI()
        {
            var customizer = (CharacterCustomizer)target;
            serializedObject.Update();

            customizer.EditorRebuildLists();

            // Compact layout.
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 165f;
            bool oldWideMode = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;

            void SliderInt(SerializedProperty prop, int min, int max, string label, int count, bool allowNone)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel($"{label} ({count})");

                if (max < min)
                {
                    prop.intValue = allowNone ? -1 : 0;
                    EditorGUILayout.IntField(prop.intValue, GUILayout.MaxWidth(56));
                    EditorGUILayout.EndHorizontal();
                    return;
                }

                prop.intValue = EditorGUILayout.IntSlider(prop.intValue, min, max);
                EditorGUILayout.EndHorizontal();
            }

            bool isHelmetHeadTab = customizer.headwearTab == CharacterCustomizer.HeadwearTab.HelmetHead;

            // --- Basics ---
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(CharacterCustomizer.gender)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(CharacterCustomizer.race)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(CharacterCustomizer.useHeadWithElements)));
            if (isHelmetHeadTab)
                EditorGUILayout.HelpBox("HelmetHead replaces the full head, so head/eyebrows/facial hair are hidden here.", MessageType.None);

            // --- Head ---
            var headIndexProp = serializedObject.FindProperty(nameof(CharacterCustomizer.headIndex));
            var eyebrowIndexProp = serializedObject.FindProperty(nameof(CharacterCustomizer.eyebrowIndex));
            var facialHairEnabledProp = serializedObject.FindProperty(nameof(CharacterCustomizer.facialHairEnabled));
            var facialHairIndexProp = serializedObject.FindProperty(nameof(CharacterCustomizer.facialHairIndex));
            var elfEarIndexProp = serializedObject.FindProperty(nameof(CharacterCustomizer.elfEarIndex));

            int headAllCount = customizer.EditorGetCountHeadsAllElements();
            int headNoCount = customizer.EditorGetCountHeadsNoElements();
            int eyebrowCount = customizer.EditorGetCountEyebrows();
            int facialHairCount = customizer.EditorGetCountFacialHair();
            int elfEarCount = customizer.EditorGetCountElfEars();

            int headMax = Mathf.Max(headAllCount, headNoCount) - 1;
            if (!isHelmetHeadTab)
            {
                EditorGUILayout.Space(2);
                SliderInt(headIndexProp, 0, headMax, "Head", Mathf.Max(headAllCount, headNoCount), allowNone: false);
                EditorGUILayout.PropertyField(facialHairEnabledProp);
            }

            // --- Headwear (tab driven) ---
            var headwearTabProp = serializedObject.FindProperty(nameof(CharacterCustomizer.headwearTab));
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(headwearTabProp);
            if (GUILayout.Button("Apply", GUILayout.MaxWidth(60)))
                customizer.ApplySelection();
            EditorGUILayout.EndHorizontal();

            var hairStyleIndexProp = serializedObject.FindProperty(nameof(CharacterCustomizer.hairStyleIndex));
            var hatIndexProp = serializedObject.FindProperty(nameof(CharacterCustomizer.hatIndex));
            var maskIndexProp = serializedObject.FindProperty(nameof(CharacterCustomizer.maskIndex));
            var helmetIndexProp = serializedObject.FindProperty(nameof(CharacterCustomizer.helmetIndex));
            var helmetAttachmentIndexProp = serializedObject.FindProperty(nameof(CharacterCustomizer.helmetAttachmentIndex));
            var helmetHeadIndexProp = serializedObject.FindProperty(nameof(CharacterCustomizer.helmetHeadIndex));

            int hairStyleCount = customizer.EditorGetCountHairStyles();
            int hatCount = customizer.EditorGetCountHats();
            int maskCount = customizer.EditorGetCountMasks();
            int helmetCount = customizer.EditorGetCountHelmets();
            int helmetAttachmentCount = customizer.EditorGetCountHelmetAttachments();

            EditorGUILayout.Space(2);
            switch (customizer.headwearTab)
            {
                case CharacterCustomizer.HeadwearTab.HairStyle:
                    if (customizer.useHeadWithElements)
                        SliderInt(eyebrowIndexProp, 0, eyebrowCount - 1, "Eyebrows", eyebrowCount, allowNone: false);
                    if (customizer.useHeadWithElements && customizer.gender == PsychoticLab.Gender.Male && customizer.facialHairEnabled)
                        SliderInt(facialHairIndexProp, 0, facialHairCount - 1, "Facial hair", facialHairCount, allowNone: false);
                    SliderInt(hairStyleIndexProp, -1, hairStyleCount - 1, "Hair style", hairStyleCount, allowNone: true);
                    break;

                case CharacterCustomizer.HeadwearTab.Hat:
                    if (customizer.useHeadWithElements)
                        SliderInt(eyebrowIndexProp, 0, eyebrowCount - 1, "Eyebrows", eyebrowCount, allowNone: false);
                    if (customizer.useHeadWithElements && customizer.gender == PsychoticLab.Gender.Male && customizer.facialHairEnabled)
                        SliderInt(facialHairIndexProp, 0, facialHairCount - 1, "Facial hair", facialHairCount, allowNone: false);
                    SliderInt(hairStyleIndexProp, -1, hairStyleCount - 1, "Hair style", hairStyleCount, allowNone: true);
                    SliderInt(hatIndexProp, -1, hatCount - 1, "Hat", hatCount, allowNone: true);
                    break;

                case CharacterCustomizer.HeadwearTab.Mask:
                    if (customizer.useHeadWithElements)
                        SliderInt(eyebrowIndexProp, 0, eyebrowCount - 1, "Eyebrows", eyebrowCount, allowNone: false);
                    SliderInt(hairStyleIndexProp, -1, hairStyleCount - 1, "Hair style", hairStyleCount, allowNone: true);
                    SliderInt(maskIndexProp, -1, maskCount - 1, "Mask", maskCount, allowNone: true);
                    break;

                case CharacterCustomizer.HeadwearTab.Helmet:
                    SliderInt(helmetIndexProp, -1, helmetCount - 1, "Helmet", helmetCount, allowNone: true);
                    if (customizer.race == PsychoticLab.Race.Elf)
                        SliderInt(elfEarIndexProp, 0, elfEarCount - 1, "Elf ears", elfEarCount, allowNone: false);
                    break;

                case CharacterCustomizer.HeadwearTab.HelmetAttachment:
                    SliderInt(helmetIndexProp, -1, helmetCount - 1, "Helmet", helmetCount, allowNone: true);
                    if (customizer.race == PsychoticLab.Race.Elf)
                        SliderInt(elfEarIndexProp, 0, elfEarCount - 1, "Elf ears", elfEarCount, allowNone: false);
                    SliderInt(helmetAttachmentIndexProp, -1, helmetAttachmentCount - 1, "Helmet attachment", helmetAttachmentCount, allowNone: true);
                    break;

                case CharacterCustomizer.HeadwearTab.HelmetHead:
                    SliderInt(helmetHeadIndexProp, -1, headNoCount - 1, "Helmet head", headNoCount, allowNone: true);
                    break;
            }

            // --- Body ---
            EditorGUILayout.Space(6);
            _showBody = EditorGUILayout.Foldout(_showBody, "Body", true);
            if (_showBody)
            {
                var torsoProp = serializedObject.FindProperty(nameof(CharacterCustomizer.torsoIndex));
                var armURProp = serializedObject.FindProperty(nameof(CharacterCustomizer.armUpperRightIndex));
                var armULProp = serializedObject.FindProperty(nameof(CharacterCustomizer.armUpperLeftIndex));
                var armLRProp = serializedObject.FindProperty(nameof(CharacterCustomizer.armLowerRightIndex));
                var armLLProp = serializedObject.FindProperty(nameof(CharacterCustomizer.armLowerLeftIndex));
                var handRProp = serializedObject.FindProperty(nameof(CharacterCustomizer.handRightIndex));
                var handLProp = serializedObject.FindProperty(nameof(CharacterCustomizer.handLeftIndex));
                var hipsProp = serializedObject.FindProperty(nameof(CharacterCustomizer.hipsIndex));
                var legRProp = serializedObject.FindProperty(nameof(CharacterCustomizer.legRightIndex));
                var legLProp = serializedObject.FindProperty(nameof(CharacterCustomizer.legLeftIndex));

                SliderInt(torsoProp, 0, customizer.EditorGetCountTorso() - 1, "Torso", customizer.EditorGetCountTorso(), allowNone: false);
                SliderInt(armURProp, 0, customizer.EditorGetCountArmUpperRight() - 1, "Arm upper (R)", customizer.EditorGetCountArmUpperRight(), allowNone: false);
                SliderInt(armULProp, 0, customizer.EditorGetCountArmUpperLeft() - 1, "Arm upper (L)", customizer.EditorGetCountArmUpperLeft(), allowNone: false);
                SliderInt(armLRProp, 0, customizer.EditorGetCountArmLowerRight() - 1, "Arm lower (R)", customizer.EditorGetCountArmLowerRight(), allowNone: false);
                SliderInt(armLLProp, 0, customizer.EditorGetCountArmLowerLeft() - 1, "Arm lower (L)", customizer.EditorGetCountArmLowerLeft(), allowNone: false);
                SliderInt(handRProp, 0, customizer.EditorGetCountHandRight() - 1, "Hand (R)", customizer.EditorGetCountHandRight(), allowNone: false);
                SliderInt(handLProp, 0, customizer.EditorGetCountHandLeft() - 1, "Hand (L)", customizer.EditorGetCountHandLeft(), allowNone: false);
                SliderInt(hipsProp, 0, customizer.EditorGetCountHips() - 1, "Hips", customizer.EditorGetCountHips(), allowNone: false);
                SliderInt(legRProp, 0, customizer.EditorGetCountLegRight() - 1, "Leg (R)", customizer.EditorGetCountLegRight(), allowNone: false);
                SliderInt(legLProp, 0, customizer.EditorGetCountLegLeft() - 1, "Leg (L)", customizer.EditorGetCountLegLeft(), allowNone: false);
            }

            // --- Attachments ---
            EditorGUILayout.Space(4);
            _showAttachments = EditorGUILayout.Foldout(_showAttachments, "Attachments", true);
            if (_showAttachments)
            {
                var chestProp = serializedObject.FindProperty(nameof(CharacterCustomizer.chestAttachmentIndex));
                var backProp = serializedObject.FindProperty(nameof(CharacterCustomizer.backAttachmentIndex));
                var shoulderRProp = serializedObject.FindProperty(nameof(CharacterCustomizer.shoulderRightIndex));
                var shoulderLProp = serializedObject.FindProperty(nameof(CharacterCustomizer.shoulderLeftIndex));
                var elbowRProp = serializedObject.FindProperty(nameof(CharacterCustomizer.elbowRightIndex));
                var elbowLProp = serializedObject.FindProperty(nameof(CharacterCustomizer.elbowLeftIndex));
                var hipsAProp = serializedObject.FindProperty(nameof(CharacterCustomizer.hipsAttachmentIndex));
                var kneeRProp = serializedObject.FindProperty(nameof(CharacterCustomizer.kneeRightIndex));
                var kneeLProp = serializedObject.FindProperty(nameof(CharacterCustomizer.kneeLeftIndex));

                SliderInt(chestProp, -1, customizer.EditorGetCountChestAttachment() - 1, "Chest", customizer.EditorGetCountChestAttachment(), allowNone: true);
                SliderInt(backProp, -1, customizer.EditorGetCountBackAttachment() - 1, "Back", customizer.EditorGetCountBackAttachment(), allowNone: true);
                SliderInt(shoulderRProp, -1, customizer.EditorGetCountShoulderRightAttachment() - 1, "Shoulder (R)", customizer.EditorGetCountShoulderRightAttachment(), allowNone: true);
                SliderInt(shoulderLProp, -1, customizer.EditorGetCountShoulderLeftAttachment() - 1, "Shoulder (L)", customizer.EditorGetCountShoulderLeftAttachment(), allowNone: true);
                SliderInt(elbowRProp, -1, customizer.EditorGetCountElbowRightAttachment() - 1, "Elbow (R)", customizer.EditorGetCountElbowRightAttachment(), allowNone: true);
                SliderInt(elbowLProp, -1, customizer.EditorGetCountElbowLeftAttachment() - 1, "Elbow (L)", customizer.EditorGetCountElbowLeftAttachment(), allowNone: true);
                SliderInt(hipsAProp, -1, customizer.EditorGetCountHipsAttachment() - 1, "Hips", customizer.EditorGetCountHipsAttachment(), allowNone: true);
                SliderInt(kneeRProp, -1, customizer.EditorGetCountKneeRightAttachment() - 1, "Knee (R)", customizer.EditorGetCountKneeRightAttachment(), allowNone: true);
                SliderInt(kneeLProp, -1, customizer.EditorGetCountKneeLeftAttachment() - 1, "Knee (L)", customizer.EditorGetCountKneeLeftAttachment(), allowNone: true);
            }

            // --- Colors/material ---
            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(CharacterCustomizer.materialOverride)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(CharacterCustomizer.primaryColor)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(CharacterCustomizer.secondaryColor)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(CharacterCustomizer.skinColor)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(CharacterCustomizer.hairColor)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(CharacterCustomizer.applyColors)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(CharacterCustomizer.mat)));

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6);
            if (GUILayout.Button(Application.isPlaying ? "Apply Selection (Play Mode)" : "Apply Selection (Edit Mode)"))
                customizer.ApplySelection();

            EditorGUIUtility.labelWidth = oldLabelWidth;
            EditorGUIUtility.wideMode = oldWideMode;
        }
    }
#endif
}
