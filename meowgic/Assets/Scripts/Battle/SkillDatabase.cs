using UnityEngine;
using System.Collections.Generic;

public class SkillDatabase : MonoBehaviour
{
    public static SkillDatabase Instance { get; private set; }

    [System.Serializable]
    public class SkillMapping
    {
        public string skillName;
        public string gestureType;
    }

    public List<SkillMapping> skillMappings = new List<SkillMapping>()
    {
        new SkillMapping { skillName = "안녕하세요", gestureType = "hello" },
        new SkillMapping { skillName = "반갑습니다", gestureType = "nice" },
        new SkillMapping { skillName = "감사합니다", gestureType = "thank" },
        new SkillMapping { skillName = "괜찮아요", gestureType = "okay" },
        new SkillMapping { skillName = "사랑한다", gestureType = "love" },
        new SkillMapping { skillName = "연락해주세요", gestureType = "call" },
        new SkillMapping { skillName = "도착하다", gestureType = "arrive" },
        new SkillMapping { skillName = "당신", gestureType = "you" },
        new SkillMapping { skillName = "나", gestureType = "me" },
        new SkillMapping { skillName = "싫다", gestureType = "no" },
        new SkillMapping { skillName = "좋다", gestureType = "goodnice" },
        new SkillMapping { skillName = "죄송합니다", gestureType = "sorry" }
    };

    private Dictionary<string, string> skillDict;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeSkillDict();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeSkillDict()
    {
        skillDict = new Dictionary<string, string>();
        foreach (var skill in skillMappings)
        {
            skillDict[skill.skillName] = skill.gestureType;
        }
    }

    public string GetGestureType(string skillName)
    {
        if (skillDict != null && skillDict.ContainsKey(skillName))
        {
            return skillDict[skillName];
        }
        return skillName.ToLower();
    }
}