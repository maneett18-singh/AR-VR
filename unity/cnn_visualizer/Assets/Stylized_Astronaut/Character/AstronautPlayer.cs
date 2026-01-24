using UnityEngine;

namespace AstronautPlayer
{
    public class AstronautPlayer : MonoBehaviour 
    {
        private Animator anim;
        private CharacterController controller;

        [Header("Movement Settings")]
        public float speed = 7.0f;
        public float gravity = 20.0f;
        private Vector3 moveDirection = Vector3.zero;

        [Header("Camera Settings")]
        public Camera playerCamera; 
        public Transform camAnchor; // Assign your "CamAnchor" object here
        public float mouseSensitivity = 200.0f;
        
        private float currentX = 0f;
        private float currentY = 0f;

        void Start () {
            controller = GetComponent<CharacterController>();
            anim = GetComponentInChildren<Animator>();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update () {
            float vertical = Input.GetAxis("Vertical");
            anim.SetInteger("AnimationPar", vertical != 0 ? 1 : 0);

            if(controller.isGrounded) {
                moveDirection = transform.forward * vertical * speed;
            }

            moveDirection.y -= gravity * Time.deltaTime;
            controller.Move(moveDirection * Time.deltaTime);
        }

        void LateUpdate() {
            if (playerCamera == null || camAnchor == null) return;

            currentX += Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            currentY -= Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
            currentY = Mathf.Clamp(currentY, -80f, 80f);

            transform.rotation = Quaternion.Euler(0, currentX, 0);
            playerCamera.transform.position = camAnchor.position;
            playerCamera.transform.rotation = Quaternion.Euler(currentY, currentX, 0);
        }
    }
}