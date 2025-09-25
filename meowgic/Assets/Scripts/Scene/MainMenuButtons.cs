using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections; 

public class MainMenuButtons : MonoBehaviour
{
    [Header("메뉴 버튼")]
    public Button startGameButton;
    
    private bool isLoading = false; 

    private void Start()
    {
        if (startGameButton != null)
            startGameButton.onClick.AddListener(StartGame);
    }

    public void StartGame()
    {
        if (isLoading) return;

        isLoading = true;

        if (GameManager.Instance != null)
        {
            // 재시작: GameManager가 페이드/상태 전환/데이터 리셋까지 처리
            GameManager.Instance.StartNewGame("Library");
            isLoading = false;
            return;
        }

        StartCoroutine(LoadGameScenes());
    }
    private IEnumerator LoadGameScenes()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[MainMenu] PlayerPrefs 데이터가 초기화되었습니다.");

        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.ResetData();
        }

        yield return SceneManager.LoadSceneAsync("Library");

        if (!SceneManager.GetSceneByName("SettingScene").isLoaded)
        {
            yield return SceneManager.LoadSceneAsync("SettingScene", LoadSceneMode.Additive);
        }

        Debug.Log("[MainMenu] 모든 씬 로딩 완료");
        isLoading = false;
    }
}