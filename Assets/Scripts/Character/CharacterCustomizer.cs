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

        [Header("Hair / Head coverings")]
        [Tooltip("0=Base Hair, 1=No Facial Hair, 2=No Hair (helmets). See HeadCovering enum.")]
        public PsychoticLab.HeadCovering headCovering = PsychoticLab.HeadCovering.HeadCoverings_Base_Hair;

        [Tooltip("Index into hair/helmet list. Base Hair/No Facial Hair: hair style. No Hair: helmet (0–13).")]
        public int hairIndex;

        [Tooltip("Index into elf ear (if HeadCovering = No Hair and race = Elf).")]
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
            bool hasFacialHair = gender == PsychoticLab.Gender.Male &&
                headCovering != PsychoticLab.HeadCovering.HeadCoverings_No_FacialHair;

            if (useHeadWithElements)
            {
                ActivateSafe(cog.headAllElements, headIndex);
                ActivateSafe(cog.eyebrow, eyebrowIndex);
                if (hasFacialHair)
                    ActivateSafe(cog.facialHair, facialHairIndex);

                switch (headCovering)
                {
                    case PsychoticLab.HeadCovering.HeadCoverings_Base_Hair:
                        ActivateSafe(allGender.all_Hair, 1);
                        ActivateSafe(allGender.headCoverings_Base_Hair, hairIndex);
                        break;
                    case PsychoticLab.HeadCovering.HeadCoverings_No_FacialHair:
                        ActivateSafe(allGender.all_Hair, hairIndex);
                        ActivateSafe(allGender.headCoverings_No_FacialHair, hairIndex);
                        break;
                    case PsychoticLab.HeadCovering.HeadCoverings_No_Hair:
                        ActivateSafe(allGender.all_Hair, 0);
                        ActivateSafe(allGender.headCoverings_No_Hair, hairIndex);
                        if (race == PsychoticLab.Race.Elf)
                            ActivateSafe(allGender.elf_Ear, elfEarIndex);
                        break;
                }
            }
            else
            {
                ActivateSafe(cog.headNoElements, headIndex);
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
                        go.SetActive(false);
                        targetList.Add(go);
                    }
                    return;
                }
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(CharacterCustomizer))]
    public class CharacterCustomizerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var customizer = (CharacterCustomizer)target;
            if (Application.isPlaying && GUILayout.Button("Apply Selection"))
            {
                customizer.ApplySelection();
            }
        }
    }
#endif
}
