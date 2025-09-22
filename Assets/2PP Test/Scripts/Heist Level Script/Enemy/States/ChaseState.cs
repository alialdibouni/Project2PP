using UnityEngine;

public class ChaseState : BaseState
{
    public override void Enter()
    {

    }

    public override void Exit()
    {

    }

    public override void Perform()
    {
        if (enemy.CanSeePlayer()) //player can be seen
        {
            enemy.Agent.SetDestination(enemy.Player.transform.position);
            enemy.LastKnownPos = enemy.Player.transform.position;
            //if close enough to the player, change to attack state
            /*if (Vector3.Distance(enemy.transform.position, enemy.Player.transform.position) < 10f)
            {
                stateMachine.ChangeState(new AttackState());
            }*/
        }
        else //lost sight of player
        {
            //go to last known position
            enemy.Agent.SetDestination(enemy.LastKnownPos);
            //if at last known position, change to search state
            if (!enemy.Agent.pathPending && enemy.Agent.remainingDistance < 0.5f)
            {
                stateMachine.ChangeState(new SearchState());
            }
        }
    }
}
