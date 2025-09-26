using UnityEngine;

public class SearchState : BaseState
{
    private float searchTimer;
    private float moveTimer;

    // ADD: sample once per search
    private float searchDuration;      // total time to spend searching before returning to patrol
    private float waitBetweenMoves;    // how long to wait at a point before moving to a new random spot

    public override void Enter()
    {
        // Ensure agent is moving
        if (enemy.Agent != null) enemy.Agent.isStopped = false;

        // Head to last known player position
        enemy.Agent.SetDestination(enemy.LastKnownPos);

        // ADD: initialize timers and one-time thresholds
        searchTimer = 0f;
        moveTimer = 0f;
        searchDuration = Random.Range(5f, 12f);
        waitBetweenMoves = Random.Range(3f, 5f);
    }

    public override void Perform()
    {
        // Do not search outside the guard area
        if (!enemy.IsPlayerInGuardArea)
        {
            stateMachine.ChangeState(new PatrolState());
            return;
        }

        // If we see the player, immediately chase
        if (enemy.CanSeePlayer())
        {
            stateMachine.ChangeState(new ChaseState());
            return;
        }

        // Robust "arrived" check (match PatrolState tolerance)
        if (!enemy.Agent.pathPending &&
            enemy.Agent.remainingDistance <= enemy.Agent.stoppingDistance + 0.1f)
        {
            searchTimer += Time.deltaTime;
            moveTimer += Time.deltaTime;

            // After waiting a bit, move to a nearby random point
            if (moveTimer > waitBetweenMoves)
            {
                // Keep movement roughly on the horizontal plane
                Vector3 randomOffset = Random.insideUnitSphere * 10f;
                randomOffset.y = 0f;

                enemy.Agent.isStopped = false;
                enemy.Agent.SetDestination(enemy.transform.position + randomOffset);

                moveTimer = 0f;
                waitBetweenMoves = Random.Range(3f, 5f); // ADD: resample per hop
            }

            // Return to patrol after the total search duration
            if (searchTimer > searchDuration)
            {
                stateMachine.ChangeState(new PatrolState());
            }
        }
    }

    public override void Exit()
    {
        // no-op
    }
}
