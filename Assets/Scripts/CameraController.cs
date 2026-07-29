using UnityEngine;

public class CameraController : MonoBehaviour
{
    public HoleController hole;

    float baseY = 12f;
    float baseZ = -9f;

    void LateUpdate()
    {
        float growth = Mathf.Clamp((hole.currentSize - 1.5f) * 0.7f, 0f, 8f);
        Vector3 target = new Vector3(0f, baseY + growth, baseZ - growth * 0.75f);
        transform.position = Vector3.Lerp(transform.position, target, 3f * Time.deltaTime);
    }
}
