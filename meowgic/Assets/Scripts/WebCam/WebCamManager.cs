using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WebCamManager : MonoBehaviour
{
    [Header("웹캠 설정")]
    public RawImage webCamDisplay;  // UI에서 연결할 RawImage
    public int preferredWidth = 640;
    public int preferredHeight = 480;
    public int preferredFPS = 30;

    [Header("상태 확인")]
    public bool isWebCamActive = false;

    private WebCamTexture webCamTexture;
    private WebCamDevice[] availableDevices;
    private int currentDeviceIndex = 0;

    void Start()
    {
        StartCoroutine(InitializeWebCamAsync());
    }

    IEnumerator InitializeWebCamAsync()
    {
        yield return new WaitForEndOfFrame(); // 한 프레임 대기

        availableDevices = WebCamTexture.devices;

        if (availableDevices.Length > 0)
        {
            Debug.Log($"발견된 웹캠 장치: {availableDevices.Length}개");

            yield return new WaitForEndOfFrame(); // 또 한 프레임 대기

            InitializeWebCam();
        }
        else
        {
            Debug.LogError("웹캠을 찾을 수 없습니다!");
        }
    }

    void InitializeWebCam()
    {
        string deviceName = availableDevices[currentDeviceIndex].name;
        webCamTexture = new WebCamTexture(deviceName, preferredWidth, preferredHeight, preferredFPS);

        // 중요: 텍스처 필터링 설정
        webCamTexture.filterMode = FilterMode.Point; // 빠른 필터링

        if (webCamDisplay != null)
        {
            webCamDisplay.texture = webCamTexture;
            StartWebCam();
            Debug.Log($"웹캠이 초기화되었습니다: {deviceName}");
        }
    }

    public void StartWebCam()
    {
        if (webCamTexture != null && !webCamTexture.isPlaying)
        {
            webCamTexture.Play();
            isWebCamActive = true;
            Debug.Log("웹캠이 시작되었습니다.");
        }
    }
}