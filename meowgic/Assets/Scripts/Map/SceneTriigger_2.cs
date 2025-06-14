using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTrigger_2 : MonoBehaviour
{
    public string MapTest;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            SceneManager.LoadScene("MapTest");
        }
    }
}
