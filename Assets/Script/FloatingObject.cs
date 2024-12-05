using UnityEngine;

public class FloatingObject : MonoBehaviour
{
    [Header("Orbit Settings")]
    [SerializeField] private float orbitSpeed = 1.0f;
    [SerializeField] private float rotationSpeed = 30.0f;
    [SerializeField] private float orbitRadius = 500f;
    [SerializeField] private float heightOffset = 0f;
    [SerializeField] private float orbitTilt = 15f;

    private float currentAngle = 0f;
    private Vector3 currentRotation;

    private void Start()
    {
        currentAngle = Random.Range(0f, 360f);
    }

    private void Update()
    {
        currentAngle += orbitSpeed * Time.deltaTime;

        float x = Mathf.Cos(currentAngle) * orbitRadius;
        float z = Mathf.Sin(currentAngle) * orbitRadius;

        float tiltedY = Mathf.Sin(currentAngle) * orbitRadius * Mathf.Sin(orbitTilt * Mathf.Deg2Rad);

        transform.position = new Vector3(x, tiltedY + heightOffset, z);

        currentRotation += new Vector3(0, rotationSpeed * Time.deltaTime, 0);
        transform.rotation = Quaternion.Euler(currentRotation);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        DrawOrbitPath();
    }

    private void DrawOrbitPath()
    {
        const int segments = 50;
        Vector3 previousPoint = GetOrbitPosition(0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2;
            Vector3 nextPoint = GetOrbitPosition(angle);
            Gizmos.DrawLine(previousPoint, nextPoint);
            previousPoint = nextPoint;
        }
    }

    private Vector3 GetOrbitPosition(float angle)
    {
        float x = Mathf.Cos(angle) * orbitRadius;
        float z = Mathf.Sin(angle) * orbitRadius;
        float tiltedY = Mathf.Sin(angle) * orbitRadius * Mathf.Sin(orbitTilt * Mathf.Deg2Rad);
        return new Vector3(x, tiltedY + heightOffset, z);
    }
}