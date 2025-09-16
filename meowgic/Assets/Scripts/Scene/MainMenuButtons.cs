using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuButtons : MonoBehaviour
{
    [Header("메뉴 버튼")]
    public Button startGameButton;

    private void Start()
    {
        if (startGameButton != null)
            startGameButton.onClick.AddListener(StartGame);
    }

    public void StartGame()
    {
        // 1) 플레이 데이터 초기화
        // * 전체를 지워도 되고, 필요한 키만 선택 삭제해도 됨
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[MainMenu] 플레이 데이터 초기화");

        // 2) 첫 실행 플래그가 필요하면 다시 세팅
        // PlayerPrefs.SetInt("HasLaunchedBefore", 1);
        // PlayerPrefs.Save();

        // 3) 실제 게임 씬 로드
        SceneManager.LoadScene("Library");


        // 4) setting 화면 로드
        if (!SceneManager.GetSceneByName("SettingScene").isLoaded)
        {
            SceneManager.LoadScene("SettingScene", LoadSceneMode.Additive);
        }
    }
}
