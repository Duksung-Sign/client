using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class KeyInputListener : MonoBehaviour
{
    public static KeyInputListener Instance;

    private Coroutine listeningCoroutine;
    private bool isListening = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void StartListeningFor(string keyString, string parameters)
    {
        if (isListening) return;

        var paramDict = ParseParameters(parameters);

        paramDict.TryGetValue("wrongKeyDialogue", out string wrongKeyDialogueID);
        paramDict.TryGetValue("timeoutDialogue", out string timeoutDialogueID);
        float.TryParse(paramDict.GetValueOrDefault("timeout", "10"), out float timeoutDuration);

        try
        {
            KeyCode targetKey = (KeyCode)Enum.Parse(typeof(KeyCode), keyString, true);
            listeningCoroutine = StartCoroutine(ListenCoroutine(targetKey, wrongKeyDialogueID, timeoutDialogueID, timeoutDuration));
        }
        catch (ArgumentException)
        {
            Debug.LogError($"[KeyInputListener] '{keyString}'는 유효한 KeyCode가 아닙니다.");
        }
    }

    private IEnumerator ListenCoroutine(KeyCode key, string wrongKeyDialogue, string timeoutDialogue, float timeout)
    {
        isListening = true;
        Debug.Log($"[KeyInputListener] '{key}' 키 입력 감시 시작. (제한 시간: {timeout}초)");

        float timer = 0f;

        while (true)
        {
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsInDialogue())
            {
                yield return null;
                continue;
            }

            if (Input.GetKeyDown(key))
            {
                Debug.Log($"[KeyInputListener] '{key}' 키 입력 감지! (성공)");
                GameEventManager.Instance?.TriggerKeyEvent("KEY_PRESSED", key.ToString());
                break; 
            }

            if (Input.anyKeyDown && !string.IsNullOrEmpty(wrongKeyDialogue))
            {
                Debug.Log("[KeyInputListener] 잘못된 키 입력 감지.");
                DialogueManager.Instance.StartRandomDialogueFromSet("wrong_key_feedback");
                yield return new WaitUntil(() => DialogueManager.Instance == null || !DialogueManager.Instance.IsInDialogue());
                timer = 0f; 
            }

            timer += Time.deltaTime;
            if (timer >= timeout && !string.IsNullOrEmpty(timeoutDialogue))
            {
                Debug.Log("[KeyInputListener] 시간 초과 감지.");
                DialogueManager.Instance.StartRandomDialogueFromSet("mio_timeout_feedback");
                yield return new WaitUntil(() => DialogueManager.Instance == null || !DialogueManager.Instance.IsInDialogue());
                timer = 0f; 
            }

            yield return null;
        }

        isListening = false;
        listeningCoroutine = null;
    }

    private Dictionary<string, string> ParseParameters(string parameters)
    {
        return parameters.Split(';')
                         .Select(p => p.Split('='))
                         .Where(p => p.Length == 2)
                         .ToDictionary(p => p[0].Trim(), p => p[1].Trim());
    }
}