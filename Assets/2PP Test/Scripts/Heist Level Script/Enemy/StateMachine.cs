using UnityEngine;

public class StateMachine : MonoBehaviour
{
    public BaseState activeState;
    //property for the patrol state
    public PatrolState patrolState;

    public void Initialise() 
    {
        //setup default state
        patrolState = new PatrolState();
        ChangeState(patrolState);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (activeState != null) 
        {
            activeState.Perform();
        }
    }

    public void ChangeState(BaseState newState) 
    {
        //check active state is not null
        if (activeState != null) 
        {
            //run cleanup on activestate
            activeState.Exit();
        }
        //change into a new state
        activeState = newState;

        //failsafe null check to make sure new state is not null
        if (activeState != null) 
        {
            //run setup on new state
            activeState.stateMachine = this;
            //assign state enemy class
            activeState.enemy = GetComponent<Enemy>();
            activeState.Enter();
        }
    }
}
