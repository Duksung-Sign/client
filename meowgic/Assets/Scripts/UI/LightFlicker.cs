using UnityEngine;
using UnityEngine.Rendering.Universal; // Light2D를 사용하기 위해 필요
using System.Collections;

public class LightFlicker : MonoBehaviour
{
    [Tooltip("제어할 Light2D 컴포넌트")]
    [SerializeField] private Light2D targetLight;

    [Header("깜빡임 설정")]
    [Tooltip("조명이 가질 수 있는 최소 밝기")]
    [SerializeField] private float minIntensity = 0.8f;
    
    [Tooltip("조명이 가질 수 있는 최대 밝기")]
    [SerializeField] private float maxIntensity = 1.2f;

    [Tooltip("깜빡이는 속도. 값이 높을수록 더 빠르게 깜빡입니다.")]
    [SerializeField] private float flickerSpeed = 10f;

    private void Awake()
    {
        if (targetLight == null)
        {
            targetLight = GetComponent<Light2D>();
        }
    }

    private void OnEnable()
    {
        StartCoroutine(FlickerRoutine());
    }

    private IEnumerator FlickerRoutine()
    {
        while (true)
        {
            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, 0);

            targetLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
            
            yield return null; 
        }
    }
}