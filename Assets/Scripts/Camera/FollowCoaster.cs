using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowCoaster : MonoBehaviour
{
    [SerializeField] private Transform coasterCart; // Reference to the coaster cart
    [SerializeField] private Vector3 offset = new Vector3(0, 2, -5); // Offset from the cart
    [SerializeField] private float followSpeed = 5f; // Speed at which the camera follows
    [SerializeField] private Camera mainCamera; // Reference to the main camera

    void Update()
    {
        cameraFollow();
    }

    private void cameraFollow()
    {
        if (coasterCart == null || mainCamera == null) return;

        // Calculate the desired position based on the cart's position and the offset
        Vector3 desiredPosition = coasterCart.position + offset;

        // Smoothly interpolate the camera's position towards the desired position
        mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, desiredPosition, followSpeed * Time.deltaTime);
    }
}
