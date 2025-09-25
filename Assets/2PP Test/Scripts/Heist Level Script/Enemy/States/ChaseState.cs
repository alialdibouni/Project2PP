using UnityEngine;

public class ChaseState : BaseState
{
    private float lostSightTimer;
    private const float LostSightTimeout = 5f;
    private float sqrCatchDistance;

    public override void Enter()
    {
        lostSightTimer = 0f;
        enemy.ApplyChaseStoppingDistance();
        if (enemy.Agent != null) enemy.Agent.isStopped = false;

        sqrCatchDistance = enemy.CatchDistance * enemy.CatchDistance;

        // Voice line (intro once, then random)
        enemy.PlayChaseAudio();

        // Start chase music crossfade
        BackgrondMusic.Instance?.BeginChase();
    }

    public override void Exit()
    {
        enemy.StopChaseAudio();
        enemy.RestoreOriginalStoppingDistance();

        // End chase music crossfade (reference counted across multiple chases / enemies)
        BackgrondMusic.Instance?.EndChase();
    }

    public override void Perform()
    {
        if (enemy.IsProcessingCatch) return;

        // Distance-based catch
        if (enemy.Player != null)
        {
            Vector3 diff = enemy.Player.transform.position - enemy.transform.position;
            if (diff.sqrMagnitude <= sqrCatchDistance)
            {
                enemy.CatchPlayer();
                return;
            }
        }

        // Abort if player leaves guard area
        if (!enemy.IsPlayerInGuardArea)
        {
            stateMachine.ChangeState(new PatrolState());
            return;
        }

        // Maintain pursuit / memory
        if (enemy.CanSeePlayer())
        {
            lostSightTimer = 0f;
            if (enemy.Agent != null && enemy.Player != null)
            {
                enemy.Agent.SetDestination(enemy.Player.transform.position);
                enemy.LastKnownPos = enemy.Player.transform.position;
            }
        }
        else
        {
            if (enemy.Agent != null)
                enemy.Agent.SetDestination(enemy.LastKnownPos);

            lostSightTimer += Time.deltaTime;
            if (lostSightTimer >= LostSightTimeout)
            {
                stateMachine.ChangeState(new PatrolState());
            }
        }
    }
}
