using UnityEngine;

public class Shredder_work : MonoBehaviour
{
    [Header("Blade Settings")]
    public Transform blade1;          
    public Transform blade2;          
    public float speed = 2f;         
    public float moveDistance = 1f;  

    [Header("Physical Conveyors")]
    public ConveyorMover conveyor1;   
    public ConveyorMover conveyor2;   

    [Header("Visual Conveyor Belts")]
    public ConveyorBelt belt1;       
    public ConveyorBelt belt2;       

    private Vector3 blade1Start;
    private Vector3 blade2Start;
    private bool isRunning = true;   
    private float elapsedTime = 0f;

    [Header("Tower Reference")]
    public RotatingTower tower;
    void Start()
    {
        if (blade1 == null || blade2 == null)
        {
            Debug.LogError("Blades not assigned!");
            enabled = false;
            return;
        }

        blade1Start = blade1.localPosition;
        blade2Start = blade2.localPosition;

        // Start conveyors if shredder is initially ON
        if (isRunning)
        {
            conveyor1?.StartConveyor();
            conveyor2?.StartConveyor();
            belt1?.StartConveyor();
            belt2?.StartConveyor();
        }

        Debug.Log("Shredder started (initial state ON)");
    }

    void Update()
    {
        if (!isRunning) return;

        elapsedTime += Time.deltaTime;

        float offset1 = Mathf.Sin(elapsedTime * speed * Mathf.PI) * moveDistance * 0.5f;
        float offset2 = Mathf.Sin(elapsedTime * speed * Mathf.PI + Mathf.PI) * moveDistance * 0.5f;

        blade1.localPosition = blade1Start + new Vector3(0f, offset1, 0f);
        blade2.localPosition = blade2Start + new Vector3(0f, offset2, 0f);
    }

    public void StartMachine()
    {
        isRunning = true;
        Debug.Log("Shredder started!");

        // Physical conveyors
        conveyor1?.StartConveyor();
        conveyor2?.StartConveyor();

        // Visual belts
        belt1?.StartConveyor();
        belt2?.StartConveyor();

        tower?.StartRotation(); // Start tower
    }

    public void StopMachine()
    {
        isRunning = false;
        Debug.Log("Shredder stopped!");

        // Physical conveyors
        conveyor1?.StopConveyor();
        conveyor2?.StopConveyor();
        

        // Visual belts
        belt1?.StopConveyor();
        belt2?.StopConveyor();

        tower?.StopRotation(); // Stop tower
    }
}
