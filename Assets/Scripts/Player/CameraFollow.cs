using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Camera mainCamera;
    [SerializeField] private float smoothSpeed;
    private Vector2 pos;
    private Vector3 targetPos;
    void Awake()
    {
        mainCamera = GetComponent<Camera>();

    }

    void Start()
    {
        mainCamera.allowMSAA = false;
        smoothSpeed = 1.5f;

    }
    void LateUpdate()
    {
            pos = Player.Instance.GetPos();
            targetPos = new Vector3(pos.x, pos.y, transform.position.z);

            transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
    }

}
