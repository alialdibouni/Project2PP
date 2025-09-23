using UnityEngine;

public class ChaseState : BaseState
{
    public override void Enter()
    {
        // Play once per chase entry
        enemy.PlayChaseAudio();
    }

    public override void Exit()
    {
        // Stop any lingering audio on exit; it will not restart until the next Chase enter
        enemy.StopChaseAudio();
    }

    public override void Perform()
    {
        if (!enemy.IsPlayerInGuardArea)
        {
            stateMachine.ChangeState(new PatrolState());
            return;
        }

        if (enemy.CanSeePlayer())
        {
            enemy.Agent.SetDestination(enemy.Player.transform.position);
            enemy.LastKnownPos = enemy.Player.transform.position;
        }
        else
        {
            enemy.Agent.SetDestination(enemy.LastKnownPos);
            if (!enemy.Agent.pathPending && enemy.Agent.remainingDistance < 0.5f)
            {
                stateMachine.ChangeState(new SearchState());
            }
        }
    }
}
