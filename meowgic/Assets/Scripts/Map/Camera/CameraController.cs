using UnityEngine;
using System.Collections;

public class CameraController : MonoBehaviour
{
    [Header("타겟 설정")]
    public Transform target; 

    [Header("카메라 설정")]
    public float cameraSize = 3f; 

    [Header("카메라 팔로우 설정")]
    public float followSpeed = 2f; 
    public Vector3 offset = new Vector3(0, 0, -10); 

    [Header("맵 경계 설정")]
    public bool useMapBounds = true;
    public Bounds mapBounds; 

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

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                target = player.transform;
        }

        if (target != null)
        {
            Vector3 initialPosition = target.position + offset;
            transform.position = initialPosition;
        }
    }

    void Start()
    {
        if (cam != null)
            cam.orthographicSize = cameraSize;

        if (useMapBounds && mapBounds.size == Vector3.zero)
        {
            AutoDetectMapBounds();
        }

        UpdateCameraBounds();

        if (useMapBounds && target != null)
        {
            Vector3 constrainedPosition = ConstrainToMapBounds(target.position + offset);
            transform.position = constrainedPosition;
        }
    }

    void Update()
    {
        if (target == null) return;
    }

    void LateUpdate()
    {
        if (target == null) return;


        UpdateCameraPosition();
        UpdateCameraBounds();
    }

    #region 카메라 위치 업데이트

    void UpdateCameraPosition()
    {
        if (isShaking) return; 

        targetPosition = target.position + offset;

        if (useMapBounds)
        {
            targetPosition = ConstrainToMapBounds(targetPosition);
        }

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
        Renderer[] renderers = FindObjectsOfType<Renderer>();

        if (renderers.Length == 0)
        {
            Debug.LogWarning("맵 경계를 자동으로 찾을 수 없습니다. 수동으로 설정해주세요.");
            return;
        }

        Bounds totalBounds = renderers[0].bounds;

        foreach (Renderer renderer in renderers)
        {
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