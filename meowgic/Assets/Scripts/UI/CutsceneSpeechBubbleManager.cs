using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class CutsceneSpeechBubbleManager : MonoBehaviour
{
    [System.Serializable]
    public class SpeechBubbleData
    {
        public string characterName;
        public Transform characterTransform;
        public Vector3 bubbleOffset = new Vector3(0, 2, 0);
        public Color bubbleColor = Color.white;
    }

    [Header("말풍선 프리팹")]
    public GameObject speechBubblePrefab;

    [Header("캐릭터 설정")]
    public List<SpeechBubbleData> characters = new List<SpeechBubbleData>();

    [Header("설정")]
    public float typewriterSpeed = 0.05f;
    public float displayDuration = 2f;

    private Dictionary<string, SpeechBubbleData> characterDict;
    private Dictionary<string, CutsceneSpeechBubble> activeBubbles;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;

        // 캐릭터 딕셔너리 초기화
        characterDict = new Dictionary<string, SpeechBubbleData>();
        activeBubbles = new Dictionary<string, CutsceneSpeechBubble>();

        foreach (var character in characters)
        {
            characterDict[character.characterName] = character;
        }
    }

    public void ShowMessage(string characterName, string message)
    {
        StartCoroutine(ShowMessageCoroutine(characterName, message));
    }

    IEnumerator ShowMessageCoroutine(string characterName, string message)
    {
        if (!characterDict.ContainsKey(characterName))
        {
            Debug.LogError($"캐릭터를 찾을 수 없습니다: {characterName}");
            yield break;
        }

        var characterData = characterDict[characterName];

        // 기존 말풍선이 있으면 제거
        if (activeBubbles.ContainsKey(characterName))
        {
            activeBubbles[characterName].HideMessage();
            Destroy(activeBubbles[characterName].gameObject);
        }

        // 새 말풍선 생성
        GameObject bubbleObj = Instantiate(speechBubblePrefab, transform);
        CutsceneSpeechBubble bubble = bubbleObj.GetComponent<CutsceneSpeechBubble>();

        // 말풍선 설정
        bubble.Initialize(characterData.characterTransform, characterData.bubbleOffset, characterData.bubbleColor);
        activeBubbles[characterName] = bubble;

        // 메시지 표시
        yield return StartCoroutine(bubble.ShowMessage(message, typewriterSpeed, displayDuration));

        // 정리
        activeBubbles.Remove(characterName);
        Destroy(bubbleObj);
    }

    public void HideAllMessages()
    {
        foreach (var bubble in activeBubbles.Values)
        {
            if (bubble != null)
            {
                bubble.HideMessage();
                Destroy(bubble.gameObject);
            }
        }
        activeBubbles.Clear();
    }
}