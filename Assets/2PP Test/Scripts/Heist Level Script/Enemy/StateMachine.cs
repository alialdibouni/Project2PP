using UnityEngine;

public class StateMachine : MonoBehaviour
{
    public BaseState activeState;

    public void Initialise() 
    {
        //setup default state
        ChangeState(new PatrolState());
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
