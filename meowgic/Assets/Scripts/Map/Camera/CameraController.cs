using UnityEngine;
using System.Collections;

public class CameraController : MonoBehaviour
{
    [Header("타겟 설정")]
    public Transform target; // 플레이어

    [Header("카메라 설정")]
    public float cameraSize = 3f; // 고정 카메라 크기 (가까운 시점)

    [Header("카메라 팔로우 설정")]
    public float followSpeed = 2f; // 카메라 따라가는 속도
    public Vector3 offset = new Vector3(0, 0, -10); // 카메라 오프셋

    [Header("맵 경계 설정")]
    public bool useMapBounds = true;
    public Bounds mapBounds; // 맵의 경계

    [Header("부드러운 이동")]
    public bool useSmoothFollow = true;
    public float smoothTime = 0.3f;

    [Header("카메라 흔들림 (셰이크)")]
    public float shakeIntensity = 0.1f;
    public float shakeDuration = 0.5f;

    [Header("디버그")]
    public bool showMapBounds = true;
    public bool debugMode = false;

    private Camera cam;
    private Vector3 velocity = Vector3.zero;
    private Vector3 targetPosition;
    private bool isShaking = false;

    // 카메라 이동 제한용
    private float halfWidth;
    private float halfHeight;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("Camera 컴포넌트를 찾을 수 없습니다!");
            return;
        }

        // 플레이어 자동 찾기
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                target = player.transform;
        }

        // 카메라를 즉시 플레이어 위치로 이동 (부드러운 이동 없이)
        if (target != null)
        {
            Vector3 initialPosition = target.position + offset;
            transform.position = initialPosition;

            if (debugMode)
                Debug.Log($"카메라 즉시 플레이어 위치로 이동: {initialPosition}");
        }
    }

    void Start()
    {
        // 카메라 크기 고정 설정
        if (cam != null)
            cam.orthographicSize = cameraSize;

        // 맵 경계가 설정되지 않았으면 자동 설정
        if (useMapBounds && mapBounds.size == Vector3.zero)
        {
            AutoDetectMapBounds();
        }

        UpdateCameraBounds();

        // 맵 경계가 설정되었다면 초기 위치도 경계에 맞게 조정
        if (useMapBounds && target != null)
        {
            Vector3 constrainedPosition = ConstrainToMapBounds(target.position + offset);
            transform.position = constrainedPosition;
        }

        if (debugMode)
            Debug.Log($"카메라 컨트롤러 초기화 완료 - 고정 크기: {cameraSize}, 시작 위치: {transform.position}");
    }

    void Update()
    {
        if (target == null) return;

        UpdateCameraPosition();
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 카메라 경계 업데이트
        UpdateCameraBounds();
    }

    #region 카메라 위치 업데이트

    void UpdateCameraPosition()
    {
        if (isShaking) return; // 흔들림 중에는 위치 업데이트 안함

        // 목표 위치 계산
        targetPosition = target.position + offset;

        // 맵 경계 제한 적용
        if (useMapBounds)
        {
            targetPosition = ConstrainToMapBounds(targetPosition);
        }

        // 부드러운 이동 적용
        if (useSmoothFollow)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
        }

        if (debugMode)
        {
            Debug.DrawLine(transform.position, target.position, Color.green);
        }
    }

    Vector3 ConstrainToMapBounds(Vector3 position)
    {
        // 카메라가 맵 밖으로 나가지 않도록 제한
        float clampedX = Mathf.Clamp(position.x,
            mapBounds.min.x + halfWidth,
            mapBounds.max.x - halfWidth);

        float clampedY = Mathf.Clamp(position.y,
            mapBounds.min.y + halfHeight,
            mapBounds.max.y - halfHeight);

        return new Vector3(clampedX, clampedY, position.z);
    }

    void UpdateCameraBounds()
    {
        if (cam != null)
        {
            halfHeight = cam.orthographicSize;
            halfWidth = halfHeight * cam.aspect;
        }
    }

    #endregion

    #region 카메라 효과

    public void ShakeCamera(float intensity = 0, float duration = 0)
    {
        if (intensity == 0) intensity = shakeIntensity;
        if (duration == 0) duration = shakeDuration;

        if (!isShaking)
        {
            StartCoroutine(ShakeCoroutine(intensity, duration));
        }
    }

    IEnumerator ShakeCoroutine(float intensity, float duration)
    {
        isShaking = true;
        Vector3 originalPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * intensity;
            float y = Random.Range(-1f, 1f) * intensity;

            transform.position = originalPosition + new Vector3(x, y, 0);

            elapsed += Time.deltaTime;
            intensity = Mathf.Lerp(shakeIntensity, 0, elapsed / duration); // 점점 약해짐
            yield return null;
        }

        transform.position = originalPosition;
        isShaking = false;
    }

    public void FocusOnTarget(Transform newTarget, float focusTime = 1f)
    {
        StartCoroutine(FocusCoroutine(newTarget, focusTime));
    }

    IEnumerator FocusCoroutine(Transform focusTarget, float duration)
    {
        Transform originalTarget = target;
        target = focusTarget;

        yield return new WaitForSeconds(duration);

        target = originalTarget;
    }

    #endregion

    #region 맵 경계 설정

    void AutoDetectMapBounds()
    {
        // 씬의 모든 Renderer를 찾아서 전체 경계 계산
        Renderer[] renderers = FindObjectsOfType<Renderer>();

        if (renderers.Length == 0)
        {
            Debug.LogWarning("맵 경계를 자동으로 찾을 수 없습니다. 수동으로 설정해주세요.");
            return;
        }

        Bounds totalBounds = renderers[0].bounds;

        foreach (Renderer renderer in renderers)
        {
            // UI나 플레이어는 제외
            if (renderer.gameObject.layer == LayerMask.NameToLayer("UI") ||
                renderer.gameObject.CompareTag("Player"))
                continue;

            totalBounds.Encapsulate(renderer.bounds);
        }

        mapBounds = totalBounds;

        if (debugMode)
            Debug.Log($"자동 감지된 맵 경계: {mapBounds}");
    }

    public void SetMapBounds(Bounds bounds)
    {
        mapBounds = bounds;
        useMapBounds = true;
    }

    public void SetMapBounds(Vector3 center, Vector3 size)
    {
        mapBounds = new Bounds(center, size);
        useMapBounds = true;
    }

    #endregion

    #region 카메라 크기 조정 (런타임에서만)

    /// <summary>
    /// 런타임에서 카메라 크기 변경 (특별한 상황에서만 사용)
    /// </summary>
    public void SetCameraSize(float newSize)
    {
        cameraSize = newSize;
        if (cam != null)
            cam.orthographicSize = cameraSize;

        if (debugMode)
            Debug.Log($"카메라 크기 변경: {newSize}");
    }

    /// <summary>
    /// 원래 카메라 크기로 복원
    /// </summary>
    public void ResetCameraSize()
    {
        if (cam != null)
            cam.orthographicSize = cameraSize;
    }

    #endregion

    #region 카메라 프리셋

    [System.Serializable]
    public class CameraPreset
    {
        public string name;
        public float size;
        public float followSpeed;
        public float smoothTime;
        public Vector3 offset;
    }

    [Header("카메라 프리셋")]
    public CameraPreset[] presets = new CameraPreset[]
    {
        new CameraPreset { name = "매우 가까움", size = 2.5f, followSpeed = 3f, smoothTime = 0.2f, offset = Vector3.back * 10 },
        new CameraPreset { name = "가까움 (기본)", size = 3f, followSpeed = 2.5f, smoothTime = 0.3f, offset = Vector3.back * 10 },
        new CameraPreset { name = "조금 멀리", size = 4f, followSpeed = 2f, smoothTime = 0.4f, offset = Vector3.back * 10 }
    };

    public void ApplyPreset(int presetIndex)
    {
        if (presetIndex < 0 || presetIndex >= presets.Length) return;

        CameraPreset preset = presets[presetIndex];
        SetCameraSize(preset.size);
        followSpeed = preset.followSpeed;
        smoothTime = preset.smoothTime;
        offset = preset.offset;

        if (debugMode)
            Debug.Log($"카메라 프리셋 적용: {preset.name}");
    }

    #endregion

    #region 개발자 도구

    [ContextMenu("Apply Close View (Size 3)")]
    public void SetCloseView()
    {
        cameraSize = 3f;
        followSpeed = 2.5f;
        smoothTime = 0.3f;
        useSmoothFollow = true;

        if (cam != null)
            cam.orthographicSize = cameraSize;

        Debug.Log("가까운 시점 카메라 설정 적용됨 (Size: 3)");
    }

    [ContextMenu("Apply Very Close View (Size 2.5)")]
    public void SetVeryCloseView()
    {
        cameraSize = 2.5f;
        followSpeed = 3f;
        smoothTime = 0.2f;
        useSmoothFollow = true;

        if (cam != null)
            cam.orthographicSize = cameraSize;

        Debug.Log("매우 가까운 시점 카메라 설정 적용됨 (Size: 2.5)");
    }

    [ContextMenu("Auto Detect Map Bounds")]
    public void DetectMapBounds()
    {
        AutoDetectMapBounds();
    }

    [ContextMenu("Test Camera Shake")]
    public void TestShake()
    {
        ShakeCamera();
    }

    #endregion

    #region 기즈모 그리기

    void OnDrawGizmosSelected()
    {
        if (showMapBounds && useMapBounds)
        {
            // 맵 경계 표시
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(mapBounds.center, mapBounds.size);

            // 카메라 뷰 경계 표시 (카메라가 이동할 수 있는 범위)
            if (Application.isPlaying)
            {
                Gizmos.color = Color.yellow;
                Vector3 cameraConstraintCenter = mapBounds.center;
                Vector3 cameraConstraintSize = new Vector3(
                    mapBounds.size.x - halfWidth * 2,
                    mapBounds.size.y - halfHeight * 2,
                    0
                );
                Gizmos.DrawWireCube(cameraConstraintCenter, cameraConstraintSize);
            }
        }

        // 타겟 표시
        if (target != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(target.position, 0.5f);
            Gizmos.DrawLine(transform.position, target.position);
        }

        // 현재 카메라 뷰 영역 표시
        if (Application.isPlaying && cam != null)
        {
            Gizmos.color = Color.blue;
            float height = cam.orthographicSize * 2;
            float width = height * cam.aspect;
            Gizmos.DrawWireCube(transform.position, new Vector3(width, height, 0));
        }
    }

    #endregion
}