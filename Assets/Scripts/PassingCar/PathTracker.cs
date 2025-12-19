using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PathTracker : MonoBehaviour
{

    public Transform Target;

    public NavMeshAgent agent;


    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    // Update is called once per frame
    void Update()
    {
        agent.destination = Target.position;
        
    }
}
