//Created By Erico Azevedo

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Panda;

public class TanksAI : MonoBehaviour
{

    private Complete.TankMovement _tMovement;
    private Complete.TankShooting _tShooting;
    private NavMeshAgent _navAgent;
    [SerializeField] private List<Transform> WaypointsList = new List<Transform>();       //A list of waypoints the AI can reach
    private List<Transform> EvadeWaypointsList = new List<Transform>();  //A list of waypoints the AI can hide at

    [SerializeField] private AnimationCurve _LaunchForceCurve;            //Used to accurately change the Launch power of the shell depending on the distance from the other tank


    [Header("Tank AI Settings")]
    [Range(0.0f, 1.0f)]
    public float turnSpeed = 0.3f;                                       //How fast the AI turns
    [Range(0.0f, 1.0f)]
    public float accelerationSpeed = 0.5f;                               //How fast the tank accelerates
    [Range(0.0f, 1.0f)]
    public float decelerationSpeed = 0.02f;                              //How fast the tank decelerates. helps with turning
    public float maxSightDistance = 30f;                                 //How far the tank can see
    [Range(0f,180f)]
    public float fieldOfView = 90f;                                      //AI's field of view 
    [Range(0f,180f)]
    public float fireAngle = 15f;                                        //The angle the enemy tank has to be within from the AI tank's transform.forward for it to fire

    public float evadeTime = 5f;                                         //How long the AI will try to evade 
    public float alertRoamTime = 10f;                                    //Max time the AI will be in the alert roam state
    public float fireRate = 2f;                                          //How often the AI can shoot    

    private Transform _wayPoints;                                        //Reference to the gameobject that holds all the waypoints  
    private float _lunchForce;                                           //The force the AI tank will shoot at
    private Vector3 _enemyTankLastSeen;                                  //Holds the location of the last position the enemy tank was seen
    private GameObject _enemyTank;                                       //Reference to the gameobject of the enemy tank    
    private float _distanceToEnemyTank;                                  //Distance to the enemy tank
    private float _tankTimer = 0f; 

    private int _areaMask = 1;                                          //The walkable area mask on the nav mesh
    private float _randomPosDistance = 5f;                              //The maximum distance a random position can be from the tank
    private float _alertRoamDistance = 10f;                             //The maximum distance the AI will travel from the last spot the enemy tank was seen
    private float _wayPointAngle = 15f;                                 //The maximum angle a waypoint can be from the forward direction of the tank. If within this angle, the AI will stop turning to face it
    private bool _wayPointFound;                                        //Is true if the AI has found a waypoint to travel to
    private float _timerLimit;

    private int _targetWaypoint;                                        //The waypoint number of the waypoint the AI is moving to
    private bool _randomPointSet = false;

    public void Awake()
    {
        if (GameObject.FindGameObjectWithTag("WayPoints").transform != null)
        {
            _wayPoints = GameObject.FindGameObjectWithTag("WayPoints").transform;
        }
        else
        {
            Debug.Log("No Objects with tag 'Waypoints'");
        }
    }

    void Start()
    {
        _navAgent = GetComponent<NavMeshAgent>();
        _tMovement = gameObject.GetComponent<Complete.TankMovement>();
        _tShooting = gameObject.GetComponent<Complete.TankShooting>();

        if (gameObject.CompareTag("Player1"))
        {
            if (GameObject.FindGameObjectWithTag("Player2") != null)
            {
                _enemyTank = GameObject.FindGameObjectWithTag("Player2");
            }
            else
            {
                //Debug.Log("No Object with tag 'Player2'");
            }
        }
        else
        {
            if (GameObject.FindGameObjectWithTag("Player1") != null)
            {
                _enemyTank = GameObject.FindGameObjectWithTag("Player1");
            }
            else
            {
                //Debug.Log("No Object with tag 'Player1'");
            }
        }

        InitializeTank();
    }

    public void ResetAi()   //Used when the round restarts
    {
        ToRoamState();
    }

    //Variables and functions that are used in the behavior tree need to have the Task header 
    [Task]
    public bool isRoaming;
    [Task]
    public bool isHunting;
    [Task]
    public bool isAlertRoaming;
    [Task]
    public bool isEvading;
    [Task]
    public bool isGameRunning;
    [Task]
    public bool canSeeEnemyTank;

    //These functions are used to change the state the AI tank is in
    #region
    [Task]
    public void ToEvadeState()
    {
        //Debug.Log("Switching to evade state");
        isRoaming = false;
        isHunting = false;
        isAlertRoaming = false;
        isEvading = true;
    }

    [Task]
    public void ToRoamState()
    {
        //Debug.Log("Switching to roam state");
        isRoaming = true;
        isHunting = false;
        isAlertRoaming = false;
        isEvading = false;
    }

    [Task]
    public void ToHuntingState()
    {
        //Debug.Log("Switching to hunting state");

        isRoaming = false;
        isHunting = true;
        isAlertRoaming = false;
        isEvading = false;
    }

    [Task]
    public void ToAlertRoamState()
    {
        //Debug.Log("Switching to alert roam state");
        isRoaming = false;
        isHunting = false;
        isAlertRoaming = true;
        isEvading = false;
        _enemyTankLastSeen = _enemyTank.transform.position;
    }
    #endregion

    /* CanSeeEnemy() checks if enemy tank can be seen and then changes the 'canSeeEnemyTank' variable to true or false. 
       Uses an angle to determine if the enemy tank is within the tanks FOV. FOV angle is determined by the state the AI tank is in*/
    [Task]
    public void CanSeeEnemy()
    {
        CalculateDistToEnemyTank();
        float viewAngle;

        if (isHunting)
        {
            viewAngle = fireAngle;
        }
        else
        {
            viewAngle = fieldOfView;
        }

        Vector3 _rayStartPos = new Vector3(transform.position.x, transform.position.y + 1f, transform.position.z);
        Vector3 _rayDirection = _enemyTank.transform.position - gameObject.transform.position;

        if (Vector3.Angle(_rayDirection, transform.forward) < viewAngle / 2f && _distanceToEnemyTank < maxSightDistance)
        {
            RaycastHit _hit;
            if (Physics.Raycast(_rayStartPos, _rayDirection, out _hit, maxSightDistance))
            {
                if (_hit.collider.gameObject.CompareTag(_enemyTank.tag))
                {
                    canSeeEnemyTank = true;
                }
                else
                {
                    canSeeEnemyTank = false;
                }
            }
        }
        else
        {
            canSeeEnemyTank = false;
        }
        Task.current.Succeed();
    }

    /* SelectRandomWaypoint() selects a random waypoint when no waypoint has been selected and when the AI reaches its current target waypoint, it selects another one */
    [Task]
    public void SelectRandomWaypoint()
    {
        if (_navAgent)
        {
            if (!_wayPointFound)
            {
                _targetWaypoint = Random.Range(0, WaypointsList.Count);
                _wayPointFound = true;
            }

            _navAgent.destination = WaypointsList[_targetWaypoint].position;


            if (_navAgent.remainingDistance <= _navAgent.stoppingDistance && !_navAgent.pathPending)
            {
                _targetWaypoint = Random.Range(0, WaypointsList.Count);
            }
            Task.current.Succeed();
        }
        else
        {
            Task.current.Fail();
        }
    }

    /*CreateEvadeWaypointsList() creates a list of waypoints that can't be seen by the enemy tank. These are waypoints that the AI can use to hide so that it can't be seen by the enemy tank.
    This list is sorted by the distance from the AI. The closer the waypoint is to the AI, the higher up the list it goes */
    [Task]
    public void CreateEvadeWaypointsList()
    {
        EvadeWaypointsList.Clear();
        foreach (Transform waypoint in WaypointsList)
        {
            Vector3 _rayDirection = _enemyTank.transform.position - waypoint.position;
            float _distanceToTarget = Vector3.Distance(waypoint.position, _enemyTank.transform.position);
            RaycastHit hit;
            if (Physics.Raycast(waypoint.position, _rayDirection, out hit, _distanceToTarget))
            {
                if (!hit.collider.gameObject.CompareTag(_enemyTank.gameObject.tag))
                    EvadeWaypointsList.Add(waypoint);
            }
        }
        EvadeWaypointsList.Sort((c1, c2) => Vector3.Distance(_enemyTank.transform.position, _enemyTank.transform.position).CompareTo
            ((Vector3.Distance(transform.position, c2.transform.position))));
        Task.current.Succeed();
    }
    /*FindEvadePosition() sets the AI destination to the position of the first waypoint in the Evadewaypoint list which should be the closest waypoint that can't be seen by the enemy tank */
    [Task]
    public void FindEvadePosition()
    {
        if (_navAgent)
        {
            _navAgent.destination = EvadeWaypointsList[EvadeWaypointsList.Count - 1].position;
            Task.current.Succeed();
        }
    }

    /* CreateAlertRoamPos() creates an alert roam position by selecting a random point around the last position that the enemy tank was seen at. 
    */
    [Task]
    public void CreateAlertRoamPos()
    {
        if (!_randomPointSet)
        {
            _navAgent.destination = _enemyTankLastSeen;
            if (_navAgent.remainingDistance <= _navAgent.stoppingDistance)
            {
                _navAgent.destination = GetRandomPatrolPos();
                _randomPointSet = true;
            }
        }
        else
        {
            if (_navAgent.remainingDistance <= _navAgent.stoppingDistance)
            {
                _randomPointSet = false;
                _navAgent.destination = GetRandomPatrolPos();
            }
        }
        Task.current.Succeed();
    }

    /* UpdateTargetPos() updates the AI destination to the enemy tanks current position. This is used to follow the enemy tank*/
    [Task]
    public void UpdateTargetPos()
    {
        _navAgent.destination = _enemyTank.transform.position;
        Task.current.Succeed();
    }

    /* FireShell() fires a missle if A: The seconds since the last time a shell was fired is higher than the fire rate. (If fire rate is 2, the AI will have to wait 2 seconds after firing a missle to be able to fire
    another one.) B: The distance to the enemy tank is shorter than the AI's maximum sight distance.*/
    [Task]
    public void FireShell()
    {
        if (_tankTimer >= fireRate && _distanceToEnemyTank < maxSightDistance)
        {
            AnimationCurve();
            _tShooting.AiFire();
            ResetTimer();
        }
        Task.current.Succeed();
    }

    /* Timer() is used to keep track of the seconds passed since _tankTimer was last reset.*/
    [Task]
    public void Timer()
    {
        _tankTimer += Time.deltaTime;
        Task.current.Succeed();
    }
    /*Resets the timer*/
    public void ResetTimer()
    {
        _tankTimer = 0f;
    }
    /* TimerChecker() is used to check if timer has passed a certain limit. The limit is dependant on the state the AI is in. For example when the AI is in the evade state the limit is now the EvadeTime which
    is the amount of time the AI will be in the evade state before switching back to the roaming state*/
    [Task]
    public void TimerChecker()

    {
        if (isEvading)
        {
            _timerLimit = evadeTime;
        }
        else if (isAlertRoaming)
        {
            _timerLimit = alertRoamTime;
        }

        if (_tankTimer >= _timerLimit)
        {
            ToRoamState();
            ResetTimer();
        }
        Task.current.Succeed();
    }
    /* MoveToTargetPos() rotates AI tank so that it faces the next nav agent path corner and accelerates the tank if its facing it. The turnspeed is the speed that the tank turns at. accelerationSpeed and decelerationSpeed is the 
     speed that the tank accelerates and decelerates*/
    [Task]
    public void MoveToTargetPos()    
    {
        if (_navAgent.path.corners.Length > 1)
        {
            Vector3 direction = _navAgent.path.corners[1] - transform.position;
            float _angle = DirectionCheck(transform.forward, direction, transform.up);
            if (!IsTargetWayPointInfront())
            {
                if (_angle > 0)
                {
                    _tMovement.m_TurnInputValue += turnSpeed;
                }
                else if (_angle < 0)
                {
                    _tMovement.m_TurnInputValue -= turnSpeed;
                }

                _tMovement.m_MovementInputValue -= decelerationSpeed;
            }
            else
            {
                _tMovement.m_TurnInputValue = 0f;
                _tMovement.m_MovementInputValue += accelerationSpeed;
            }
            _tMovement.m_TurnInputValue = Mathf.Clamp(_tMovement.m_TurnInputValue, -1f, 1f);
            _tMovement.m_MovementInputValue = Mathf.Clamp(_tMovement.m_MovementInputValue, 0, 1f);

        }
        Task.current.Succeed();
    }

    public void AnimationCurve()  //AnimationCurve() is used to calcuate the launch power needed to hit the enemy tank based on its distanced.
    {
        float _norDistanceToTank = _distanceToEnemyTank / maxSightDistance;
        //Debug.Log("Distance to other tank is: " + norDistanceToTank);
        float _norLunchForce = _LaunchForceCurve.Evaluate(_norDistanceToTank);
        _lunchForce = Mathf.Lerp(_tShooting.m_MinLaunchForce, _tShooting.m_MaxLaunchForce, _norLunchForce);
        _lunchForce = Mathf.RoundToInt(_lunchForce);
        _tShooting.m_CurrentLaunchForce = _lunchForce;
    }

    public bool IsTargetWayPointInfront()       //IsTargetWayPointInfront() checks if the next waypoint position is infront of the AI tank
    {
        Vector3 _rayDirection = _navAgent.path.corners[1] - transform.position;
        if (Vector3.Angle(_rayDirection, transform.forward) < _wayPointAngle / 2)
        {
            return true;
        }
        return false;
    }

    float DirectionCheck(Vector3 forward, Vector3 targetDir, Vector3 up)  // Checks if a target is to the left or right of the AI tank's forward direction.
    {
        Vector3 _perp = Vector3.Cross(forward, targetDir);
        float _direction = Vector3.Dot(_perp, up);

        if (_direction > 0f)
        {
            return 1f;
        }
        else if (_direction < 0f)
        {
            return -1f;
        }
        else
        {
            return 0f;
        }
    }

    /* Finds the closest point on the navmesh from random point created for the alert roam destination */
    Vector3 GetRandomPatrolPos()
    {
        NavMeshHit _hit;
        NavMesh.SamplePosition((Random.insideUnitSphere * _alertRoamDistance) + _enemyTankLastSeen, out _hit, _randomPosDistance, _areaMask);
        return _hit.position;
    }

    /*Used to intialize the AI tank at the start of the game*/
    void InitializeTank()
    {
        if (_LaunchForceCurve.length < 2) { //Opening Project in different version of unity deletes the keys. This makes sure they are always set to the correct values.
            _LaunchForceCurve.AddKey(0, 0);
            _LaunchForceCurve.AddKey(1, 0.5f);
        }

        ToRoamState();
        CheckWayPoints();
        _navAgent.isStopped = true;
    }

    void CheckWayPoints() //CheckWayPoints() checks if waypoints set in the map can be reached by the AI. If they can, they are added to a list. if not, they are destroyed 
    {
        foreach (Transform waypoint in _wayPoints)
        {
            NavMeshPath _path = new NavMeshPath();
            _navAgent.CalculatePath(waypoint.position, _path);
            if (_path.status == NavMeshPathStatus.PathComplete)
            {
                WaypointsList.Add(waypoint);
            }
            else
            {
                Destroy(waypoint.gameObject);
            }
        }
    }

    public void CalculateDistToEnemyTank() //CalculateDistToEnemyTank() calculates the distance to enemy tank in game and sets it as a variable 
    {
        _distanceToEnemyTank = Vector3.Distance(_enemyTank.transform.position, transform.position);
        Task.current.Succeed();
    }

    /*Use to show the path the AI will be taking */
    private void DrawCurrentNavAgentPath(Color color)
    {
        for (int i = 0; i < _navAgent.path.corners.Length - 1; i++)
        {
            Debug.DrawLine(_navAgent.path.corners[i], _navAgent.path.corners[i + 1], color);
        }
    }

}
