using UnityEngine;
using System.IO.Ports;
using System.Threading;
using System; // Array.Copy 사용
using System.Linq;

public class SerialSensorReader : MonoBehaviour
{
    [Header("Ports")]
    public string portRight = "COM6";  // 오른손
    public string portLeft = "COM5";   // 왼손
    public int baudRate = 9600;

    [Header("Filter (EWMA)")]
    [Range(0f, 1f)] public float alpha = 0.3f;

    private SerialPort spRight, spLeft;
    private Thread thRight, thLeft;
    private volatile bool running;

    // 클래스 상단에 노출하면 현장 조정 쉬움
    [Header("Stop Detection (match Python)")]
    [SerializeField] private float accNormMin = 0.8f;
    [SerializeField] private float accNormMax = 1.2f;
    [SerializeField] private float gyroNormLim = 0.5f;

    private float[] latestRight = new float[16];
    private float[] latestLeft = new float[16];

    // [수정] diff 계산을 위한 이전 프레임의 '필터링된 raw' 값
    private float[] prevRightRaw = new float[6];
    private float[] prevLeftRaw = new float[6];

    // [수정] hasRight/hasLeft는 EWMA용, hasRightPrev/hasLeftPrev는 diff 계산용
    private bool hasRight, hasLeft;
    private bool hasRightPrev, hasLeftPrev;

    private readonly object lockObj = new object();

    void OnEnable()
    {
        TryOpen(ref spRight, portRight);
        TryOpen(ref spLeft, portLeft);

        running = true;
        if (spRight != null) { thRight = new Thread(ReadRightLoop); thRight.Start(); }
        if (spLeft != null) { thLeft = new Thread(ReadLeftLoop); thLeft.Start(); }
    }

    void OnDisable()
    {
        running = false;

        // 스레드 종료 대기
        try { thRight?.Join(200); } catch { }
        try { thLeft?.Join(200); } catch { }

        SafeClose(spRight); SafeClose(spLeft);
        spRight = spLeft = null;
    }

    void TryOpen(ref SerialPort sp, string port)
    {
        try
        {
            if (string.IsNullOrEmpty(port)) return;
            sp = new SerialPort(port, baudRate);
            sp.NewLine = "\n";
            sp.ReadTimeout = 100;
            sp.Open();
            Debug.Log($"[Arduino] Opened {port}");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Arduino] Open failed {port}: {ex.Message}");
            sp = null;
        }
    }

    void SafeClose(SerialPort sp)
    {
        try { if (sp != null && sp.IsOpen) sp.Close(); } catch { }
    }

    void ReadRightLoop()
    {
        ReadLoop(spRight, isRight: true);
    }
    void ReadLeftLoop()
    {
        ReadLoop(spLeft, isRight: false);
    }

    //void ReadLoop(SerialPort sp, bool isRight)
    //{
    //    if (sp == null) return;

    //    while (running)
    //    {
    //        try
    //        {
    //            string line = sp.ReadLine();
    //            if (string.IsNullOrEmpty(line)) continue;
    //            string[] parts = line.Trim().Split(',');
    //            if (parts.Length < 7) continue;

    //            // 1. Raw 데이터 파싱
    //            float ax = Parse(parts[1]);
    //            float ay = Parse(parts[2]);
    //            float az = Parse(parts[3]);
    //            float gx = Parse(parts[4]);
    //            float gy = Parse(parts[5]);
    //            float gz = Parse(parts[6]);

    //            lock (lockObj)
    //            {
    //                // [수정] 16개짜리 배열 및 6개짜리 prev 배열 가져오기
    //                float[] target = isRight ? latestRight : latestLeft;
    //                float[] prevRaw = isRight ? prevRightRaw : prevLeftRaw;
    //                bool hasData = isRight ? hasRight : hasLeft;
    //                bool hasPrev = isRight ? hasRightPrev : hasLeftPrev;

    //                // 2. EWMA low-pass (target[0] ~ target[5]에 저장)
    //                if (!hasData)
    //                {
    //                    target[0] = ax; target[1] = ay; target[2] = az;
    //                    target[3] = gx; target[4] = gy; target[5] = gz;
    //                    if (isRight) hasRight = true; else hasLeft = true;
    //                }
    //                else
    //                {
    //                    target[0] = alpha * ax + (1f - alpha) * target[0];
    //                    target[1] = alpha * ay + (1f - alpha) * target[1];
    //                    target[2] = alpha * az + (1f - alpha) * target[2];
    //                    target[3] = alpha * gx + (1f - alpha) * target[3];
    //                    target[4] = alpha * gy + (1f - alpha) * target[4];
    //                    target[5] = alpha * gz + (1f - alpha) * target[5];
    //                }

    //                // [수정] 3. 파생 특성 계산

    //                // 3-1) diff (Δ) (target[6] ~ target[11])
    //                if (!hasPrev)
    //                {
    //                    // 첫 프레임은 diff가 0
    //                    for (int i = 0; i < 6; i++) target[i + 6] = 0f;
    //                    if (isRight) hasRightPrev = true; else hasLeftPrev = true;
    //                }
    //                else
    //                {
    //                    // (현재 필터링된 값) - (이전 필터링된 값)
    //                    target[6] = target[0] - prevRaw[0]; // acc_x_diff
    //                    target[7] = target[1] - prevRaw[1]; // acc_y_diff
    //                    target[8] = target[2] - prevRaw[2]; // acc_z_diff
    //                    target[9] = target[3] - prevRaw[3]; // gyro_x_diff
    //                    target[10] = target[4] - prevRaw[4]; // gyro_y_diff
    //                    target[11] = target[5] - prevRaw[5]; // gyro_z_diff
    //                }
    //                // 현재 값을 '이전 값'으로 저장
    //                Array.Copy(target, 0, prevRaw, 0, 6);

    //                // 3-2) norm (target[12], target[13])
    //                target[12] = Mathf.Sqrt(target[0] * target[0] + target[1] * target[1] + target[2] * target[2]); // acc_norm
    //                target[13] = Mathf.Sqrt(target[3] * target[3] + target[4] * target[4] + target[5] * target[5]); // gyro_norm

    //                // 3-3) tilt (roll, pitch) (target[14], target[15])
    //                float eps = 1e-6f;
    //                float cur_ax = target[0], cur_ay = target[1], cur_az = target[2];

    //                target[14] = Mathf.Atan2(cur_ay, cur_az + eps); // roll
    //                target[15] = Mathf.Atan2(-cur_ax, Mathf.Sqrt(cur_ay * cur_ay + cur_az * cur_az) + eps); // pitch
    //            }
    //        }
    //        catch { /* timeout or parse error -> keep looping */ }
    //    }
    //}

    float Parse(string s)
    {
        if (float.TryParse(s, System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out var v))
            return v;
        return 0f;
    }

    /// <summary>
    /// [수정] 
    /// 왼손 16개(6raw+10derived), 오른손 16개(6raw+10derived)
    /// </summary>
    public bool GetLatest32(out float[] outArr)
    {
        outArr = new float[32];
        lock (lockObj)
        {
            if (!hasRightPrev || !hasLeftPrev) // 둘 중 하나라도 첫 프레임 미도착이면 false
                return false;

            // [0..15] = Right16, [16..31] = Left16  (R → L)
            Array.Copy(latestRight, 0, outArr, 0, 16);
            Array.Copy(latestLeft, 0, outArr, 16, 16);
            return true;
        }
    }

    void ReadLoop(SerialPort sp, bool isRight)
    {
        if (sp == null) return;

        const float eps = 1e-6f;

        while (running)
        {
            try
            {
                string line = sp.ReadLine();
                if (string.IsNullOrEmpty(line)) continue;

                // [수정] 파이썬은 7개(R,ax,ay,az,gx,gy,gz) 또는 8개(R,1,ax,ay,az,gx,gy,gz)를 받습니다.
                // parts[1]이 숫자 '1'일 수 있으므로, 6개 값(ax~gz)을 올바르게 파싱합니다.
                string[] parts = line.Trim().Split(',');
                float[] v6 = new float[6];

                if (parts.Length == 7) // "R,ax,ay,az,gx,gy,gz" 형식
                {
                    for (int i = 0; i < 6; i++) v6[i] = Parse(parts[i + 1]);
                }
                else if (parts.Length == 8) // "R,1,ax,ay,az,gx,gy,gz" 형식
                {
                    for (int i = 0; i < 6; i++) v6[i] = Parse(parts[i + 2]);
                }
                else
                {
                    continue; // 형식이 맞지 않으면 스킵
                }


                lock (lockObj)
                {
                    if (isRight)
                    {
                        if (!hasRight) hasRight = true;
                    }
                    else
                    {
                        if (!hasLeft) hasLeft = true;
                    }

                    float[] target = isRight ? latestRight : latestLeft;
                    float[] prevRaw = isRight ? prevRightRaw : prevLeftRaw;
                    bool hasPrev = isRight ? hasRightPrev : hasLeftPrev;

                    // --- [수정] Python SensorProcessor 로직 시작 ---

                    // 1. Norm 계산
                    float acc_norm = Mathf.Sqrt(v6[0] * v6[0] + v6[1] * v6[1] + v6[2] * v6[2]);
                    float gyro_norm = Mathf.Sqrt(v6[3] * v6[3] + v6[4] * v6[4] + v6[5] * v6[5]);

                    // 2. 정지 상태 감지 (Python과 동일)
                    if ((accNormMin < acc_norm && acc_norm < accNormMax) && (gyro_norm < gyroNormLim))
                    {
                        Array.Clear(target, 0, 16);
                        Array.Copy(v6, prevRaw, 6);
                        if (isRight) hasRightPrev = true; else hasLeftPrev = true;
                        // 최초 수신 플래그도 세움
                        if (isRight) hasRight = true; else hasLeft = true;
                        continue;
                    }

                    // 3. 정지 상태가 아닐 경우: 16개 특성 계산

                    // [0-5]: Raw (v6)
                    Array.Copy(v6, 0, target, 0, 6);

                    // [6-11]: Diff (v6 - prevRaw)
                    if (!hasPrev)
                    {
                        for (int i = 6; i < 12; i++) target[i] = 0f; // 첫 프레임 diff = 0
                        if (isRight) hasRightPrev = true; else hasLeftPrev = true;
                    }
                    else
                    {
                        for (int i = 0; i < 6; i++) target[i + 6] = v6[i] - prevRaw[i];
                    }

                    // 현재 값을 '이전 값'으로 저장
                    Array.Copy(v6, prevRaw, 6);

                    // [12]: acc_norm
                    target[12] = acc_norm;
                    // [13]: gyro_norm
                    target[13] = gyro_norm;

                    // [14-15]: Tilt (roll, pitch)
                    float ax = v6[0], ay = v6[1], az = v6[2];
                    target[14] = Mathf.Atan2(ay, az + eps); // roll
                    target[15] = Mathf.Atan2(-ax, Mathf.Sqrt(ay * ay + az * az) + eps); // pitch

                    // --- Python SensorProcessor 로직 끝 ---
                }
            }
            catch { /* timeout or parse error -> keep looping */ }
        }
    }

}