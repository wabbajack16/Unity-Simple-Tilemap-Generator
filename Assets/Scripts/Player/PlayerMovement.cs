using System.Collections;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private float speed = 4f;
    private float inputX;
    private float inputY;
    private Vector2 inputDir;

    void Awake()
    {
        if (rb == null)
            if (!TryGetComponent<Rigidbody2D>(out rb))
            {
                Debug.LogWarning("Rigidbody2D component not found on " + gameObject.name + ".");
                rb = gameObject.AddComponent<Rigidbody2D>();
            }
    }
    void Update()
    {
        inputX = Input.GetAxisRaw("Horizontal");
        inputY = Input.GetAxisRaw("Vertical");
        inputDir = new Vector2(inputX, inputY).normalized;

    }
    void FixedUpdate()
    {
        rb.velocity = inputDir * speed;
    }

}
