using UnityEngine;

/// <summary>
/// Camara 2.5D: sigue a Bjorn lateralmente pero en PERSPECTIVA.
/// Al ser una camara con profundidad real, las montanas y la aurora
/// (colocadas lejos en Z) se mueven solas mas lento: parallax autentico.
/// </summary>
public class Camara2D : MonoBehaviour
{
    [HideInInspector] public Transform objetivo;
    public float minX = -6f;
    public float maxX = 120f;
    public float distancia = 13f;

    void Start()
    {
        Camera cam = GetComponent<Camera>();
        cam.orthographic = false;
        cam.fieldOfView = 46f;
        cam.nearClipPlane = 0.5f;
        cam.farClipPlane = 250f;
    }

    void LateUpdate()
    {
        if (objetivo == null) return;
        float x = Mathf.Clamp(objetivo.position.x + 1.5f, minX, maxX);
        float y = Mathf.Lerp(transform.position.y, Mathf.Max(3.2f, objetivo.position.y + 1.8f), 4f * Time.deltaTime);
        transform.position = new Vector3(x, y, -distancia);
        transform.rotation = Quaternion.identity;
    }
}
