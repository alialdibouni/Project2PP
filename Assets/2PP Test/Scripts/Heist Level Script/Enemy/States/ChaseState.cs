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
        // Do not chase outside the guard area
        if (!enemy.IsPlayerInGuardArea)
        {
            stateMachine.ChangeState(new PatrolState());
            return;
        }

        if (enemy.CanSeePlayer()) //player can be seen
        {
            enemy.Agent.SetDestination(enemy.Player.transform.position);
            enemy.LastKnownPos = enemy.Player.transform.position;
        }
        else //lost sight of player
        {
            enemy.Agent.SetDestination(enemy.LastKnownPos);
            if (!enemy.Agent.pathPending && enemy.Agent.remainingDistance < 0.5f)
            {
                stateMachine.ChangeState(new SearchState());
            }
        }
    }
}
