using Riptide;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    private PlayerCharacter player;
    private Camera playerCam;
    [SerializeField] private CharacterController controller;
    [SerializeField] private float gravity;
    [SerializeField] private float moveSpeed;
    [SerializeField] private float jumpSpeed;

    private bool[] inputs;
    private float yVelocity;
    public ushort playerId;

    private void OnValidate()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        if (player == null)
        {
            if (!NetworkManager.Singleton.isHosting)
                player = Player.localPlayer.character;
            else
                player = Player.List[playerId].character;
        }

        // Doing it this way allows us to make sure we don't select the weapon cam when we add one to avoid clipping
        playerCam = GetComponentInChildren<CameraController>().gameObject.GetComponent<Camera>();

        gravity *= Time.fixedDeltaTime * Time.fixedDeltaTime;
        moveSpeed *= Time.fixedDeltaTime;
        jumpSpeed *= Time.fixedDeltaTime;

        inputs = new bool[5];
    }

    private void Update()
    {
        if (NetworkManager.Singleton.isHosting)
            return;

        // Sample inputs every frame and store them until they're sent. This ensures no inputs are missed because they happened between FixedUpdate calls
        if (Input.GetKey(KeyCode.Z))
            inputs[0] = true;

        if (Input.GetKey(KeyCode.S))
            inputs[1] = true;

        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow))
            inputs[2] = true;

        if (Input.GetKey(KeyCode.D))
            inputs[3] = true;

        if (Input.GetKey(KeyCode.Space))
            inputs[4] = true;
    }

    private void FixedUpdate()
    {
        if (NetworkManager.Singleton.isHosting)
            return;

        Vector3 inputDirection = Vector3.zero;
        if (inputs[0])
            inputDirection.z += 1;

        if (inputs[1])
            inputDirection.z -= 1;

        if (inputs[2])
            inputDirection.x -= 1;

        if (inputs[3])
            inputDirection.x += 1;

        inputDirection = transform.TransformDirection(inputDirection);
        //inputDirection = transform.right * inputDirection.x + transform.forward * inputDirection.y + Vector3.forward * inputDirection.z;
        SendInputs(inputDirection, inputs[4], Rotation);

        for (int i = 0; i < inputs.Length; i++)
            inputs[i] = false;
    }

    private Vector2 Rotation => new Vector2(playerCam.transform.localEulerAngles.x, player.transform.localEulerAngles.y);

    public void Move(Vector3 inputDirection, bool jump, Vector2 rotation)
    {
        // Update player body direction
        Vector3 newRotation = transform.localEulerAngles;
        newRotation.y = rotation.y;
        transform.localEulerAngles = newRotation;

        // Update player camera direction
        newRotation = playerCam.transform.localEulerAngles;
        newRotation.x = rotation.x;
        playerCam.transform.localEulerAngles = newRotation;

        // Update player movement
        Vector3 moveDirection = inputDirection;
        moveDirection *= moveSpeed;

        if (controller.isGrounded)
        {
            yVelocity = 0f;
            if (jump)
                yVelocity = jumpSpeed;
        }
        yVelocity += gravity;

        moveDirection.y = yVelocity;
        controller.Move(moveDirection);

        SendMovement();
    }

    #region Messages
    private void SendMovement()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, MessageId.PlayerMovement);
        message.AddUShort(player.Id);
        message.AddVector3(transform.position);
        message.AddVector3(transform.forward);
        
        foreach(Player raider in Player.ServerListRaiders)
        {
            NetworkManager.Singleton.Server.Send(message, raider.Id);
        }
    }

    private void SendInputs(Vector3 inputDirection, bool jump, Vector2 rotation)
    {
        Message message = Message.Create(MessageSendMode.Unreliable, MessageId.PlayerMovement);
        message.AddVector3(inputDirection);
        message.AddBool(jump);
        message.AddVector2(rotation);

        NetworkManager.Singleton.Client.Send(message);
    }
    #endregion
}