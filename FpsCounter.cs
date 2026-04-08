using UnityEngine;

public static class FpsCounter
{
    private static float deltaTime;

    public static int GetFps()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        return Mathf.RoundToInt(1.0f / deltaTime);
    }
}
