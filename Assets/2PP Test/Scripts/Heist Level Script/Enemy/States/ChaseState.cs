using UnityEngine;

public class ChaseState : BaseState
{
    private float lostSightTimer;
    private const float LostSightTimeout = 5f;

    public override void Enter()
    {
        lostSightTimer = 0f;
        // Play once per chase entry
        enemy.PlayChaseAudio();

        if (enemy.Agent != null)
        {
            enemy.Agent.isStopped = false;
        }
    }

    public override void Exit()
    {
        // Stop any lingering audio on exit; it will not restart until the next Chase enter
        enemy.StopChaseAudio();
        lostSightTimer = 0f;
    }

    public override void Perform()
    {
        // Optional: still enforce guarding area (leave chase immediately outside area)
        if (!enemy.IsPlayerInGuardArea)
        {
            stateMachine.ChangeState(new PatrolState());
            return;
        }

        if (enemy.CanSeePlayer())
        {
            // Reset timer if we can see the player again
            lostSightTimer = 0f;

            enemy.Agent.SetDestination(enemy.Player.transform.position);
            enemy.LastKnownPos = enemy.Player.transform.position;
        }
        else
        {
            // Lost line of sight: move to last known position and start timeout
            lostSightTimer += Time.deltaTime;

            enemy.Agent.SetDestination(enemy.LastKnownPos);

            if (lostSightTimer >= LostSightTimeout)
            {
                stateMachine.ChangeState(new PatrolState());
            }
        }
    }
}
