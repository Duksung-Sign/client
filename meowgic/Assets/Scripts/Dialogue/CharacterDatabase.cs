using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "CharacterDatabase", menuName = "Game Data/Character Database")]
public class CharacterDatabase : ScriptableObject
{
    [SerializeField] private List<CharacterData> characterDataList; 

    private Dictionary<string, Sprite> portraitDictionary;
    private Dictionary<string, AudioClip> typingSoundDictionary;

    private void OnEnable()
    {
        Initialize();
    }

    public void Initialize()
    {
        portraitDictionary = new Dictionary<string, Sprite>();
        typingSoundDictionary = new Dictionary<string, AudioClip>(); 
        if (characterDataList == null) return;

        foreach (var entry in characterDataList)
        {
            portraitDictionary[entry.characterName] = entry.portraitSprite;
            typingSoundDictionary[entry.characterName] = entry.typingSound; 
        }
    }

    public Sprite GetPortrait(string characterName)
    {
        if (portraitDictionary == null) Initialize();
        portraitDictionary.TryGetValue(characterName, out Sprite portrait);
        return portrait;
    }

    public AudioClip GetTypingSound(string characterName)
    {
        if (typingSoundDictionary == null) Initialize();
        typingSoundDictionary.TryGetValue(characterName, out AudioClip sound);
        return sound;
    }
}

[System.Serializable]
public class CharacterData 
{
    public string characterName;
    public Sprite portraitSprite;
    public AudioClip typingSound;
    public float soundInterval = 0.1f;
}