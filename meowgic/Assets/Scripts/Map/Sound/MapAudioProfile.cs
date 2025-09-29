using UnityEngine;

[CreateAssetMenu(fileName = "MapAudioProfile", menuName = "Game Data/Map Audio Profile")]
public class MapAudioProfile : ScriptableObject
{
    [Header("맵 오디오 설정")]
    public AudioClip backgroundMusic;
    public AudioClip footstepSound;
}