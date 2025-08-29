using UnityEngine;

public class PatrolState : BaseState
{
    public int waypointIndex; //track which waypoint we are currently targetting
    public float waitTimer; //how long we have waited at a waypoint
    public override void Enter()
    {

    }

    public override void Perform()
    {
        PatrolCycle();
        if (enemy.CanSeePlayer())
        {
            stateMachine.ChangeState(new AttackState());
        }
    }

    public override void Exit()
    {

    }

    public void PatrolCycle() 
    {
        //implement our patrol logic
        if (enemy.Agent.remainingDistance < 0.2f)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer > 3f) //wait for 2 seconds at each waypoint
            {


                if (waypointIndex < enemy.path.waypoints.Count - 1)
                    waypointIndex++;
                else
                    waypointIndex = 0;
                enemy.Agent.SetDestination(enemy.path.waypoints[waypointIndex].position);
                waitTimer = 0f;
            }
        }
    }
}
