using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "CharacterDatabase", menuName = "Game Data/Character Database")]
public class CharacterDatabase : ScriptableObject
{
    [SerializeField] private List<CharacterPortrait> portraits;

    private Dictionary<string, Sprite> portraitDictionary;

    private void OnEnable()
    {
        Initialize();
    }

    public void Initialize()
    {
        portraitDictionary = new Dictionary<string, Sprite>();
        if (portraits == null) return;
        
        foreach (var entry in portraits)
        {
            portraitDictionary[entry.characterName] = entry.portraitSprite;
        }
    }

    public Sprite GetPortrait(string characterName)
    {
        if (portraitDictionary == null)
        {
            Initialize();
        }
        
        if (portraitDictionary.TryGetValue(characterName, out Sprite portrait))
        {
            return portrait;
        }

        return null;
    }
}

[System.Serializable]
public class CharacterPortrait
{
    public string characterName;
    public Sprite portraitSprite;
}